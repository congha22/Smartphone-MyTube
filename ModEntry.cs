using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmartphoneMyTube.Data;
using SmartphoneMyTube.Playback;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SmartphoneMyTube;

internal sealed class ModEntry : Mod
{
    private const string SmartphoneModId = "d5a1lamdtd.Smartphone";
    private const string AppId = "mytube";

    private ISmartPhoneApi? smartphoneApi;
    private IPlaybackBackend? playback;
    private MyTubeScreen? activeScreen;
    private Texture2D? appIcon;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        smartphoneApi = Helper.ModRegistry.GetApi<ISmartPhoneApi>(SmartphoneModId);
        if (smartphoneApi == null)
        {
            Monitor.Log("Smartphone API not found. MyTube was not registered.", LogLevel.Warn);
            return;
        }

        playback = CreatePlaybackBackend();
        appIcon = TryLoadTexture("assets/default/1x1.png") ?? CreateSolidTexture(Color.White);

        bool registered = smartphoneApi.RegisterPhoneApp(
            ownerModId: ModManifest.UniqueID,
            appId: AppId,
            displayName: Helper.Translation.Get("app.name"),
            onClick: OpenApp,
            closePhoneOnLaunch: true,
            supportedSizes: new[] { AppSize.Size1x1 },
            themedIconTextures: new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = appIcon
            });

        if (!registered)
        {
            Monitor.Log("Failed to register MyTube app.", LogLevel.Warn);
            return;
        }

        smartphoneApi.RegisterPassiveHudCallback(
            ownerModId: ModManifest.UniqueID,
            appId: AppId,
            onDrawHudScreen: DrawPinnedHud,
            onUpdateHudScreen: UpdatePinnedHud,
            landscape: true);
    }

    private void OpenApp()
    {
        if (!Context.IsWorldReady || smartphoneApi == null)
            return;

        playback ??= CreatePlaybackBackend();

        bool resume = smartphoneApi.IsHudPinned()
            && string.Equals(smartphoneApi.GetPinnedAppId(), $"{ModManifest.UniqueID}::{AppId}", StringComparison.OrdinalIgnoreCase);

        if (!resume || activeScreen == null)
        {
            activeScreen?.Dispose();
            activeScreen = new MyTubeScreen(
                api: smartphoneApi,
                playback: playback,
                helper: Helper,
                onBack: () => smartphoneApi.OpenPhoneHomeScreen());
        }

        Game1.activeClickableMenu = activeScreen;
    }

    private void DrawPinnedHud(SpriteBatch b, Rectangle dest)
    {
        if (activeScreen != null)
            activeScreen.DrawScreenContent(b, dest, pinned: true);
        else
            b.Draw(Game1.staminaRect, dest, Color.Black);
    }

    private void UpdatePinnedHud(GameTime time)
    {
        if (Game1.activeClickableMenu == activeScreen)
            return;
        activeScreen?.UpdatePlaybackOnly(time);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        activeScreen?.Dispose();
        activeScreen = null;
        playback?.Dispose();
        playback = null;
    }

    private IPlaybackBackend CreatePlaybackBackend()
        => new BrowserHostBackend(Helper.DirectoryPath, Monitor);

    private Texture2D? TryLoadTexture(string path)
    {
        try { return Helper.ModContent.Load<Texture2D>(path); }
        catch (Exception ex)
        {
            Monitor.Log($"Could not load '{path}': {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        var texture = new Texture2D(Game1.graphics.GraphicsDevice, 64, 64);
        var data = new Color[64 * 64];
        Array.Fill(data, color);
        texture.SetData(data);
        return texture;
    }
}
