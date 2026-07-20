using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SkinnedSpike.Skinning;

namespace SkinnedSpike;

public class SpikeGame : Game
{
    private readonly string _glbPath;
    private GraphicsDeviceManager _graphics;
    private SkinnedModel _model;
    private SkinnedModelInstance _instance;

    private float _orbitAngle;
    private float _fpsAccum;
    private int _fpsFrames;
    private float _fps;

    public SpikeGame(string glbPath)
    {
        _glbPath = glbPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _model = SkinnedModel.Load(GraphicsDevice, _glbPath);
        _instance = new SkinnedModelInstance(_model);

        string clipEnv = Environment.GetEnvironmentVariable("SPIKE_CLIP");
        if (!string.IsNullOrEmpty(clipEnv)) _instance.Play(clipEnv);

        Console.WriteLine($"Loaded '{_glbPath}':");
        Console.WriteLine($"  Nodes: {_model.NodeCount}, Joints: {_model.JointCount}, Mesh parts: {_model.Parts.Count}");
        Console.WriteLine($"  Bind bounds: {_model.BindPoseBounds.Min} .. {_model.BindPoseBounds.Max}");
        Console.WriteLine($"  Clips: {string.Join(", ", _model.Clips.ConvertAll(c => $"{c.Name} ({c.Duration:F2}s, {c.Channels.Count}ch)"))}");
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape)) Exit();

        if (_model.Clips.Count > 0 && kb.IsKeyDown(Keys.D1)) _instance.Play(_model.Clips[0].Name);
        if (_model.Clips.Count > 1 && kb.IsKeyDown(Keys.D2)) _instance.Play(_model.Clips[1].Name);
        if (_model.Clips.Count > 2 && kb.IsKeyDown(Keys.D3)) _instance.Play(_model.Clips[2].Name);

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _instance.Update(dt);
        _orbitAngle += dt * 0.3f; // slow orbit to inspect the model from all sides

        // FPS in window title
        _fpsAccum += dt; _fpsFrames++;
        if (_fpsAccum >= 0.5f)
        {
            _fps = _fpsFrames / _fpsAccum;
            _fpsAccum = 0; _fpsFrames = 0;
            Window.Title = $"SkinnedSpike | {_fps:F0} fps | clip: {_instance.CurrentClip?.Name ?? "(bind pose)"} | keys 1/2/3 switch, Esc quit";
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(40, 44, 52));

        // Fit camera to the model's bind-pose bounding sphere (model scale is unknown/generic).
        var bounds = _model.BindPoseBounds;
        Vector3 center = (bounds.Min + bounds.Max) / 2f;
        float radius = Math.Max(0.001f, (bounds.Max - bounds.Min).Length() / 2f);

        float camDist = radius * 2.2f;
        var camPos = center + new Vector3(
            (float)Math.Sin(_orbitAngle) * camDist,
            radius * 0.6f,
            (float)Math.Cos(_orbitAngle) * camDist);

        Matrix view = Matrix.CreateLookAt(camPos, center, Vector3.Up);
        Matrix projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4,
            GraphicsDevice.Viewport.AspectRatio,
            radius * 0.05f,
            radius * 20f);

        _instance.Draw(GraphicsDevice, Matrix.Identity, view, projection);

        // Headless verification: SPIKE_SHOT=/path/out.png SPIKE_CLIP=<name> dotnet run
        // renders offscreen at frame 120, saves a PNG, and exits.
        string shotPath = Environment.GetEnvironmentVariable("SPIKE_SHOT");
        if (!string.IsNullOrEmpty(shotPath) && ++_shotFrame == 120)
        {
            int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var rt = new RenderTarget2D(GraphicsDevice, w, h, false,
                SurfaceFormat.Color, DepthFormat.Depth24);
            GraphicsDevice.SetRenderTarget(rt);
            GraphicsDevice.Clear(new Color(40, 44, 52));
            _instance.Draw(GraphicsDevice, Matrix.Identity, view, projection);
            GraphicsDevice.SetRenderTarget(null);

            using (var fs = File.Create(shotPath))
                rt.SaveAsPng(fs, w, h);
            rt.Dispose();
            Console.WriteLine($"Screenshot saved to {shotPath}");
            Exit();
        }

        base.Draw(gameTime);
    }

    private int _shotFrame;
}
