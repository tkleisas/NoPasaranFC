using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Screens;
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
        
        // Update TouchUI
        Gameplay.TouchUI.Instance.UpdateScreenSize(width, height);
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
        
        // Try to load existing championship or create new one
        _championship = _database.LoadChampionship();
        
        if (_championship.Teams.Count == 0)
        {
            _championship = ChampionshipInitializer.CreateNewChampionship();
            _database.SaveChampionship(_championship);
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
        GraphicsDevice.Clear(new Color(34, 139, 34));

        _spriteBatch.Begin();
        _screenManager.Draw(_spriteBatch, _font);
        
        // Draw touch UI overlay on top of everything
        Gameplay.TouchUI.Instance.Draw(_spriteBatch, _font);
        _spriteBatch.End();

        base.Draw(gameTime);
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
}
