namespace SmartphoneMyTube.Playback;

internal sealed class PlaybackFrame
{
    public PlaybackFrame(int width, int height, byte[] bgraPixels, long sequence)
    {
        Width = width;
        Height = height;
        BgraPixels = bgraPixels;
        Sequence = sequence;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] BgraPixels { get; }
    public long Sequence { get; }
}
