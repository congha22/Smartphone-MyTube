using Xilium.CefGlue;

namespace SmartphoneMyTube.BrowserHost;

internal sealed class BrowserClient : CefClient
{
    private readonly CefRenderHandler renderHandler;
    private readonly CefLifeSpanHandler lifeSpanHandler;

    public BrowserClient(CefRenderHandler renderHandler, CefLifeSpanHandler lifeSpanHandler)
    {
        this.renderHandler = renderHandler;
        this.lifeSpanHandler = lifeSpanHandler;
    }

    protected override CefRenderHandler GetRenderHandler() => renderHandler;
    protected override CefLifeSpanHandler GetLifeSpanHandler() => lifeSpanHandler;
}
