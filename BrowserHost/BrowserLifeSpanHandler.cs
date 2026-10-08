using Xilium.CefGlue;

namespace SmartphoneMyTube.BrowserHost;

internal sealed class BrowserLifeSpanHandler : CefLifeSpanHandler
{
    private readonly Action<CefBrowser> onCreated;
    private readonly Action onClosed;

    public BrowserLifeSpanHandler(Action<CefBrowser> onCreated, Action onClosed)
    {
        this.onCreated = onCreated;
        this.onClosed = onClosed;
    }

    protected override void OnAfterCreated(CefBrowser browser) => onCreated(browser);
    protected override void OnBeforeClose(CefBrowser browser) => onClosed();
}
