using System.Text.Json.Serialization;

namespace SmartphoneMyTube.Shared;

public static class FrameBufferLayout
{
    public const int HeaderSize = 64;
    public const int Magic = 0x4D595455; // "MYTU"
    public const int Version = 1;

    public const int MagicOffset = 0;
    public const int VersionOffset = 4;
    public const int WidthOffset = 8;
    public const int HeightOffset = 12;
    public const int DataLengthOffset = 16;
    public const int SequenceOffset = 24;
    public const int PixelDataOffset = HeaderSize;
}

public sealed class BrowserHostCommand
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("videoId")]
    public string? VideoId { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}

public sealed class BrowserHostStatus
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
