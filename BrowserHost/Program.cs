using System.Collections.Concurrent;
using System.Text.Json;
using SmartphoneMyTube.Shared;
using Xilium.CefGlue;

namespace SmartphoneMyTube.BrowserHost;

internal static class Program
{
    private static readonly ConcurrentQueue<BrowserHostCommand> Commands = new();
    private static CefBrowser? browser;
    private static bool running = true;
    private static string referer = "https://d5a1lamdtd.smartphone-mytube";

    public static int Main(string[] args)
    {
        Dictionary<string, string> options = ParseArgs(args);
        if (!options.TryGetValue("frame-file", out string? frameFile))
        {
            Console.Error.WriteLine("Missing --frame-file.");
            return 2;
        }

        int width = options.TryGetValue("width", out string? w) && int.TryParse(w, out int wi) ? wi : 854;
        int height = options.TryGetValue("height", out string? h) && int.TryParse(h, out int he) ? he : 480;
        if (options.TryGetValue("referer", out string? r) && !string.IsNullOrWhiteSpace(r))
            referer = r;

        var mainArgs = new CefMainArgs(args);
        var app = new MyTubeCefApp();
        int exitCode = CefRuntime.ExecuteProcess(mainArgs, app, IntPtr.Zero);
        if (exitCode >= 0)
            return exitCode;

        string dataDir = Path.Combine(Path.GetTempPath(), "SmartphoneMyTube", Environment.UserName);
        Directory.CreateDirectory(dataDir);

        var settings = new CefSettings
        {
            NoSandbox = true,
            WindowlessRenderingEnabled = true,
            MultiThreadedMessageLoop = false,
            CachePath = Path.Combine(dataDir, "cache"),
            RootCachePath = dataDir,
        };

        try
        {
            CefRuntime.Initialize(mainArgs, settings, app, IntPtr.Zero);
            using var writer = new FrameFileWriter(frameFile, width, height);
            var renderHandler = new BrowserRenderHandler(width, height, writer);
            var lifeSpan = new BrowserLifeSpanHandler(
                created =>
                {
                    browser = created;
                    WriteStatus("ready", "Browser ready. Paste a YouTube URL and press Play.");
                },
                () => browser = null);
            var client = new BrowserClient(renderHandler, lifeSpan);

            CefWindowInfo windowInfo = CefWindowInfo.Create();
            windowInfo.SetAsWindowless(IntPtr.Zero, false);
            var browserSettings = new CefBrowserSettings { WindowlessFrameRate = 30 };
            CefBrowserHost.CreateBrowser(windowInfo, client, browserSettings, "about:blank");

            Thread inputThread = new(ReadCommands) { IsBackground = true, Name = "MyTube command reader" };
            inputThread.Start();

            while (running)
            {
                CefRuntime.DoMessageLoopWork();
                while (Commands.TryDequeue(out BrowserHostCommand? command))
                    HandleCommand(command);
                Thread.Sleep(8);
            }

            if (browser != null)
                browser.GetHost().CloseBrowser(true);

            for (int i = 0; i < 20; i++)
            {
                CefRuntime.DoMessageLoopWork();
                Thread.Sleep(5);
            }

            CefRuntime.Shutdown();
            return 0;
        }
        catch (Exception ex)
        {
            WriteStatus("error", ex.ToString());
            try { CefRuntime.Shutdown(); } catch { }
            return 1;
        }
    }

    private static void ReadCommands()
    {
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                BrowserHostCommand? command = JsonSerializer.Deserialize<BrowserHostCommand>(line);
                if (command != null)
                    Commands.Enqueue(command);
            }
            catch (Exception ex)
            {
                WriteStatus("error", $"Invalid command: {ex.Message}");
            }
        }
    }

    private static void HandleCommand(BrowserHostCommand command)
    {
        switch (command.Type)
        {
            case "load" when !string.IsNullOrWhiteSpace(command.VideoId):
                LoadVideo(command.VideoId);
                break;
            case "mouseClick":
                SendMouseClick(command.X, command.Y);
                break;
            case "shutdown":
                running = false;
                break;
        }
    }

    private static void LoadVideo(string videoId)
    {
        CefBrowser? current = browser;
        if (current == null)
        {
            WriteStatus("error", "Browser is not ready yet.");
            return;
        }

        string encodedReferer = Uri.EscapeDataString(referer);
        string url = $"https://www.youtube.com/embed/{Uri.EscapeDataString(videoId)}" +
                     $"?autoplay=1&controls=1&playsinline=1&enablejsapi=1&rel=0&origin={encodedReferer}&widget_referrer={encodedReferer}";

        try
        {
            CefRequest request = CefRequest.Create();
            request.Url = url;
            request.Method = "GET";
            request.SetReferrer(referer, CefReferrerPolicy.Default);
            current.GetMainFrame().LoadRequest(request);
            WriteStatus("loading", $"Loading YouTube video {videoId}...");
        }
        catch (Exception ex)
        {
            WriteStatus("error", $"Unable to load video: {ex.Message}");
        }
    }

    private static void SendMouseClick(int x, int y)
    {
        CefBrowser? current = browser;
        if (current == null)
            return;

        x = Math.Clamp(x, 0, 853);
        y = Math.Clamp(y, 0, 479);
        var mouse = new CefMouseEvent(x, y, CefEventFlags.None);
        CefBrowserHost host = current.GetHost();
        host.SendMouseMoveEvent(mouse, mouseLeave: false);
        host.SendMouseClickEvent(mouse, CefMouseButtonType.Left, mouseUp: false, clickCount: 1);
        host.SendMouseClickEvent(mouse, CefMouseButtonType.Left, mouseUp: true, clickCount: 1);
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;
            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            result[key] = value;
        }
        return result;
    }

    private static void WriteStatus(string type, string message)
    {
        Console.WriteLine(JsonSerializer.Serialize(new BrowserHostStatus { Type = type, Message = message }));
        Console.Out.Flush();
    }
}
