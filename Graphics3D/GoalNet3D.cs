using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Dynamic 3D goal net: a set of spring-mass cloth panels (back, top, two
    /// sides) rendered as semi-transparent grid lines. Mirrors the structure of
    /// the 2D GoalNet (spring back to rest position, damping, wind sway, ball
    /// impulse) but self-contained in 3D - MatchEngine/GoalNet are untouched.
    /// Panel borders are anchored to the posts/ground; interior points are free.
    /// </summary>
    public class GoalNet3D
    {
        // Physics constants (frame-based, same feel as the 2D GoalNet)
        private const float Damping = 0.85f;
        private const float Stiffness = 0.3f;
        private const float NeighborStiffness = 0.12f;
        private const float WindStrength = 0.015f;
        private const float MaxStep = 0.6f;         // Velocity clamp per frame (m)
        private const float MaxDisplacement = 1.2f; // Safety tether to rest position (m)
        private const float ImpulseRadius = 0.55f;  // Ball influence radius (m)
        private const float ImpulseFactor = 0.03f;  // Fraction of ball velocity transferred
        
        private static readonly Color NetColor = new Color(255, 255, 255, 128);
        
        private readonly BasicEffect _effect;
        private readonly ClothPanel[] _panels;
        private readonly VertexPositionColor[] _lineVertices;
        private float _windTime;
        
        // Goal volume (world meters) for ball interaction
        private readonly float _goalLineX;
        private readonly float _direction; // +1: net extends toward +X, -1: toward -X
        private readonly float _halfWidth;
        private readonly float _height;
        private readonly float _depth;
        
        public GoalNet3D(GraphicsDevice device, float goalLineX, float direction)
        {
            _goalLineX = goalLineX;
            _direction = direction;
            _halfWidth = WorldUnits.PxToM(MatchEngine.GoalWidth) / 2f; // 3.66m
            _height = WorldUnits.PxToM(MatchEngine.GoalPostHeight);    // 2.44m
            _depth = WorldUnits.PxToM(MatchEngine.GoalDepth);          // 2m
            
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            
            float backX = goalLineX + direction * _depth;
            
            _panels = new[]
            {
                // Back plane (width x height)
                new ClothPanel(13, 6, (i, j, u, v) =>
                    new Vector3(backX, v * _height, MathHelper.Lerp(-_halfWidth, _halfWidth, u))),
                // Top plane (width x depth)
                new ClothPanel(13, 4, (i, j, u, v) =>
                    new Vector3(MathHelper.Lerp(goalLineX, backX, v), _height, MathHelper.Lerp(-_halfWidth, _halfWidth, u))),
                // Side planes (depth x height)
                new ClothPanel(4, 6, (i, j, u, v) =>
                    new Vector3(MathHelper.Lerp(goalLineX, backX, u), v * _height, -_halfWidth)),
                new ClothPanel(4, 6, (i, j, u, v) =>
                    new Vector3(MathHelper.Lerp(goalLineX, backX, u), v * _height, _halfWidth)),
            };
            
            int lineCount = 0;
            foreach (var panel in _panels)
                lineCount += panel.LineCount;
            _lineVertices = new VertexPositionColor[lineCount * 2];
        }
        
        /// <summary>Applies the environment tint to the net lines.</summary>
        public void ApplyEnvironment(MatchEnvironment environment)
        {
            environment.ApplyTo(_effect, false);
        }
        
        /// <summary>
        /// Advance the cloth simulation. When the ball (world meters) is inside
        /// the goal volume, its velocity pushes nearby net points outward.
        /// </summary>
        public void Update(float dt, Vector3 ballPos, Vector3 ballVel)
        {
            _windTime += dt;
            
            bool ballInside = IsInsideGoalVolume(ballPos) && ballVel.LengthSquared() > 0.25f;
            
            foreach (var panel in _panels)
            {
                if (ballInside)
                    panel.ApplyImpulse(ballPos, ballVel);
                panel.Update(_windTime);
            }
        }
        
        private bool IsInsideGoalVolume(Vector3 pos)
        {
            float depth = (pos.X - _goalLineX) * _direction;
            return depth > 0f && depth < _depth + 0.3f &&
                   Math.Abs(pos.Z) < _halfWidth + 0.3f &&
                   pos.Y > -0.3f && pos.Y < _height + 0.3f;
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            int index = 0;
            foreach (var panel in _panels)
                panel.FillLines(_lineVertices, ref index, NetColor);
            
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList,
                    _lineVertices, 0, _lineVertices.Length / 2);
            }
        }
        
        /// <summary>
        /// A rectangular spring-mass cloth grid. Border points are anchored,
        /// interior points spring back to their rest position with neighbor
        /// coupling, damping and a subtle wind sway.
        /// </summary>
        private class ClothPanel
        {
            private readonly int _cols;
            private readonly int _rows;
            private readonly Vector3[,] _points;
            private readonly Vector3[,] _velocities;
            private readonly Vector3[,] _rest;
            
            public int LineCount => (_cols - 1) * _rows + _cols * (_rows - 1);
            
            public ClothPanel(int cols, int rows, Func<int, int, float, float, Vector3> position)
            {
                _cols = cols;
                _rows = rows;
                _points = new Vector3[cols, rows];
                _velocities = new Vector3[cols, rows];
                _rest = new Vector3[cols, rows];
                
                for (int i = 0; i < cols; i++)
                {
                    for (int j = 0; j < rows; j++)
                    {
                        float u = i / (float)(cols - 1);
                        float v = j / (float)(rows - 1);
                        _points[i, j] = position(i, j, u, v);
                        _rest[i, j] = _points[i, j];
                        _velocities[i, j] = Vector3.Zero;
                    }
                }
            }
            
            private bool IsAnchored(int i, int j)
            {
                return i == 0 || i == _cols - 1 || j == 0 || j == _rows - 1;
            }
            
            public void ApplyImpulse(Vector3 center, Vector3 velocity)
            {
                for (int i = 0; i < _cols; i++)
                {
                    for (int j = 0; j < _rows; j++)
                    {
                        if (IsAnchored(i, j)) continue;
                        
                        float distance = Vector3.Distance(_points[i, j], center);
                        if (distance < ImpulseRadius)
                        {
                            float force = 1f - distance / ImpulseRadius;
                            _velocities[i, j] += velocity * force * ImpulseFactor;
                        }
                    }
                }
            }
            
            public void Update(float windTime)
            {
                for (int i = 0; i < _cols; i++)
                {
                    for (int j = 0; j < _rows; j++)
                    {
                        if (IsAnchored(i, j)) continue;
                        
                        // Subtle wind wave
                        _velocities[i, j] += new Vector3(
                            (float)Math.Sin(windTime * 1.5f + j * 0.5f),
                            (float)Math.Cos(windTime * 1.05f + i * 0.3f) * 0.5f,
                            (float)Math.Sin(windTime * 0.8f + i * 0.4f) * 0.5f) * WindStrength;
                        
                        // Spring back to rest position
                        _velocities[i, j] += (_rest[i, j] - _points[i, j]) * Stiffness;
                        
                        // Couple to neighbors so deformation propagates like cloth
                        Vector3 neighborSum = Vector3.Zero;
                        int neighborCount = 0;
                        if (i > 0) { neighborSum += _points[i - 1, j]; neighborCount++; }
                        if (i < _cols - 1) { neighborSum += _points[i + 1, j]; neighborCount++; }
                        if (j > 0) { neighborSum += _points[i, j - 1]; neighborCount++; }
                        if (j < _rows - 1) { neighborSum += _points[i, j + 1]; neighborCount++; }
                        _velocities[i, j] += (neighborSum / neighborCount - _points[i, j]) * NeighborStiffness;
                        
                        // Damping + integration (frame-based, like the 2D GoalNet)
                        Vector3 velocity = _velocities[i, j] * Damping;
                        if (velocity.LengthSquared() > MaxStep * MaxStep)
                            velocity = Vector3.Normalize(velocity) * MaxStep;
                        _velocities[i, j] = velocity;
                        _points[i, j] += velocity;
                        
                        // Safety tether so a hard shot can never explode the net
                        Vector3 displacement = _points[i, j] - _rest[i, j];
                        if (displacement.LengthSquared() > MaxDisplacement * MaxDisplacement)
                            _points[i, j] = _rest[i, j] + Vector3.Normalize(displacement) * MaxDisplacement;
                    }
                }
            }
            
            public void FillLines(VertexPositionColor[] buffer, ref int index, Color color)
            {
                for (int i = 0; i < _cols; i++)
                {
                    for (int j = 0; j < _rows - 1; j++)
                    {
                        buffer[index++] = new VertexPositionColor(_points[i, j], color);
                        buffer[index++] = new VertexPositionColor(_points[i, j + 1], color);
                    }
                }
                for (int j = 0; j < _rows; j++)
                {
                    for (int i = 0; i < _cols - 1; i++)
                    {
                        buffer[index++] = new VertexPositionColor(_points[i, j], color);
                        buffer[index++] = new VertexPositionColor(_points[i + 1, j], color);
                    }
                }
            }
        }
    }
}
