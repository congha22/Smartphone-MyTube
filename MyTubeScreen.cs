using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SmartphoneMyTube.Data;
using SmartphoneMyTube.Playback;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TextCopy;

namespace SmartphoneMyTube;

internal sealed class MyTubeScreen : IClickableMenu, IDisposable
{
    private const int BrowserWidth = 854;
    private const int BrowserHeight = 480;

    private readonly ISmartPhoneApi api;
    private readonly IPlaybackBackend playback;
    private readonly IModHelper helper;
    private readonly Action onBack;

    private Texture2D? videoTexture;
    private byte[]? rgbaBuffer;
    private string pastedUrl = string.Empty;
    private string status = "Paste a YouTube URL to begin.";

    private float phoneUiScale;
    private int phoneFrameWidth;
    private int phoneFrameHeight;
    private int contentOffsetX;
    private int contentOffsetY;
    private Texture2D? frameTexture;
    private Texture2D? backgroundTexture;

    private Rectangle pasteButton;
    private Rectangle loadButton;
    private Rectangle pinButton;
    private Rectangle backButton;
    private Rectangle currentVideoRect;

    public MyTubeScreen(ISmartPhoneApi api, IPlaybackBackend playback, IModHelper helper, Action onBack)
    {
        this.api = api;
        this.playback = playback;
        this.helper = helper;
        this.onBack = onBack;

        RefreshLayout(initial: true);
    }

    private Rectangle LandscapeContentRect
    {
        get
        {
            int contentWidth = backgroundTexture != null
                ? (int)Math.Round(backgroundTexture.Width * phoneUiScale)
                : Math.Max(1, phoneFrameWidth - contentOffsetX * 2);
            int contentHeight = backgroundTexture != null
                ? (int)Math.Round(backgroundTexture.Height * phoneUiScale)
                : Math.Max(1, phoneFrameHeight - contentOffsetY * 2);

            return new Rectangle(
                xPositionOnScreen + contentOffsetY,
                yPositionOnScreen + phoneFrameWidth - contentOffsetX - contentWidth,
                contentHeight,
                contentWidth);
        }
    }

    private void RefreshLayout(bool initial = false)
    {
        int oldW = phoneFrameWidth;
        int oldH = phoneFrameHeight;
        int oldCenterX = xPositionOnScreen + oldH / 2;
        int oldCenterY = yPositionOnScreen + oldW / 2;

        phoneUiScale = api.GetPhoneUiScale();
        phoneFrameWidth = api.GetPhoneFrameWidth();
        phoneFrameHeight = api.GetPhoneFrameHeight();
        (contentOffsetX, contentOffsetY) = api.GetPhoneContentOffset();
        frameTexture = api.GetPhoneFrameTexture();
        backgroundTexture = api.GetPhoneBackgroundTexture();

        width = phoneFrameHeight;
        height = phoneFrameWidth;

        if (initial)
        {
            var (px, py) = api.GetPhonePosition();
            xPositionOnScreen = px + (phoneFrameWidth - phoneFrameHeight) / 2;
            yPositionOnScreen = py + (phoneFrameHeight - phoneFrameWidth) / 2;
        }
        else
        {
            xPositionOnScreen = oldCenterX - phoneFrameHeight / 2;
            yPositionOnScreen = oldCenterY - phoneFrameWidth / 2;
            SyncPortraitPosition();
        }

        RebuildButtons();
    }

    private void RebuildButtons()
    {
        Rectangle c = LandscapeContentRect;
        int gap = Math.Max(4, (int)Math.Round(6 * phoneUiScale));
        int buttonH = Math.Max(24, (int)Math.Round(34 * phoneUiScale));
        int y = c.Bottom - buttonH - gap;
        int available = c.Width - gap * 5;
        int w = Math.Max(40, available / 4);

        pasteButton = new Rectangle(c.X + gap, y, w, buttonH);
        loadButton = new Rectangle(pasteButton.Right + gap, y, w, buttonH);
        pinButton = new Rectangle(loadButton.Right + gap, y, w, buttonH);
        backButton = new Rectangle(pinButton.Right + gap, y, w, buttonH);
    }

    private void SyncPortraitPosition()
    {
        int px = xPositionOnScreen - (phoneFrameWidth - phoneFrameHeight) / 2;
        int py = yPositionOnScreen - (phoneFrameHeight - phoneFrameWidth) / 2;
        api.SetPhonePosition(px, py);
    }

    public override void update(GameTime time)
    {
        float currentScale = api.GetPhoneUiScale();
        if (Math.Abs(currentScale - phoneUiScale) > 0.001f)
            RefreshLayout();

        base.update(time);
        UpdatePlaybackOnly(time);
    }

    public void UpdatePlaybackOnly(GameTime time)
    {
        playback.Update();
        status = playback.Status;

        if (playback.TryGetLatestFrame(out PlaybackFrame? frame) && frame != null)
            UploadFrame(frame);
    }

    private void UploadFrame(PlaybackFrame frame)
    {
        if (videoTexture == null || videoTexture.IsDisposed || videoTexture.Width != frame.Width || videoTexture.Height != frame.Height)
        {
            videoTexture?.Dispose();
            videoTexture = new Texture2D(Game1.graphics.GraphicsDevice, frame.Width, frame.Height, false, SurfaceFormat.Color);
            rgbaBuffer = new byte[frame.BgraPixels.Length];
        }

        rgbaBuffer ??= new byte[frame.BgraPixels.Length];
        byte[] src = frame.BgraPixels;
        byte[] dst = rgbaBuffer;
        for (int i = 0; i < src.Length; i += 4)
        {
            dst[i] = src[i + 2];
            dst[i + 1] = src[i + 1];
            dst[i + 2] = src[i];
            dst[i + 3] = src[i + 3];
        }
        videoTexture.SetData(dst);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (api.HandlePhoneSizeButtonsClick(x, y, xPositionOnScreen, yPositionOnScreen))
            return;

        if (pasteButton.Contains(x, y))
        {
            try
            {
                pastedUrl = ClipboardService.GetText()?.Trim() ?? string.Empty;
                status = string.IsNullOrWhiteSpace(pastedUrl)
                    ? helper.Translation.Get("ui.no_url")
                    : pastedUrl;
            }
            catch (Exception ex)
            {
                status = $"Clipboard error: {ex.Message}";
            }
            Game1.playSound("smallSelect");
            return;
        }

        if (loadButton.Contains(x, y))
        {
            if (YouTubeUrlParser.TryGetVideoId(pastedUrl, out string videoId))
            {
                playback.LoadVideo(videoId);
                status = helper.Translation.Get("ui.loading");
            }
            else
            {
                status = helper.Translation.Get("ui.invalid_url");
                Game1.playSound("cancel");
            }
            return;
        }

        if (pinButton.Contains(x, y))
        {
            api.SetHudPinned(true);
            status = helper.Translation.Get("ui.pinned");
            Game1.activeClickableMenu = null;
            return;
        }

        if (backButton.Contains(x, y))
        {
            if (api.IsHudPinned())
                api.SetHudPinned(false);
            onBack();
            return;
        }

        if (currentVideoRect.Contains(x, y) && currentVideoRect.Width > 0 && currentVideoRect.Height > 0)
        {
            int bx = Math.Clamp((x - currentVideoRect.X) * BrowserWidth / currentVideoRect.Width, 0, BrowserWidth - 1);
            int by = Math.Clamp((y - currentVideoRect.Y) * BrowserHeight / currentVideoRect.Height, 0, BrowserHeight - 1);
            playback.SendMouseClick(bx, by);
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            if (api.IsHudPinned())
                api.SetHudPinned(false);
            onBack();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.55f);

        Rectangle content = LandscapeContentRect;
        DrawScreenContent(b, content, pinned: false);

        if (frameTexture != null && !frameTexture.IsDisposed)
        {
            float scale = phoneFrameWidth / (float)Math.Max(1, frameTexture.Width);
            b.Draw(frameTexture,
                new Vector2(xPositionOnScreen, yPositionOnScreen + phoneFrameWidth),
                null,
                Color.White,
                -MathHelper.PiOver2,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }

        api.DrawPhoneSizeButtons(b, xPositionOnScreen, yPositionOnScreen, landscape: true);
        drawMouse(b);
    }

    public void DrawScreenContent(SpriteBatch b, Rectangle dest, bool pinned)
    {
        b.Draw(Game1.staminaRect, dest, new Color(12, 12, 14));

        int padding = pinned ? 8 : Math.Max(6, (int)Math.Round(8 * phoneUiScale));
        int footer = pinned ? 24 : Math.Max(42, (int)Math.Round(48 * phoneUiScale));
        Rectangle playerArea = new(dest.X + padding, dest.Y + padding,
            Math.Max(1, dest.Width - padding * 2), Math.Max(1, dest.Height - footer - padding * 2));
        currentVideoRect = Fit16By9(playerArea);

        if (videoTexture != null && !videoTexture.IsDisposed)
            b.Draw(videoTexture, currentVideoRect, Color.White);
        else
        {
            b.Draw(Game1.staminaRect, currentVideoRect, Color.Black);
            DrawCenteredText(b, "MyTube", currentVideoRect, Color.White * 0.85f, 1f);
        }

        if (pinned)
        {
            string shortStatus = status.Length > 85 ? status[..82] + "..." : status;
            b.DrawString(Game1.smallFont, shortStatus,
                new Vector2(dest.X + padding, dest.Bottom - 22), Color.White * 0.8f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
            return;
        }

        DrawButton(b, pasteButton, helper.Translation.Get("ui.paste"));
        DrawButton(b, loadButton, helper.Translation.Get("ui.load"));
        DrawButton(b, pinButton, helper.Translation.Get("ui.pin"));
        DrawButton(b, backButton, helper.Translation.Get("ui.back"));

        string display = string.IsNullOrWhiteSpace(pastedUrl) ? status : pastedUrl;
        if (display.Length > 96) display = display[..93] + "...";
        b.DrawString(Game1.smallFont, display,
            new Vector2(dest.X + padding, Math.Max(dest.Y + padding, pasteButton.Y - 20)),
            Color.White * 0.85f, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private static Rectangle Fit16By9(Rectangle bounds)
    {
        int width = bounds.Width;
        int height = (int)Math.Round(width * 9d / 16d);
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = (int)Math.Round(height * 16d / 9d);
        }
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    private static void DrawButton(SpriteBatch b, Rectangle rect, string text)
    {
        b.Draw(Game1.staminaRect, rect, new Color(45, 48, 56));
        b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.White * 0.35f);
        b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.White * 0.35f);
        DrawCenteredText(b, text, rect, Color.White, 0.65f);
    }

    private static void DrawCenteredText(SpriteBatch b, string text, Rectangle rect, Color color, float scale)
    {
        Vector2 size = Game1.smallFont.MeasureString(text) * scale;
        Vector2 pos = new(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
        b.DrawString(Game1.smallFont, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    public void Dispose()
    {
        videoTexture?.Dispose();
        videoTexture = null;
    }
}
