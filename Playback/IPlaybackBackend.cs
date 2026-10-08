using System;

namespace SmartphoneMyTube.Playback;

internal interface IPlaybackBackend : IDisposable
{
    bool IsAvailable { get; }
    string Status { get; }

    void LoadVideo(string videoId);
    void Update();
    bool TryGetLatestFrame(out PlaybackFrame? frame);
    void SendMouseClick(int x, int y);
}
