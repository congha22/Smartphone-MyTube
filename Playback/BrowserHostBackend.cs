using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text.Json;
using StardewModdingAPI;
using SmartphoneMyTube.Shared;

namespace SmartphoneMyTube.Playback;

internal sealed class BrowserHostBackend : IPlaybackBackend
{
    private const int PlayerWidth = 854;
    private const int PlayerHeight = 480;
    private const int MaxFrameBytes = PlayerWidth * PlayerHeight * 4;

    private readonly IMonitor monitor;
    private readonly string modDirectory;
    private readonly object commandLock = new();

    private Process? process;
    private MemoryMappedFile? frameMap;
    private MemoryMappedViewAccessor? frameView;
    private string? frameFile;
    private long lastSequence;
    private PlaybackFrame? pendingFrame;
    private bool launchAttempted;

    public BrowserHostBackend(string modDirectory, IMonitor monitor)
    {
        this.modDirectory = modDirectory;
        this.monitor = monitor;
        Status = "Browser host not started.";
    }

    public bool IsAvailable => process is { HasExited: false };
    public string Status { get; private set; }

    public void LoadVideo(string videoId)
    {
        EnsureStarted();
        if (!IsAvailable)
            return;

        SendCommand(new BrowserHostCommand { Type = "load", VideoId = videoId });
        Status = "Loading video...";
    }

    public void SendMouseClick(int x, int y)
    {
        if (!IsAvailable)
            return;
        SendCommand(new BrowserHostCommand { Type = "mouseClick", X = x, Y = y });
    }

    public void Update()
    {
        if (process is { HasExited: true })
        {
            Status = $"Browser host exited with code {process.ExitCode}.";
            return;
        }

        ReadFrameIfChanged();
    }

    public bool TryGetLatestFrame(out PlaybackFrame? frame)
    {
        frame = pendingFrame;
        pendingFrame = null;
        return frame != null;
    }

    private void EnsureStarted()
    {
        if (IsAvailable || launchAttempted)
            return;

        launchAttempted = true;
        string rid = GetRuntimeIdentifier();
        string executableName = OperatingSystem.IsWindows()
            ? "SmartphoneMyTube.BrowserHost.exe"
            : "SmartphoneMyTube.BrowserHost";
        string hostPath = Path.Combine(modDirectory, "browserhost", rid, executableName);

        if (!File.Exists(hostPath))
        {
            Status = $"Browser host missing for {rid}. Expected: {hostPath}";
            monitor.Log(Status, LogLevel.Warn);
            return;
        }

        try
        {
            frameFile = Path.Combine(Path.GetTempPath(), $"smartphone-mytube-{Environment.ProcessId}-{Guid.NewGuid():N}.frame");
            long capacity = FrameBufferLayout.HeaderSize + MaxFrameBytes;
            using (FileStream fs = new(frameFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                fs.SetLength(capacity);

            frameMap = MemoryMappedFile.CreateFromFile(frameFile, FileMode.Open, null, capacity, MemoryMappedFileAccess.ReadWrite);
            frameView = frameMap.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.ReadWrite);
            frameView.Write(FrameBufferLayout.MagicOffset, FrameBufferLayout.Magic);
            frameView.Write(FrameBufferLayout.VersionOffset, FrameBufferLayout.Version);

            var psi = new ProcessStartInfo
            {
                FileName = hostPath,
                Arguments = $"--frame-file \"{frameFile}\" --width {PlayerWidth} --height {PlayerHeight} --referer https://d5a1lamdtd.smartphone-mytube",
                WorkingDirectory = Path.GetDirectoryName(hostPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => HandleHostOutput(e.Data);
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    monitor.Log($"BrowserHost: {e.Data}", LogLevel.Trace);
            };
            process.Exited += (_, _) => Status = $"Browser host exited with code {process?.ExitCode}.";

            if (!process.Start())
                throw new InvalidOperationException("Process.Start returned false.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Status = "Browser host started.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to start browser host: {ex.Message}";
            monitor.Log(Status, LogLevel.Error);
            DisposeProcess();
        }
    }

    private void HandleHostOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            BrowserHostStatus? status = JsonSerializer.Deserialize<BrowserHostStatus>(line);
            if (status?.Message is { Length: > 0 })
                Status = status.Message;
        }
        catch
        {
            monitor.Log($"BrowserHost: {line}", LogLevel.Trace);
        }
    }

    private void SendCommand(BrowserHostCommand command)
    {
        try
        {
            lock (commandLock)
            {
                if (process is not { HasExited: false })
                    return;
                process.StandardInput.WriteLine(JsonSerializer.Serialize(command));
                process.StandardInput.Flush();
            }
        }
        catch (Exception ex)
        {
            Status = $"Failed to send command to browser host: {ex.Message}";
        }
    }

    private void ReadFrameIfChanged()
    {
        MemoryMappedViewAccessor? view = frameView;
        if (view == null)
            return;

        try
        {
            int magic = view.ReadInt32(FrameBufferLayout.MagicOffset);
            int version = view.ReadInt32(FrameBufferLayout.VersionOffset);
            long sequenceBefore = view.ReadInt64(FrameBufferLayout.SequenceOffset);
            if (magic != FrameBufferLayout.Magic || version != FrameBufferLayout.Version || sequenceBefore <= lastSequence)
                return;

            int width = view.ReadInt32(FrameBufferLayout.WidthOffset);
            int height = view.ReadInt32(FrameBufferLayout.HeightOffset);
            int length = view.ReadInt32(FrameBufferLayout.DataLengthOffset);
            if (width <= 0 || height <= 0 || length != width * height * 4 || length > MaxFrameBytes)
                return;

            byte[] pixels = new byte[length];
            view.ReadArray(FrameBufferLayout.PixelDataOffset, pixels, 0, length);
            long sequenceAfter = view.ReadInt64(FrameBufferLayout.SequenceOffset);
            if (sequenceAfter != sequenceBefore)
                return;

            lastSequence = sequenceAfter;
            pendingFrame = new PlaybackFrame(width, height, pixels, sequenceAfter);
        }
        catch (Exception ex)
        {
            monitor.Log($"Failed reading MyTube frame: {ex.Message}", LogLevel.Trace);
        }
    }

    private static string GetRuntimeIdentifier()
    {
        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };

        if (OperatingSystem.IsWindows()) return $"win-{arch}";
        if (OperatingSystem.IsLinux()) return $"linux-{arch}";
        if (OperatingSystem.IsMacOS()) return $"osx-{arch}";
        return $"unknown-{arch}";
    }

    public void Dispose()
    {
        try { SendCommand(new BrowserHostCommand { Type = "shutdown" }); } catch { }
        DisposeProcess();
        frameView?.Dispose();
        frameMap?.Dispose();
        frameView = null;
        frameMap = null;

        if (frameFile != null)
        {
            try { File.Delete(frameFile); } catch { }
            frameFile = null;
        }
    }

    private void DisposeProcess()
    {
        if (process != null)
        {
            try
            {
                if (!process.HasExited && !process.WaitForExit(500))
                    process.Kill(entireProcessTree: true);
            }
            catch { }
            process.Dispose();
            process = null;
        }
    }
}
