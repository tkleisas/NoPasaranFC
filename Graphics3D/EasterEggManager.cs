using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Rolls and runs the per-match easter eggs:
    /// - fox     10%  (any venue)   - wanders the pitch all match
    /// - dog      5%  (any venue)   - tinted fox with a small tail; walks in, barks, leaves
    /// - crows   10%  (any venue)   - dark flock crossing the sky, craw.wav
    /// - seagulls 50% (Sfageia)     - white flock circling, seagulls.wav
    /// - tornado  5%  (rain only)   - whirlwind funnel on the apron, whirlwind.wav
    /// - ufo      3%  (night only)  - saucer flyover, hovers over midfield, ufo.wav
    /// - blackout 2%  (night only)  - floodlights flicker out for 5-8s, then come back
    /// - cats     3%  (any venue)   - a clowder of 4-6 cats mills about, then leaves
    /// Events are scheduled at random times in the first ~2 minutes of play.
    /// </summary>
    public class EasterEggManager
    {
        private enum Kind { Fox, Dog, Crows, Seagulls, Tornado, Bees, Santa, BeachBall, Sprinklers, Thunder, Ufo, Blackout, Cats }

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
        private readonly MatchEnvironment _environment;

        private readonly List<FoxWalker> _walkers = new List<FoxWalker>();
        private FoxWalker _dog;
        private float _dogTimer;      // safety cap only - normally the dog leaves after a goal
        private int _lastScore = -1;  // score when the dog arrived (leaves when it changes)
        private BirdFlock _flock;
        private TornadoFx _tornado;
        private BeeSwarm _bees;
        private SantaSleigh _santa;
        private PianoFx _piano;
        private BeachBallFx _beachBall;
        private SprinklersFx _sprinklers;
        private ThunderFx _thunder;
        private UfoFx _ufo;
        private BlackoutFx _blackout;
        private readonly List<FoxWalker> _cats = new List<FoxWalker>();
        private int _catsPending;      // cats still waiting to wander on
        private float _catSpawnTimer;  // stagger between cat arrivals
        private float _catTimer = -1f; // countdown until the clowder leaves
        private SnowSystem _snow;
        private MatchEngine _engine; // set on first Update (bees/piano need it)
        private bool _foggy;
        private bool _snowing;
        private float _time;

        /// <summary>Fog easter egg active for the whole match (2%).</summary>
        public bool Foggy => _foggy;
        /// <summary>Snow easter egg active for the whole match (winter months, 5%).</summary>
        public bool Snowing => _snowing;

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

        public EasterEggManager(GraphicsDevice device, SkinnedModel foxModel, Venue venue,
            bool isRaining, MatchEnvironment environment = null, int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _device = device;
            _foxModel = foxModel;
            _venue = venue;
            _isRaining = isRaining;
            _environment = environment;

            if (Roll(0.10)) Schedule(Kind.Fox);
            if (Roll(0.05)) Schedule(Kind.Dog);
            if (Roll(0.10)) Schedule(Kind.Crows);
            if (venue == Venue.Sfageia && Roll(0.50)) Schedule(Kind.Seagulls);
            if (isRaining && Roll(0.05)) Schedule(Kind.Tornado);
            if (isRaining && Roll(0.01)) Schedule(Kind.Thunder); // lightning strikes only in the rain

            // New eggs: bees harass players, santa in December, beach ball, sprinklers
            if (Roll(0.05)) Schedule(Kind.Bees);
            if (DateTime.Now.Month == 12 && Roll(0.05)) Schedule(Kind.Santa);
            if (Roll(0.04)) Schedule(Kind.BeachBall);
            if (Roll(0.03)) Schedule(Kind.Sprinklers);

            // Night-only eggs: a UFO flyover and a floodlight blackout
            if (_environment?.IsNight == true)
            {
                if (Roll(0.03)) Schedule(Kind.Ufo);
                if (Roll(0.02)) Schedule(Kind.Blackout);
            }
            if (Roll(0.03)) Schedule(Kind.Cats);

            // Match-long atmospheres
            _foggy = Roll(0.02); // "The Fog" (Carpenter vibe)
            int month = DateTime.Now.Month;
            _snowing = (month == 12 || month == 1 || month == 2) && Roll(0.05);
            if (_foggy) _environment?.SetFog(true);
            if (_snowing)
            {
                _environment?.SetSnow(true);
                if (_device != null) _snow = new SnowSystem(_device);
            }
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
                case "thunder": StartEvent(Kind.Thunder); return "OK thunder";
                case "bees": StartEvent(Kind.Bees); return "OK bees";
                case "santa": StartEvent(Kind.Santa); return "OK santa";
                case "beachball": StartEvent(Kind.BeachBall); return "OK beachball";
                case "sprinklers": StartEvent(Kind.Sprinklers); return "OK sprinklers";
                case "ufo": StartEvent(Kind.Ufo); return "OK ufo";
                case "blackout": StartEvent(Kind.Blackout); return "OK blackout";
                case "cats": StartEvent(Kind.Cats); return "OK cats";
                case "fog": if (_environment != null) { _foggy = true; _environment.SetFog(true); return "OK fog"; } return "ERR no environment";
                case "snow": if (_environment != null) { _snowing = true; _environment.SetSnow(true); if (_snow == null && _device != null) _snow = new SnowSystem(_device); return "OK snow"; } return "ERR no environment";
                default: return "ERR usage: easter <fox|dog|crows|seagulls|tornado|bees|santa|beachball|sprinklers|ufo|blackout|cats|fog|snow>";
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
                case Kind.Bees:
                    if (_engine != null)
                    {
                        _bees = new BeeSwarm(_random, _engine);
                        AudioManager.Instance.PlaySoundEffect("bees");
                    }
                    break;
                case Kind.Thunder:
                    if (_engine != null)
                    {
                        Player victim = null;
                        int best = int.MaxValue;
                        foreach (var p in _engine.GetAllPlayers())
                        {
                            if (p.Position == Models.PlayerPosition.Goalkeeper) continue;
                            int roll = _random.Next();
                            if (roll < best) { best = roll; victim = p; }
                        }
                        if (victim != null)
                        {
                            _thunder = new ThunderFx(victim, _random);
                            AudioManager.Instance.PlaySoundEffect("thunder");
                        }
                    }
                    break;
                case Kind.Santa:
                    _santa = new SantaSleigh(_random);
                    AudioManager.Instance.PlaySoundEffect("santa_bells");
                    break;
                case Kind.BeachBall:
                    _beachBall = new BeachBallFx(_random);
                    break;
                case Kind.Sprinklers:
                    _sprinklers = new SprinklersFx(_random);
                    AudioManager.Instance.PlaySoundEffect("sprinklers");
                    break;
                case Kind.Ufo:
                    _ufo = new UfoFx(_random);
                    AudioManager.Instance.PlaySoundEffect("ufo");
                    break;
                case Kind.Blackout:
                    if (_environment != null)
                        _blackout = new BlackoutFx(_environment, _random);
                    break;
                case Kind.Cats:
                    if (_device != null && _foxModel != null && _catsPending == 0 && _cats.Count == 0)
                    {
                        _catsPending = 4 + _random.Next(3); // a clowder of 4-6
                        _catSpawnTimer = 0f;
                        _catTimer = 65f; // they wander off ~a minute after arriving
                    }
                    break;
            }
        }

        /// <summary>
        /// A cat: the fox model at cat size with a repainted moggy atlas
        /// (gray, black, ginger or cream), same trick as the dog.
        /// </summary>
        private FoxWalker SpawnCat()
        {
            var palette = new[]
            {
                new Color(115, 115, 122), // gray
                new Color(45, 45, 50),    // black
                new Color(198, 128, 58),  // ginger
                new Color(225, 218, 205), // cream
            };
            var baseTexture = _foxModel.Parts[0].Texture;
            var texture = KitTextureFactory.GetKitTexture(_device, baseTexture,
                palette[_random.Next(palette.Length)],
                new Rectangle(0, 0, baseTexture.Width, baseTexture.Height));
            return new FoxWalker(_foxModel, texture, scale: 0.004f);
        }

        public void Update(float dt, MatchEngine engine)
        {
            _time += dt;
            _engine = engine;

            // Snow easter egg: slippery pitch for the whole match
            if (_snowing && engine != null && !engine.IsSnowing)
                engine.IsSnowing = true;

            // Piano easter egg: 1% of penalties set the hook in the engine
            if (engine?.PianoDropTarget != null)
            {
                _piano = new PianoFx(engine.PianoDropTarget);
                engine.PianoDropTarget = null;
            }

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

            if (_bees != null)
            {
                _bees.Update(dt, engine);
                if (_bees.IsDone) _bees = null;
            }

            if (_santa != null)
            {
                _santa.Update(dt, engine);
                if (_santa.IsDone) _santa = null;
            }

            if (_piano != null)
            {
                _piano.Update(dt, engine);
                if (_piano.IsDone) _piano = null;
            }

            if (_beachBall != null)
            {
                _beachBall.Update(dt, engine);
                if (_beachBall.IsDone) _beachBall = null;
            }

            if (_sprinklers != null)
            {
                _sprinklers.Update(dt);
                if (_sprinklers.IsDone) _sprinklers = null;
            }

            if (_thunder != null)
            {
                _thunder.Update(dt, engine);
                if (_thunder.IsDone) _thunder = null;
            }

            if (_ufo != null)
            {
                _ufo.Update(dt);
                if (_ufo.IsDone) _ufo = null;
            }

            if (_blackout != null)
            {
                _blackout.Update(dt);
                if (_blackout.IsDone) _blackout = null;
            }

            // Cat invasion: the cats trickle in one by one from the sidelines
            // (staggered so they don't stack), mill about, then all wander off
            if (_catsPending > 0)
            {
                _catSpawnTimer -= dt;
                if (_catSpawnTimer <= 0f)
                {
                    _catsPending--;
                    _catSpawnTimer = 1.5f + (float)_random.NextDouble() * 2f;
                    var cat = SpawnCat();
                    _cats.Add(cat);
                    _walkers.Add(cat);
                }
            }
            if (_catTimer > 0f)
            {
                _catTimer -= dt;
                if (_catTimer <= 0f)
                    foreach (var cat in _cats) cat.Leave();
            }
            _cats.RemoveAll(c => c.IsGone);

            _snow?.Update(dt, _cameraTarget(engine));
        }

        private static Vector3 _cameraTarget(MatchEngine engine) =>
            engine != null ? WorldUnits.ToWorld(engine.BallPosition) : Vector3.Zero;

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            foreach (var walker in _walkers)
                walker.Draw(device, view, projection, environment);
            _flock?.Draw(device, view, projection, environment);
            _tornado?.Draw(device, view, projection, environment);
            _bees?.Draw(device, view, projection, environment);
            _santa?.Draw(device, view, projection, environment);
            _piano?.Draw(device, view, projection, environment);
            _beachBall?.Draw(device, view, projection, environment);
            _sprinklers?.Draw(device, view, projection, environment);
            _thunder?.Draw(device, view, projection, environment);
            _ufo?.Draw(device, view, projection, environment);
            _snow?.Draw(device, view, projection);
        }
    }
}
