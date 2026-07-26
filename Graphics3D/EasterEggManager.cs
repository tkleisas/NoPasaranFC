using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Rolls and runs the per-match easter eggs:
    /// - fox     10%  (any venue)   - wanders the pitch all match
    /// - dog      5%  (any venue)   - tinted fox with a small tail; walks in, barks, leaves
    /// - crows   10%  (any venue)   - dark flock crossing the sky, craw.wav
    /// - seagulls 50% (Sfageia)     - white flock circling, seagulls.wav
    /// - tornado  5%  (rain only)   - whirlwind funnel on the apron, whirlwind.wav
    /// Events are scheduled at random times in the first ~2 minutes of play.
    /// </summary>
    public class EasterEggManager
    {
        private enum Kind { Fox, Dog, Crows, Seagulls, Tornado }

        private class Scheduled
        {
            public Kind Type;
            public float StartAt;
            public bool Started;
        }

        private readonly List<Scheduled> _schedule = new List<Scheduled>();
        private readonly Random _random;
        private readonly GraphicsDevice _device;
        private readonly SkinnedModel _foxModel;
        private readonly Venue _venue;
        private readonly bool _isRaining;

        private readonly List<FoxWalker> _walkers = new List<FoxWalker>();
        private FoxWalker _dog;
        private float _dogTimer;      // safety cap only - normally the dog leaves after a goal
        private int _lastScore = -1;  // score when the dog arrived (leaves when it changes)
        private BirdFlock _flock;
        private TornadoFx _tornado;
        private float _time;

        /// <summary>Which events were rolled this match (debug state).</summary>
        public string RolledSummary
        {
            get
            {
                if (_schedule.Count == 0) return "none";
                var parts = new List<string>();
                foreach (var s in _schedule)
                    parts.Add($"{s.Type}@{s.StartAt:F0}s{(s.Started ? "*" : "")}");
                return string.Join(",", parts);
            }
        }

        public EasterEggManager(GraphicsDevice device, SkinnedModel foxModel, Venue venue, bool isRaining, int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _device = device;
            _foxModel = foxModel;
            _venue = venue;
            _isRaining = isRaining;

            if (Roll(0.10)) Schedule(Kind.Fox);
            if (Roll(0.05)) Schedule(Kind.Dog);
            if (Roll(0.10)) Schedule(Kind.Crows);
            if (venue == Venue.Sfageia && Roll(0.50)) Schedule(Kind.Seagulls);
            if (isRaining && Roll(0.05)) Schedule(Kind.Tornado);
        }

        private bool Roll(double probability) => _random.NextDouble() < probability;

        private void Schedule(Kind type)
        {
            // Fox is visible from kickoff; the rest drop in over the first 2 minutes
            float startAt = type == Kind.Fox ? 0f : 8f + (float)_random.NextDouble() * 110f;
            _schedule.Add(new Scheduled { Type = type, StartAt = startAt });
        }

        /// <summary>Debug console: force an event now, regardless of the roll.</summary>
        public string Trigger(string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case "fox": StartEvent(Kind.Fox); return "OK fox";
                case "dog": StartEvent(Kind.Dog); return "OK dog";
                case "crows": StartEvent(Kind.Crows); return "OK crows";
                case "seagulls": StartEvent(Kind.Seagulls); return "OK seagulls";
                case "tornado": StartEvent(Kind.Tornado); return "OK tornado";
                default: return "ERR usage: easter <fox|dog|crows|seagulls|tornado>";
            }
        }

        private void StartEvent(Kind type)
        {
            switch (type)
            {
                case Kind.Fox:
                    if (_foxModel != null)
                        _walkers.Add(new FoxWalker(_foxModel));
                    break;
                case Kind.Dog:
                {
                    // Purpose-built Dog.glb (fox mesh with a repainted dog atlas).
                    // Falls back to a texture-recolored fox when it's missing.
                    SkinnedModel dogModel = _device != null ? ModelCache.TryGet(_device, "Dog.glb") : null;
                    Texture2D dogTexture = null;
                    if (dogModel == null && _device != null && _foxModel != null)
                    {
                        var dogColor = _random.NextDouble() < 0.5
                            ? new Color(140, 85, 45)    // brown
                            : new Color(240, 235, 225); // white
                        var baseTexture = _foxModel.Parts[0].Texture;
                        dogTexture = KitTextureFactory.GetKitTexture(_device, baseTexture,
                            dogColor, new Rectangle(0, 0, baseTexture.Width, baseTexture.Height));
                    }
                    var model = dogModel ?? _foxModel;
                    if (model != null)
                    {
                        _dog = new FoxWalker(model, dogTexture, scale: 0.0075f, tailScale: 0.35f,
                            chaseBall: true);
                        _walkers.Add(_dog);
                        _lastScore = -1; // dog stays until it scores (any goal), cap 150s
                        _dogTimer = 150f;
                        AudioManager.Instance.PlaySoundEffect("dog_bark");
                    }
                    break;
                }
                case Kind.Crows:
                    _flock = new BirdFlock(seagull: false, _random);
                    AudioManager.Instance.PlaySoundEffect("craw");
                    break;
                case Kind.Seagulls:
                    _flock = new BirdFlock(seagull: true, _random);
                    AudioManager.Instance.PlaySoundEffect("seagulls");
                    break;
                case Kind.Tornado:
                    _tornado = new TornadoFx(_random);
                    AudioManager.Instance.PlaySoundEffect("whirlwind");
                    break;
            }
        }

        public void Update(float dt, MatchEngine engine)
        {
            _time += dt;

            foreach (var s in _schedule)
            {
                if (!s.Started && _time >= s.StartAt)
                {
                    s.Started = true;
                    StartEvent(s.Type);
                }
            }

            // The dog chases and pushes the ball toward NO PASARAN's target
            // goal until a goal is scored (any goal), then walks off; 150s cap
            if (_dog != null && !_dog.IsGone && engine != null)
            {
                var playerTeam = engine.HomeTeam.IsPlayerControlled ? engine.HomeTeam : engine.AwayTeam;
                _dog.GoalSign = engine.AttackSign(playerTeam);

                int score = engine.HomeScore + engine.AwayScore;
                if (_lastScore < 0) _lastScore = score;
                _dogTimer -= dt;
                if (score != _lastScore || _dogTimer <= 0f)
                    _dog.Leave();
            }

            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                _walkers[i].Update(dt, engine);
                if (_walkers[i].IsGone)
                {
                    if (_walkers[i] == _dog) _dog = null;
                    _walkers.RemoveAt(i);
                }
            }

            if (_flock != null)
            {
                _flock.Update(dt);
                if (_flock.IsDone) _flock = null;
            }

            if (_tornado != null)
            {
                _tornado.Update(dt, engine);
                if (_tornado.IsDone) _tornado = null;
            }
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            foreach (var walker in _walkers)
                walker.Draw(device, view, projection, environment);
            _flock?.Draw(device, view, projection, environment);
            _tornado?.Draw(device, view, projection, environment);
        }
    }
}
