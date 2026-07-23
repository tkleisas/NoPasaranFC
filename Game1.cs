using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Screens;
using NoPasaranFC.Debugging;
using Microsoft.Xna.Framework.Content;

namespace NoPasaranFC;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private SpriteFont _font;
    private ScreenManager _screenManager;
    private ContentManager _contentManager;
    private Championship _championship;
    private DatabaseManager _database;

    // Debug console (enabled via NOPASARAN_DEBUG=1)
    private DebugServer _debugServer;
    private ScreenCapture _screenCapture;
    private float _fpsAccum;
    private int _fpsFrames;
    private float _fps;
    
    // Resolution settings
    public static int ScreenWidth { get; private set; } = 1280;
    public static int ScreenHeight { get; private set; } = 720;
    public static bool IsFullscreen { get; private set; } = false;
    
    // UI Scale for high-DPI displays (Android)
    public static float UIScale { get; private set; } = 1f;
    
    // Available resolutions
    private static readonly Point[] AvailableResolutions = new[]
    {
        new Point(800, 600),
        new Point(1024, 768),
        new Point(1280, 720),
        new Point(1366, 768),
        new Point(1600, 900),
        new Point(1920, 1080)
    };

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _contentManager = Content;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        
#if ANDROID
        // On Android, use native resolution and fullscreen
        _graphics.IsFullScreen = true;
        _graphics.SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
        // Set initial resolution for desktop
        ApplyResolution(ScreenWidth, ScreenHeight, IsFullscreen);
#endif
    }
    
    public void ApplyResolution(int width, int height, bool fullscreen)
    {
        ScreenWidth = width;
        ScreenHeight = height;
        IsFullscreen = fullscreen;
        
        // Calculate UI scale
        UIScale = Math.Max(1f, height / 720f);
        UIScale = Math.Clamp(UIScale, 1f, 3f);
        
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.IsFullScreen = fullscreen;
        _graphics.ApplyChanges();
        
        // Reconcile with the ACTUAL backbuffer: in fullscreen, mode-setting may
        // silently fall back to the desktop resolution (e.g. Wayland/XWayland),
        // leaving the game thinking it's 1920x1080 while rendering 2560x1440 —
        // which makes screens tile/position content for the wrong size.
        SyncScreenSizeToBackbuffer();
        
        // Update TouchUI
        Gameplay.TouchUI.Instance.UpdateScreenSize(ScreenWidth, ScreenHeight);
    }
    
    public static Point[] GetAvailableResolutions()
    {
        return AvailableResolutions;
    }

    protected override void Initialize()
    {
        _database = new DatabaseManager();
        _screenManager = new ScreenManager(this);
        
        // Load settings from database
        var settings = _database.LoadSettings();
        GameSettings.SetInstance(settings);
        
#if ANDROID
        // On Android, get actual screen resolution after graphics device is ready
        ScreenWidth = GraphicsDevice.Adapter.CurrentDisplayMode.Width;
        ScreenHeight = GraphicsDevice.Adapter.CurrentDisplayMode.Height;
        
        // Calculate UI scale for Android
        UIScale = Math.Max(1f, ScreenHeight / 720f);
        UIScale = Math.Clamp(UIScale, 1f, 3f);
        
        _graphics.PreferredBackBufferWidth = ScreenWidth;
        _graphics.PreferredBackBufferHeight = ScreenHeight;
        _graphics.ApplyChanges();
        
        // Update TouchUI
        Gameplay.TouchUI.Instance.UpdateScreenSize(ScreenWidth, ScreenHeight);
#else
        // Apply video settings on desktop
        ApplyResolution(settings.ResolutionWidth, settings.ResolutionHeight, settings.IsFullscreen);
        _graphics.SynchronizeWithVerticalRetrace = settings.VSync;
#endif
        
        // Try to load existing championship; if none exists, leave it empty so the
        // menu can launch the championship selection flow.
        _championship = _database.LoadChampionship();

        // Keep logical screen size in sync with the REAL backbuffer: in fullscreen
        // the compositor may resize the window to the desktop resolution AFTER
        // startup (Wayland/XWayland), so a one-time check is not enough.
        Window.ClientSizeChanged += (s, e) => SyncScreenSizeToBackbuffer();

        // Debug TCP console: NOPASARAN_DEBUG=1 [NOPASARAN_DEBUG_PORT=7777]
        if (Environment.GetEnvironmentVariable("NOPASARAN_DEBUG") == "1")
        {
            int port = 7777;
            string portEnv = Environment.GetEnvironmentVariable("NOPASARAN_DEBUG_PORT");
            if (!string.IsNullOrEmpty(portEnv)) int.TryParse(portEnv, out port);
            _screenCapture = new ScreenCapture();
            _debugServer = new DebugServer();
            _debugServer.Start(port);
            System.Diagnostics.Debug.WriteLine($"Debug console on 127.0.0.1:{port}");
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Font");
        
        // Warmup font to prevent first-frame stuttering (especially in debugger)
        _font.MeasureString("SETTINGS");
        _font.MeasureString("▲ MORE");
        _font.MeasureString("ΕΠΙΛΟΓΕΣ");
        
        // Initialize touch UI
        Gameplay.TouchUI.Instance.Initialize(GraphicsDevice);
        
        // Initialize audio system
        AudioManager.Instance.Initialize(Content);
        AudioManager.Instance.MusicVolume = GameSettings.Instance.MusicVolume;
        AudioManager.Instance.SfxVolume = GameSettings.Instance.SfxVolume;
        AudioManager.Instance.MusicEnabled = GameSettings.Instance.MusicEnabled;
        AudioManager.Instance.SfxEnabled = GameSettings.Instance.SfxEnabled;
        
        // Start menu music
        AudioManager.Instance.PlayMusic("menu_music");
        
        // Start with menu screen
        var menuScreen = new MenuScreen(_championship, _database, _screenManager, _contentManager, GraphicsDevice);
        _screenManager.PushScreen(menuScreen);
    }

    protected override void Update(GameTime gameTime)
    {
        // Debug input injection frame boundary + queued console commands
        DebugInput.BeginFrame();
        ProcessDebugCommands();

        // FPS tracking for the debug console
        float frameDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _fpsAccum += frameDt; _fpsFrames++;
        if (_fpsAccum >= 0.5f) { _fps = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }

        // Update touch UI first
        Gameplay.TouchUI.Instance.Update(gameTime);
        
        _screenManager.Update(gameTime);
        
        // Update audio manager
        AudioManager.Instance.Update();
        
        // Make sure MenuScreen has graphics device reference
        if (_screenManager.CurrentScreen is MenuScreen menuScreen)
        {
            menuScreen.SetGraphicsDevice(GraphicsDevice);
            
            if (menuScreen.ShouldExit)
            {
                ExitGame();
            }
        }
        
        // Set graphics device for match screen if needed (fallback)
        if (_screenManager.CurrentScreen is MatchScreen matchScreen)
        {
            matchScreen.SetGraphicsDevice(GraphicsDevice);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // When a screenshot is queued, this frame renders into a capture target.
        var captureTarget = _screenCapture?.BeginFrame(GraphicsDevice);

        GraphicsDevice.Clear(new Color(34, 139, 34));

        _spriteBatch.Begin();
        _screenManager.Draw(_spriteBatch, _font);
        
        // Draw touch UI overlay on top of everything
        Gameplay.TouchUI.Instance.Draw(_spriteBatch, _font);
        _spriteBatch.End();

        if (captureTarget != null)
            _screenCapture.EndFrame(GraphicsDevice);

        base.Draw(gameTime);
    }

    /// <summary>
    /// Syncs the logical screen size (used by ALL screen layout code) to the
    /// real backbuffer size. In fullscreen the window may be resized by the
    /// compositor to the desktop resolution regardless of the preferred
    /// backbuffer size, so this must run on every client-size change.
    /// </summary>
    private void SyncScreenSizeToBackbuffer()
    {
        if (GraphicsDevice == null) return;
        var pp = GraphicsDevice.PresentationParameters;
        if (pp.BackBufferWidth <= 0 || pp.BackBufferHeight <= 0) return;
        if (pp.BackBufferWidth == ScreenWidth && pp.BackBufferHeight == ScreenHeight) return;
        
        ScreenWidth = pp.BackBufferWidth;
        ScreenHeight = pp.BackBufferHeight;
        UIScale = Math.Max(1f, ScreenHeight / 720f);
        UIScale = Math.Clamp(UIScale, 1f, 3f);
        Gameplay.TouchUI.Instance.UpdateScreenSize(ScreenWidth, ScreenHeight);
    }
    
    /// <summary>
    /// Platform-aware exit. On Android, properly finishes the activity.
    /// </summary>
    private void ExitGame()
    {
#if ANDROID
        NoPasaranFC.Android.Activity1.Instance?.ExitGame();
#else
        Exit();
#endif
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _debugServer?.Stop();
        base.OnExiting(sender, args);
    }

    // ---- Debug console command handling (game thread) ----

    private void ProcessDebugCommands()
    {
        if (_debugServer == null) return;
        while (_debugServer.TryDequeue(out var cmd))
        {
            string response;
            try { response = ExecuteDebugCommand(cmd); }
            catch (Exception ex) { response = "ERR " + ex.Message; }
            cmd.Respond(response);
        }
    }

    private string ExecuteDebugCommand(DebugServer.DebugCommand cmd)
    {
        var parts = cmd.Line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "ERR empty command";

        switch (parts[0].ToLowerInvariant())
        {
            case "shot":
            {
                if (parts.Length < 2) return "ERR usage: shot <path> [delayFrames]";
                int delay = parts.Length > 2 && int.TryParse(parts[2], out int d) ? d : 0;
                if (_screenCapture.CaptureInProgress) return "ERR capture already in progress";
                _screenCapture.Request(parts[1], delay,
                    (ok, path) => cmd.Respond(ok ? $"OK saved {path}" : $"ERR save failed {path}"));
                return null; // response sent when the frame is captured
            }
            case "key":
            case "down":
            case "up":
            {
                if (parts.Length < 2 || !Enum.TryParse<Keys>(parts[1], true, out var key))
                    return "ERR usage: key|down|up <XNA Keys enum name>";
                if (parts[0] == "key") DebugInput.InjectTap(key);
                else if (parts[0] == "down") DebugInput.InjectDown(key);
                else DebugInput.InjectUp(key);
                return "OK";
            }
            case "state":
                return "OK " + GetDebugState();
            case "match":
                return StartNextMatch();
            case "quit":
                ExitGame();
                return "OK";
            default:
                return "ERR unknown command (shot|key|down|up|state|match|quit)";
        }
    }

    private string GetDebugState()
    {
        string s = $"screen={_screenManager.CurrentScreen?.GetType().Name ?? "none"} fps={_fps:F0}";
        if (_screenManager.CurrentScreen is MatchScreen ms && ms.Engine != null)
        {
            var e = ms.Engine;
            s += $" match[state={e.CurrentState} time={e.MatchTime:F1} score={e.HomeScore}-{e.AwayScore} " +
                 $"ball=({e.BallPosition.X:F0},{e.BallPosition.Y:F0},h={e.BallHeight:F0})]";
            
            // Animation state census (helps diagnose stuck/oscillating animations)
            var counts = new Dictionary<string, int>();
            string controlled = null;
            foreach (var p in e.GetAllPlayers())
            {
                string a = p.CurrentAnimationState ?? "null";
                counts[a] = counts.TryGetValue(a, out int c) ? c + 1 : 1;
                if (p.IsControlled)
                    controlled = $"{p.Name}:{a}{(p.IsKnockedDown ? "(down)" : "")}";
            }
            s += " anims[" + string.Join(",", counts.Select(kv => $"{kv.Key}:{kv.Value}")) + "]";
            if (controlled != null) s += $" controlled={controlled}";
        }
        return s;
    }

    /// <summary>Jump straight to the next unplayed player match (same flow as the menu).</summary>
    private string StartNextMatch()
    {
        if (_championship == null) return "ERR no championship loaded";
        var playerTeam = _championship.Teams.Find(t => t.IsPlayerControlled);
        if (playerTeam == null) return "ERR no player-controlled team";
        var nextMatch = _championship.Matches.Find(m => !m.IsPlayed &&
            (m.HomeTeamId == playerTeam.Id || m.AwayTeamId == playerTeam.Id));
        if (nextMatch == null) return "ERR season complete";

        var lineupScreen = new LineupScreen(playerTeam, nextMatch, _championship,
            _database, _screenManager, _contentManager, GraphicsDevice);
        _screenManager.PushScreen(lineupScreen);
        return "OK lineup";
    }
}
