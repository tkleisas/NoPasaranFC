using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (1% of matches): lightning strikes a random outfield player.
    /// A jagged bolt flashes down for a split second with a thunder clap, then
    /// the player goes down, smoldering and briefly confused.
    /// </summary>
    public class ThunderFx
    {
        private enum Phase { Bolt, Smoke, Done }
        private const float BoltSeconds = 0.35f;
        private const float SmokeSeconds = 2.5f;

        private readonly Player _victim;
        private readonly Random _random;
        private readonly Vector3 _strikePoint;
        private Phase _phase = Phase.Bolt;
        private float _phaseTime;
        private BasicEffect _effect;

        public bool IsDone => _phase == Phase.Done;

        public ThunderFx(Player victim, Random random)
        {
            _victim = victim;
            _random = random ?? new Random();
            _strikePoint = WorldUnits.ToWorld(victim.FieldPosition);
        }

        public void Update(float dt, MatchEngine engine)
        {
            _phaseTime += dt;
            switch (_phase)
            {
                case Phase.Bolt:
                    if (_phaseTime >= BoltSeconds)
                    {
                        _phase = Phase.Smoke;
                        _phaseTime = 0f;
                        engine.EasterEggKnockdown(_victim);
                        _victim.CharcoalRemaining = 12f; // charred black
                        _victim.ConfusedRemaining = 5f;  // dazed
                    }
                    break;
                case Phase.Smoke:
                    if (_phaseTime >= SmokeSeconds)
                        _phase = Phase.Done;
                    break;
            }
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            _effect ??= new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            if (environment != null)
                environment.ApplyTo(_effect, false);

            var lines = new System.Collections.Generic.List<VertexPositionColor>();

            if (_phase == Phase.Bolt)
            {
                // Jagged bolt from 30m up, re-jittered every frame = electric flicker
                AddBolt(lines, _strikePoint + Vector3.Up * 30f, _strikePoint, 2.2f,
                    new Color(240, 245, 255, 255));
                // A thinner side branch
                AddBolt(lines, _strikePoint + Vector3.Up * 30f + new Vector3(1.5f, 0f, 1f),
                    _strikePoint + new Vector3(0.6f, 0.4f, 0.6f), 1.4f,
                    new Color(190, 210, 255, 200));

                // Ground flash disc around the strike point
                var flash = new Color(255, 255, 240, 160);
                for (int i = 0; i < 10; i++)
                {
                    float a0 = i / 10f * MathF.PI * 2f, a1 = (i + 1) / 10f * MathF.PI * 2f;
                    lines.Add(new VertexPositionColor(_strikePoint, flash));
                    lines.Add(new VertexPositionColor(
                        _strikePoint + new Vector3(MathF.Cos(a0) * 2f, 0.05f, MathF.Sin(a0) * 2f), flash));
                    lines.Add(new VertexPositionColor(
                        _strikePoint + new Vector3(MathF.Cos(a1) * 2f, 0.05f, MathF.Sin(a1) * 2f), flash));
                }
            }
            else if (_phase == Phase.Smoke)
            {
                // Charcoal dust: dark specks crumbling off the charred player,
                // plus a dust pile forming at his feet (all small triangles)
                var dust = new Color(25, 22, 20, 200);
                var victimPos = WorldUnits.ToWorld(_victim.FieldPosition);
                for (int i = 0; i < 14; i++)
                {
                    float angle = i * 2.4f;
                    float r = 0.25f + (i % 3) * 0.2f;
                    float fall = (_phaseTime * (1.2f + (i % 4) * 0.3f)) % 1.4f;
                    var pos = victimPos + new Vector3(
                        MathF.Cos(angle) * r, 1.1f - fall, MathF.Sin(angle) * r);
                    if (pos.Y < 0.05f) pos.Y = 0.05f;
                    float size = 0.06f + (i % 3) * 0.03f;
                    lines.Add(new VertexPositionColor(pos + new Vector3(-size, 0f, 0f), dust));
                    lines.Add(new VertexPositionColor(pos + new Vector3(size, 0f, 0f), dust));
                    lines.Add(new VertexPositionColor(pos + Vector3.Up * size * 1.6f, dust));
                }
                // The dust pile
                var pile = new Color(18, 16, 15, 220);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i / 8f * MathF.PI * 2f, a1 = (i + 1) / 8f * MathF.PI * 2f;
                    float pr = 0.2f + Math.Min(0.3f, _phaseTime * 0.15f);
                    lines.Add(new VertexPositionColor(victimPos + Vector3.Up * 0.03f, pile));
                    lines.Add(new VertexPositionColor(victimPos + new Vector3(MathF.Cos(a0) * pr, 0.03f, MathF.Sin(a0) * pr), pile));
                    lines.Add(new VertexPositionColor(victimPos + new Vector3(MathF.Cos(a1) * pr, 0.03f, MathF.Sin(a1) * pr), pile));
                }
            }

            if (lines.Count == 0) return;
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                if (_phase == Phase.Bolt && lines.Count > 30)
                {
                    // Bolt+branch as lines, ground flash as triangles
                    device.DrawUserPrimitives(PrimitiveType.LineList,
                        lines.GetRange(0, 30).ToArray(), 0, 15);
                    device.DrawUserPrimitives(PrimitiveType.TriangleList,
                        lines.GetRange(30, lines.Count - 30).ToArray(), 0, 10);
                }
                else
                {
                    // Smoke: all triangles
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, lines.ToArray(), 0, lines.Count / 3);
                }
            }
            device.DepthStencilState = DepthStencilState.Default;
        }

        private void AddBolt(System.Collections.Generic.List<VertexPositionColor> lines,
            Vector3 top, Vector3 bottom, float jitter, Color color)
        {
            const int segments = 10;
            Vector3 prev = top;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                var next = Vector3.Lerp(top, bottom, t);
                if (i < segments)
                {
                    next.X += (float)(_random.NextDouble() * 2 - 1) * jitter;
                    next.Z += (float)(_random.NextDouble() * 2 - 1) * jitter;
                }
                lines.Add(new VertexPositionColor(prev, color));
                lines.Add(new VertexPositionColor(next, color));
                prev = next;
            }
        }
    }
}
