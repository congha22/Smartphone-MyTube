using System.Runtime.InteropServices;
using Xilium.CefGlue;

namespace SmartphoneMyTube.BrowserHost;

internal sealed class BrowserRenderHandler : CefRenderHandler
{
    private readonly int width;
    private readonly int height;
    private readonly FrameFileWriter writer;
    private byte[] buffer;

    public BrowserRenderHandler(int width, int height, FrameFileWriter writer)
    {
        this.width = width;
        this.height = height;
        this.writer = writer;
        buffer = new byte[width * height * 4];
    }

    protected override CefAccessibilityHandler? GetAccessibilityHandler() => null;

    protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
    {
        screenInfo.DeviceScaleFactor = 1f;
        screenInfo.Depth = 24;
        screenInfo.DepthPerComponent = 8;
        screenInfo.IsMonochrome = false;
        screenInfo.Rectangle = new CefRectangle(0, 0, width, height);
        screenInfo.AvailableRectangle = new CefRectangle(0, 0, width, height);
        return true;
    }

    protected override void GetViewRect(CefBrowser browser, out CefRectangle rect)
        => rect = new CefRectangle(0, 0, width, height);

    protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, nint source, int frameWidth, int frameHeight)
    {
        if (type != CefPaintElementType.View)
            return;

        int bytes = checked(frameWidth * frameHeight * 4);
        if (buffer.Length != bytes)
            buffer = new byte[bytes];
        Marshal.Copy(source, buffer, 0, bytes);
        writer.WriteFrame(buffer, frameWidth, frameHeight);
    }

    protected override void OnAcceleratedPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, nint sharedHandle) { }
    protected override void OnImeCompositionRangeChanged(CefBrowser browser, CefRange selectedRange, CefRectangle[] characterBounds) { }
    protected override void OnPopupSize(CefBrowser browser, CefRectangle rect) { }
    protected override void OnScrollOffsetChanged(CefBrowser browser, double x, double y) { }
}
