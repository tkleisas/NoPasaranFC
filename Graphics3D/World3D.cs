using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Static 3D world geometry for the match view: pitch, field markings,
    /// goals with nets, and stadium stands. All dimensions in meters,
    /// derived from the same MatchEngine pixel constants the 2D view uses.
    /// </summary>
    public class World3D
    {
        private readonly BasicEffect _colorEffect;
        private readonly BasicEffect _pitchEffect;
        private readonly Texture2D _grassTexture;
        
        // Opaque geometry (apron, ground, markings, posts, stands)
        private VertexPositionColor[] _opaqueVertices;
        private int[] _opaqueIndices;
        
        // Semi-transparent geometry (goal nets)
        private VertexPositionColor[] _netVertices;
        private int[] _netIndices;
        
        // Textured pitch plane
        private VertexPositionTexture[] _pitchVertices;
        private int[] _pitchIndices;
        
        // Field dimensions in meters (same constants as MatchScreen.DrawFieldMarkings)
        private readonly float _halfLength = WorldUnits.PitchLengthMeters / 2f;   // 52.5
        private readonly float _halfWidth = WorldUnits.PitchWidthMeters / 2f;     // 34
        private const float LineY = 0.02f;
        private const float LineWidth = 0.12f; // FIFA ~12cm lines
        
        public World3D(GraphicsDevice device)
        {
            _colorEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            
            _grassTexture = CreateGrassTexture(device);
            _pitchEffect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                TextureEnabled = true,
                Texture = _grassTexture,
                LightingEnabled = false
            };
            
            BuildPitch();
            BuildOpaqueGeometry();
            BuildNetGeometry();
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            _colorEffect.View = view;
            _colorEffect.Projection = projection;
            _colorEffect.World = Matrix.Identity;
            _pitchEffect.View = view;
            _pitchEffect.Projection = projection;
            _pitchEffect.World = Matrix.Identity;
            
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            // Textured striped pitch
            foreach (var pass in _pitchEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _pitchVertices, 0, _pitchVertices.Length,
                    _pitchIndices, 0, _pitchIndices.Length / 3);
            }
            
            // Opaque vertex-colored geometry
            foreach (var pass in _colorEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _opaqueVertices, 0, _opaqueVertices.Length,
                    _opaqueIndices, 0, _opaqueIndices.Length / 3);
            }
            
            // Semi-transparent nets on top
            device.BlendState = BlendState.AlphaBlend;
            foreach (var pass in _colorEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _netVertices, 0, _netVertices.Length,
                    _netIndices, 0, _netIndices.Length / 3);
            }
            device.BlendState = BlendState.Opaque;
        }
        
        #region Pitch + grass texture
        
        private Texture2D CreateGrassTexture(GraphicsDevice device)
        {
            // Mowing stripes: 5m wide stripes across the 105m length
            const int size = 512;
            const int stripeCount = 21; // 105m / 5m
            Texture2D texture = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Color grassA = new Color(34, 139, 34);
            Color grassB = new Color(26, 112, 26);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int stripe = x * stripeCount / size;
                    data[y * size + x] = (stripe % 2 == 0) ? grassA : grassB;
                }
            }
            
            texture.SetData(data);
            return texture;
        }
        
        private void BuildPitch()
        {
            float y = 0.01f; // Slightly above the apron to avoid z-fighting
            _pitchVertices = new[]
            {
                new VertexPositionTexture(new Vector3(-_halfLength, y, -_halfWidth), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(_halfLength, y, -_halfWidth), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(_halfLength, y, _halfWidth), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-_halfLength, y, _halfWidth), new Vector2(0, 1)),
            };
            _pitchIndices = new[] { 0, 1, 2, 0, 2, 3 };
        }
        
        #endregion
        
        #region Opaque geometry (apron, ground, markings, goals, stands)
        
        private void BuildOpaqueGeometry()
        {
            var verts = new List<VertexPositionColor>();
            var indices = new List<int>();
            
            // Dark ground plane far beyond the stadium
            AddGroundQuad(verts, indices, -150f, -110f, 150f, 110f, -0.02f, new Color(35, 35, 35));
            
            // Green apron around the pitch
            float apron = 6f;
            AddGroundQuad(verts, indices,
                -_halfLength - apron, -_halfWidth - apron,
                _halfLength + apron, _halfWidth + apron,
                0f, new Color(24, 100, 24));
            
            BuildFieldMarkings(verts, indices);
            BuildGoals(verts, indices);
            BuildStands(verts, indices);
            
            _opaqueVertices = verts.ToArray();
            _opaqueIndices = indices.ToArray();
        }
        
        private void BuildFieldMarkings(List<VertexPositionColor> verts, List<int> indices)
        {
            Color lineColor = Color.White;
            
            // Outer boundary (touchlines + goal lines)
            AddRectOutline(verts, indices, -_halfLength, -_halfWidth, _halfLength, _halfWidth, LineWidth, lineColor);
            
            // Halfway line
            AddLineQuad(verts, indices, new Vector2(0, -_halfWidth), new Vector2(0, _halfWidth), LineWidth, lineColor);
            
            // Center circle (FIFA: 9.15m radius) + center spot
            AddRing(verts, indices, Vector2.Zero, 9.15f, LineWidth, 32, lineColor);
            AddGroundQuad(verts, indices, -0.15f, -0.15f, 0.15f, 0.15f, LineY, lineColor);
            
            // Penalty areas (FIFA: 40.3m wide x 16.5m deep)
            float penaltyHalfWidth = 40.3f / 2f;
            float penaltyDepth = 16.5f;
            AddRectOutline(verts, indices, -_halfLength, -penaltyHalfWidth, -_halfLength + penaltyDepth, penaltyHalfWidth, LineWidth, lineColor);
            AddRectOutline(verts, indices, _halfLength - penaltyDepth, -penaltyHalfWidth, _halfLength, penaltyHalfWidth, LineWidth, lineColor);
            
            // Penalty spots (11m from goal line)
            AddGroundQuad(verts, indices, -_halfLength + 11f - 0.15f, -0.15f, -_halfLength + 11f + 0.15f, 0.15f, LineY, lineColor);
            AddGroundQuad(verts, indices, _halfLength - 11f - 0.15f, -0.15f, _halfLength - 11f + 0.15f, 0.15f, LineY, lineColor);
            
            // Goal areas / 6-yard boxes (FIFA: 18.3m wide x 5.5m deep)
            float goalAreaHalfWidth = 18.3f / 2f;
            float goalAreaDepth = 5.5f;
            AddRectOutline(verts, indices, -_halfLength, -goalAreaHalfWidth, -_halfLength + goalAreaDepth, goalAreaHalfWidth, LineWidth, lineColor);
            AddRectOutline(verts, indices, _halfLength - goalAreaDepth, -goalAreaHalfWidth, _halfLength, goalAreaHalfWidth, LineWidth, lineColor);
            
            // Corner arcs (FIFA: 1m radius)
            float cornerRadius = 1f;
            AddArc(verts, indices, new Vector2(-_halfLength, -_halfWidth), cornerRadius, 0, MathHelper.PiOver2, 8, lineColor);
            AddArc(verts, indices, new Vector2(_halfLength, -_halfWidth), cornerRadius, MathHelper.PiOver2, MathHelper.Pi, 8, lineColor);
            AddArc(verts, indices, new Vector2(_halfLength, _halfWidth), cornerRadius, MathHelper.Pi, MathHelper.Pi + MathHelper.PiOver2, 8, lineColor);
            AddArc(verts, indices, new Vector2(-_halfLength, _halfWidth), cornerRadius, -MathHelper.PiOver2, 0, 8, lineColor);
        }
        
        private void BuildGoals(List<VertexPositionColor> verts, List<int> indices)
        {
            float goalHalfWidth = WorldUnits.PxToM(MatchEngine.GoalWidth) / 2f; // 7.32m / 2
            float goalHeight = WorldUnits.PxToM(MatchEngine.GoalPostHeight);    // 2.44m
            const float postThickness = 0.12f;
            Color postColor = Color.White;
            
            BuildGoal(verts, indices, -_halfLength, -1f, goalHalfWidth, goalHeight, postThickness, postColor);
            BuildGoal(verts, indices, _halfLength, 1f, goalHalfWidth, goalHeight, postThickness, postColor);
        }
        
        private void BuildGoal(List<VertexPositionColor> verts, List<int> indices,
            float goalLineX, float direction, float halfWidth, float height, float thickness, Color color)
        {
            float x0 = goalLineX - thickness / 2f;
            float x1 = goalLineX + thickness / 2f;
            
            // Two upright posts
            AddBox(verts, indices,
                new Vector3(x0, 0f, -halfWidth - thickness / 2f),
                new Vector3(x1, height, -halfWidth + thickness / 2f), color);
            AddBox(verts, indices,
                new Vector3(x0, 0f, halfWidth - thickness / 2f),
                new Vector3(x1, height, halfWidth + thickness / 2f), color);
            
            // Crossbar
            AddBox(verts, indices,
                new Vector3(x0, height - thickness, -halfWidth - thickness / 2f),
                new Vector3(x1, height, halfWidth + thickness / 2f), color);
        }
        
        private void BuildStands(List<VertexPositionColor> verts, List<int> indices)
        {
            // 4 large dark boxes as stands, starting just past the stadium margin
            Color standColor = new Color(80, 60, 60);
            float gap = WorldUnits.StadiumMarginMeters; // stands begin at the margin distance
            float standDepth = 13f;
            float standHeight = 12f;
            float spanX = _halfLength + gap + standDepth;
            float spanZ = _halfWidth + gap + standDepth;
            
            // North stand (camera side, negative Z)
            AddBox(verts, indices,
                new Vector3(-spanX, 0f, -_halfWidth - gap - standDepth),
                new Vector3(spanX, standHeight, -_halfWidth - gap), standColor);
            // South stand
            AddBox(verts, indices,
                new Vector3(-spanX, 0f, _halfWidth + gap),
                new Vector3(spanX, standHeight, _halfWidth + gap + standDepth), standColor);
            // West stand
            AddBox(verts, indices,
                new Vector3(-_halfLength - gap - standDepth, 0f, -spanZ),
                new Vector3(-_halfLength - gap, standHeight, spanZ), standColor);
            // East stand
            AddBox(verts, indices,
                new Vector3(_halfLength + gap, 0f, -spanZ),
                new Vector3(_halfLength + gap + standDepth, standHeight, spanZ), standColor);
        }
        
        #endregion
        
        #region Net geometry (semi-transparent)
        
        private void BuildNetGeometry()
        {
            var verts = new List<VertexPositionColor>();
            var indices = new List<int>();
            
            float goalHalfWidth = WorldUnits.PxToM(MatchEngine.GoalWidth) / 2f;
            float goalHeight = WorldUnits.PxToM(MatchEngine.GoalPostHeight);
            float goalDepth = WorldUnits.PxToM(MatchEngine.GoalDepth); // 2m
            Color netColor = new Color(255, 255, 255, 80); // Semi-transparent white
            
            BuildNet(verts, indices, -_halfLength, -1f, goalHalfWidth, goalHeight, goalDepth, netColor);
            BuildNet(verts, indices, _halfLength, 1f, goalHalfWidth, goalHeight, goalDepth, netColor);
            
            _netVertices = verts.ToArray();
            _netIndices = indices.ToArray();
        }
        
        private void BuildNet(List<VertexPositionColor> verts, List<int> indices,
            float goalLineX, float direction, float halfWidth, float height, float depth, Color color)
        {
            float backX = goalLineX + direction * depth;
            
            // Back quad
            AddQuad(verts, indices,
                new Vector3(backX, 0f, -halfWidth),
                new Vector3(backX, 0f, halfWidth),
                new Vector3(backX, height, halfWidth),
                new Vector3(backX, height, -halfWidth), color);
            
            // Top quad
            AddQuad(verts, indices,
                new Vector3(goalLineX, height, -halfWidth),
                new Vector3(goalLineX, height, halfWidth),
                new Vector3(backX, height, halfWidth),
                new Vector3(backX, height, -halfWidth), color);
            
            // Side quads
            AddQuad(verts, indices,
                new Vector3(goalLineX, 0f, -halfWidth),
                new Vector3(backX, 0f, -halfWidth),
                new Vector3(backX, height, -halfWidth),
                new Vector3(goalLineX, height, -halfWidth), color);
            AddQuad(verts, indices,
                new Vector3(backX, 0f, halfWidth),
                new Vector3(goalLineX, 0f, halfWidth),
                new Vector3(goalLineX, height, halfWidth),
                new Vector3(backX, height, halfWidth), color);
        }
        
        #endregion
        
        #region Geometry helpers
        
        private static void AddQuad(List<VertexPositionColor> verts, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int baseIndex = verts.Count;
            verts.Add(new VertexPositionColor(a, color));
            verts.Add(new VertexPositionColor(b, color));
            verts.Add(new VertexPositionColor(c, color));
            verts.Add(new VertexPositionColor(d, color));
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
        
        private static void AddGroundQuad(List<VertexPositionColor> verts, List<int> indices,
            float x0, float z0, float x1, float z1, float y, Color color)
        {
            AddQuad(verts, indices,
                new Vector3(x0, y, z0),
                new Vector3(x1, y, z0),
                new Vector3(x1, y, z1),
                new Vector3(x0, y, z1), color);
        }
        
        /// <summary>Adds a flat line strip (quad) between two XZ points at marking height.</summary>
        private static void AddLineQuad(List<VertexPositionColor> verts, List<int> indices,
            Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 dir = to - from;
            if (dir.LengthSquared() < 0.0001f) return;
            dir.Normalize();
            Vector2 perp = new Vector2(-dir.Y, dir.X) * (width / 2f);
            
            AddQuad(verts, indices,
                new Vector3(from.X - perp.X, LineY, from.Y - perp.Y),
                new Vector3(to.X - perp.X, LineY, to.Y - perp.Y),
                new Vector3(to.X + perp.X, LineY, to.Y + perp.Y),
                new Vector3(from.X + perp.X, LineY, from.Y + perp.Y), color);
        }
        
        private static void AddRectOutline(List<VertexPositionColor> verts, List<int> indices,
            float x0, float z0, float x1, float z1, float width, Color color)
        {
            AddLineQuad(verts, indices, new Vector2(x0, z0), new Vector2(x1, z0), width, color);
            AddLineQuad(verts, indices, new Vector2(x1, z0), new Vector2(x1, z1), width, color);
            AddLineQuad(verts, indices, new Vector2(x1, z1), new Vector2(x0, z1), width, color);
            AddLineQuad(verts, indices, new Vector2(x0, z1), new Vector2(x0, z0), width, color);
        }
        
        /// <summary>Adds a ring (circle outline) as a strip of quads in the XZ plane.</summary>
        private static void AddRing(List<VertexPositionColor> verts, List<int> indices,
            Vector2 center, float radius, float width, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                AddLineQuad(verts, indices, p1, p2, width, color);
            }
        }
        
        /// <summary>Adds an arc as a strip of quads in the XZ plane.</summary>
        private static void AddArc(List<VertexPositionColor> verts, List<int> indices,
            Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle1 = startAngle + (endAngle - startAngle) * i / segments;
                float angle2 = startAngle + (endAngle - startAngle) * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                AddLineQuad(verts, indices, p1, p2, LineWidth, color);
            }
        }
        
        /// <summary>Adds an axis-aligned box (6 faces) with outward-facing quads.</summary>
        private static void AddBox(List<VertexPositionColor> verts, List<int> indices,
            Vector3 min, Vector3 max, Color color)
        {
            // Bottom
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), color);
            // Top
            AddQuad(verts, indices,
                new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), color);
            // North (-Z)
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z), color);
            // South (+Z)
            AddQuad(verts, indices,
                new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), color);
            // West (-X)
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), color);
            // East (+X)
            AddQuad(verts, indices,
                new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), color);
        }
        
        #endregion
    }
}
