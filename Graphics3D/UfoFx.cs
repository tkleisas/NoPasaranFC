using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (night only): a flying saucer descends from the night sky,
    /// slows to a hover low over the far touchline (classic "observing the
    /// match" behavior - and low enough to be in the broadcast frame), then
    /// accelerates up and away. Rotating ring of blinking lights;
    /// emissive/unlit so it glows in the dark.
    /// </summary>
    public class UfoFx
    {
        private const int Segments = 24;       // disc triangulation
        private const int LightCount = 10;     // blinking lights on the rim
        private const float Radius = 4f;       // disc radius (m)
        private const float Altitude = 8f;     // hover height above the pitch
        private const float ApproachTime = 9f; // decelerating fly-in
        private const float HoverTime = 5f;    // hovering over the far side
        private const float DepartTime = 8f;   // accelerating away
        private const float Duration = ApproachTime + HoverTime + DepartTime;

        private readonly Vector3 _start;
        private readonly Vector3 _hover;
        private readonly Vector3 _exit;
        private float _time;
        private BasicEffect _effect;

        public bool IsDone => _time > Duration;

        public UfoFx(Random random)
        {
            random ??= new Random();
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;

            // Cross the sky along the far touchline (the side the broadcast
            // camera looks toward), descending from high up to a low hover,
            // then climbing away
            int sign = random.Next(2) == 0 ? -1 : 1;
            float zDrift = (float)(random.NextDouble() * 2 - 1) * 6f;
            _hover = new Vector3(0f, Altitude, -halfW - 6f);
            _start = new Vector3(-sign * (halfL + 70f), Altitude + 18f, _hover.Z + zDrift);
            _exit = new Vector3(sign * (halfL + 70f), Altitude + 24f, _hover.Z + zDrift);
        }

        public void Update(float dt)
        {
            _time += dt;
        }

        /// <summary>Current saucer position: ease-out approach, hover, ease-in exit.</summary>
        public Vector3 Position
        {
            get
            {
                if (_time < ApproachTime)
                {
                    float t = _time / ApproachTime;
                    t = 1f - (1f - t) * (1f - t); // decelerate into the hover
                    return Vector3.Lerp(_start, _hover, t);
                }
                if (_time < ApproachTime + HoverTime)
                {
                    // Gentle bobbing while hovering over the far touchline
                    float h = _time - ApproachTime;
                    return _hover + new Vector3(0f, MathF.Sin(h * 1.6f) * 0.6f, 0f);
                }
                float d = Math.Min(1f, (_time - ApproachTime - HoverTime) / DepartTime);
                return Vector3.Lerp(_hover, _exit, d * d); // accelerate away
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
            // No environment tint: the saucer is emissive and glows at night

            var center = Position;
            var hull = new System.Collections.Generic.List<VertexPositionColor>(Segments * 12);
            var glow = new System.Collections.Generic.List<VertexPositionColor>(Segments * 6 + LightCount * 6);

            var hullTop = new Color(175, 180, 190);
            var hullBottom = new Color(85, 90, 100);
            var domeColor = new Color(140, 220, 255, 110);

            Vector3 apex = center + new Vector3(0f, 0.9f, 0f);
            Vector3 belly = center + new Vector3(0f, -0.6f, 0f);

            // Flattened disc: a shallow cone up to the apex, another down to the belly
            for (int i = 0; i < Segments; i++)
            {
                Vector3 a = RimPoint(center, i);
                Vector3 b = RimPoint(center, i + 1);
                hull.Add(new VertexPositionColor(apex, hullTop));
                hull.Add(new VertexPositionColor(a, hullTop));
                hull.Add(new VertexPositionColor(b, hullTop));
                hull.Add(new VertexPositionColor(belly, hullBottom));
                hull.Add(new VertexPositionColor(b, hullBottom));
                hull.Add(new VertexPositionColor(a, hullBottom));
            }

            // Glassy dome on top: hemisphere of three latitude rings + a cap
            const float domeRadius = 1.7f;
            Vector3 domeBase = center + new Vector3(0f, 0.55f, 0f);
            Vector3 prev = domeBase + new Vector3(0f, domeRadius, 0f);
            for (int ring = 1; ring <= 3; ring++)
            {
                float phi = ring * (MathF.PI / 2f) / 3f; // 0 = top, pi/2 = base
                float y = MathF.Cos(phi) * domeRadius;
                float r = MathF.Sin(phi) * domeRadius;
                for (int i = 0; i < Segments; i++)
                {
                    float a0 = i * MathF.PI * 2f / Segments;
                    float a1 = (i + 1) * MathF.PI * 2f / Segments;
                    var p0 = domeBase + new Vector3(MathF.Cos(a0) * r, y, MathF.Sin(a0) * r);
                    var p1 = domeBase + new Vector3(MathF.Cos(a1) * r, y, MathF.Sin(a1) * r);
                    if (ring == 1)
                    {
                        glow.Add(new VertexPositionColor(prev, domeColor));
                        glow.Add(new VertexPositionColor(p1, domeColor));
                        glow.Add(new VertexPositionColor(p0, domeColor));
                    }
                    else
                    {
                        float phi0 = (ring - 1) * (MathF.PI / 2f) / 3f;
                        float y0 = MathF.Cos(phi0) * domeRadius;
                        float r0 = MathF.Sin(phi0) * domeRadius;
                        var q0 = domeBase + new Vector3(MathF.Cos(a0) * r0, y0, MathF.Sin(a0) * r0);
                        var q1 = domeBase + new Vector3(MathF.Cos(a1) * r0, y0, MathF.Sin(a1) * r0);
                        glow.Add(new VertexPositionColor(q0, domeColor));
                        glow.Add(new VertexPositionColor(p1, domeColor));
                        glow.Add(new VertexPositionColor(p0, domeColor));
                        glow.Add(new VertexPositionColor(q0, domeColor));
                        glow.Add(new VertexPositionColor(q1, domeColor));
                        glow.Add(new VertexPositionColor(p1, domeColor));
                    }
                }
            }

            // Rotating ring of blinking lights around the rim
            var lightColors = new[]
            {
                new Color(255, 70, 60),
                new Color(90, 255, 120),
                new Color(255, 235, 140),
            };
            float ringAngle = _time * 1.6f;
            for (int i = 0; i < LightCount; i++)
            {
                float angle = ringAngle + i * MathF.PI * 2f / LightCount;
                var pos = center + new Vector3(MathF.Cos(angle) * (Radius + 0.15f), -0.15f,
                    MathF.Sin(angle) * (Radius + 0.15f));
                // Chase pattern: one lit light runs around the ring, the rest dim
                int lit = (int)(_time * 7f) % LightCount;
                var color = i == lit ? lightColors[i % lightColors.Length]
                                     : new Color(60, 60, 70);
                const float size = 0.38f;
                var side = new Vector3(-MathF.Sin(angle), 0f, MathF.Cos(angle)) * size;
                var up = Vector3.Up * size;
                glow.Add(new VertexPositionColor(pos - side - up, color));
                glow.Add(new VertexPositionColor(pos + side + up, color));
                glow.Add(new VertexPositionColor(pos - side + up, color));
                glow.Add(new VertexPositionColor(pos - side - up, color));
                glow.Add(new VertexPositionColor(pos + side - up, color));
                glow.Add(new VertexPositionColor(pos + side + up, color));
            }

            device.RasterizerState = RasterizerState.CullNone;

            // Pass 1: opaque hull
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, hull.ToArray(), 0, hull.Count / 3);
            }

            // Pass 2: translucent dome + lights
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, glow.ToArray(), 0, glow.Count / 3);
            }
            device.DepthStencilState = DepthStencilState.Default;
        }

        private static Vector3 RimPoint(Vector3 center, int i)
        {
            float angle = i * MathF.PI * 2f / Segments;
            return center + new Vector3(MathF.Cos(angle) * Radius, 0f, MathF.Sin(angle) * Radius);
        }
    }
}
