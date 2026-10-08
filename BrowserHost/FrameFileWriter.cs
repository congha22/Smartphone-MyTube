using System.IO;
using System.IO.MemoryMappedFiles;
using SmartphoneMyTube.Shared;

namespace SmartphoneMyTube.BrowserHost;

internal sealed class FrameFileWriter : IDisposable
{
    private readonly MemoryMappedFile map;
    private readonly MemoryMappedViewAccessor view;
    private readonly int maxBytes;
    private long sequence;

    public FrameFileWriter(string path, int maxWidth, int maxHeight)
    {
        maxBytes = maxWidth * maxHeight * 4;
        long capacity = FrameBufferLayout.HeaderSize + maxBytes;
        map = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, capacity, MemoryMappedFileAccess.ReadWrite);
        view = map.CreateViewAccessor(0, capacity, MemoryMappedFileAccess.ReadWrite);
        view.Write(FrameBufferLayout.MagicOffset, FrameBufferLayout.Magic);
        view.Write(FrameBufferLayout.VersionOffset, FrameBufferLayout.Version);
    }

    public void WriteFrame(byte[] bgraPixels, int width, int height)
    {
        int length = checked(width * height * 4);
        if (length <= 0 || length > maxBytes || bgraPixels.Length < length)
            return;

        view.WriteArray(FrameBufferLayout.PixelDataOffset, bgraPixels, 0, length);
        view.Write(FrameBufferLayout.WidthOffset, width);
        view.Write(FrameBufferLayout.HeightOffset, height);
        view.Write(FrameBufferLayout.DataLengthOffset, length);
        view.Write(FrameBufferLayout.SequenceOffset, ++sequence);
    }

    public void Dispose()
    {
        view.Dispose();
        map.Dispose();
    }
}
