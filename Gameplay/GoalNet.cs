using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace NoPasaranFC.Gameplay
{
    public class GoalNet
    {
        private const int GridWidth = 8;
        private const int GridHeight = 12;
        private Vector2[,] _netPoints;
        private Vector2[,] _netVelocity;
        private Vector2[,] _restPositions;
        private float _x, _y, _depth, _width;
        private bool _facingRight;
        private Random _random;
        private float _windTime;
        
        // Physics constants
        private const float Damping = 0.85f;
        private const float Stiffness = 0.3f;
        private const float WindStrength = 0.15f;
        private const float WindFrequency = 1.5f;
        
        public GoalNet(float x, float y, float depth, float width, bool facingRight)
        {
            _x = x;
            _y = y;
            _depth = depth;
            _width = width;
            _facingRight = facingRight;
            _random = new Random();
            _windTime = 0f;
            
            _netPoints = new Vector2[GridWidth, GridHeight];
            _netVelocity = new Vector2[GridWidth, GridHeight];
            _restPositions = new Vector2[GridWidth, GridHeight];
            
            InitializeNet();
        }
        
        private void InitializeNet()
        {
            for (int i = 0; i < GridWidth; i++)
            {
                for (int j = 0; j < GridHeight; j++)
                {
                    float xPos = _facingRight ? _x + (i * _depth / (GridWidth - 1)) : _x + _depth - (i * _depth / (GridWidth - 1));
                    float yPos = _y + (j * _width / (GridHeight - 1));
                    
                    _netPoints[i, j] = new Vector2(xPos, yPos);
                    _restPositions[i, j] = new Vector2(xPos, yPos);
                    _netVelocity[i, j] = Vector2.Zero;
                }
            }
        }
        
        public void Update(GameTime gameTime, Vector2 ballPosition, Vector2 ballVelocity, float ballRadius)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _windTime += deltaTime;
            
            // Apply wind effect (subtle wave motion)
            for (int i = 1; i < GridWidth - 1; i++) // Skip fixed edges
            {
                for (int j = 1; j < GridHeight - 1; j++)
                {
                    float windX = (float)Math.Sin(_windTime * WindFrequency + j * 0.5f) * WindStrength;
                    float windY = (float)Math.Cos(_windTime * WindFrequency * 0.7f + i * 0.3f) * WindStrength * 0.5f;
                    
                    _netVelocity[i, j] += new Vector2(windX, windY);
                }
            }
            
            // Apply ball collision deformation
            if (ballVelocity.LengthSquared() > 1f) // Only if ball is moving
            {
                for (int i = 0; i < GridWidth; i++)
                {
                    for (int j = 0; j < GridHeight; j++)
                    {
                        float distance = Vector2.Distance(_netPoints[i, j], ballPosition);
                        if (distance < ballRadius + 30f) // Influence radius
                        {
                            Vector2 direction = _netPoints[i, j] - ballPosition;
                            if (direction.LengthSquared() > 0.01f)
                            {
                                direction.Normalize();
                                float force = Math.Max(0, 1f - distance / (ballRadius + 30f));
                                _netVelocity[i, j] += direction * ballVelocity.Length() * force * 0.3f;
                            }
                        }
                    }
                }
            }
            
            // Apply spring forces to return to rest position
            for (int i = 1; i < GridWidth - 1; i++)
            {
                for (int j = 1; j < GridHeight - 1; j++)
                {
                    Vector2 displacement = _restPositions[i, j] - _netPoints[i, j];
                    _netVelocity[i, j] += displacement * Stiffness;
                    
                    // Apply damping
                    _netVelocity[i, j] *= Damping;
                    
                    // Update position
                    _netPoints[i, j] += _netVelocity[i, j];
                }
            }
        }
        
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
        {
            Color netLineColor = new Color(180, 180, 180, 200);
            Color backColor = new Color(100, 100, 100, 100);
            
            // Draw back panel (semi-transparent)
            Rectangle backPanel = new Rectangle((int)_x, (int)_y, (int)_depth, (int)_width);
            spriteBatch.Draw(pixel, backPanel, backColor);
            
            // Draw net grid - vertical lines
            for (int i = 0; i < GridWidth; i++)
            {
                for (int j = 0; j < GridHeight - 1; j++)
                {
                    DrawLine(spriteBatch, pixel, _netPoints[i, j], _netPoints[i, j + 1], netLineColor, 2);
                }
            }
            
            // Draw net grid - horizontal lines
            for (int j = 0; j < GridHeight; j++)
            {
                for (int i = 0; i < GridWidth - 1; i++)
                {
                    DrawLine(spriteBatch, pixel, _netPoints[i, j], _netPoints[i + 1, j], netLineColor, 2);
                }
            }
            
            // Draw diagonal lines for mesh appearance
            for (int i = 0; i < GridWidth - 1; i++)
            {
                for (int j = 0; j < GridHeight - 1; j++)
                {
                    if ((i + j) % 2 == 0)
                    {
                        DrawLine(spriteBatch, pixel, _netPoints[i, j], _netPoints[i + 1, j + 1], 
                            new Color(180, 180, 180, 100), 1);
                    }
                }
            }
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            spriteBatch.Draw(pixel, 
                new Rectangle((int)start.X, (int)start.Y, (int)length, thickness),
                null, color, angle, new Vector2(0, 0.5f), SpriteEffects.None, 0);
        }
        
        public bool IsPlayerBehindNet(Vector2 playerPosition)
        {
            // Check if player is inside the goal area (behind the net)
            if (_facingRight)
            {
                return playerPosition.X >= _x && playerPosition.X <= _x + _depth &&
                       playerPosition.Y >= _y && playerPosition.Y <= _y + _width;
            }
            else
            {
                return playerPosition.X >= _x && playerPosition.X <= _x + _depth &&
                       playerPosition.Y >= _y && playerPosition.Y <= _y + _width;
            }
        }
    }
}
