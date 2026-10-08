using Xilium.CefGlue;

namespace SmartphoneMyTube.BrowserHost;

internal sealed class MyTubeCefApp : CefApp
{
    protected override void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
    {
        commandLine.AppendSwitch("autoplay-policy", "no-user-gesture-required");
    }
}
