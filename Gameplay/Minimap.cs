using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    public class Minimap
    {
        private Rectangle _minimapRect;
        private float _scaleX;
        private float _scaleY;
        
        public Minimap(int screenWidth, int screenHeight, int minimapWidth, int minimapHeight)
        {
            // Apply 150% scale
            minimapWidth = (int)(minimapWidth * 1.5f);
            minimapHeight = (int)(minimapHeight * 1.5f);
            
            // Position minimap at top center, underneath the score (around y=50)
            int topMargin = 50; // Below the score display
            _minimapRect = new Rectangle(
                (screenWidth - minimapWidth) / 2,
                topMargin,
                minimapWidth,
                minimapHeight
            );
            
            // Calculate scale factors
            _scaleX = (float)minimapWidth / MatchEngine.FieldWidth;
            _scaleY = (float)minimapHeight / MatchEngine.FieldHeight;
        }
        
        public void Draw(SpriteBatch spriteBatch, Texture2D pixel, MatchEngine matchEngine, Team homeTeam, Team awayTeam)
        {
            // Draw minimap background
            spriteBatch.Draw(pixel, _minimapRect, new Color(0, 40, 0, 200)); // Dark green, semi-transparent
            
            // Draw minimap border
            DrawBorder(spriteBatch, pixel, _minimapRect, Color.White, 2);
            
            // Draw field lines
            DrawFieldLines(spriteBatch, pixel);
            
            // Draw players
            foreach (var player in homeTeam.Players)
            {
                DrawPlayerOnMinimap(spriteBatch, pixel, player, Color.Blue);
            }
            
            foreach (var player in awayTeam.Players)
            {
                DrawPlayerOnMinimap(spriteBatch, pixel, player, Color.Red);
            }
            
            // Draw ball
            Vector2 ballScreenPos = WorldToMinimap(matchEngine.BallPosition);
            spriteBatch.Draw(pixel, new Rectangle((int)ballScreenPos.X - 3, (int)ballScreenPos.Y - 3, 6, 6), Color.White);
            
            // Draw camera viewport indicator
            DrawViewportIndicator(spriteBatch, pixel, matchEngine.Camera);
        }
        
        private void DrawPlayerOnMinimap(SpriteBatch spriteBatch, Texture2D pixel, Player player, Color color)
        {
            Vector2 screenPos = WorldToMinimap(player.FieldPosition);
            int size = player.IsControlled ? 5 : 4; // Slightly larger for better visibility
            
            Color drawColor = player.IsControlled ? Color.Yellow : color;
            spriteBatch.Draw(pixel, new Rectangle((int)screenPos.X - size/2, (int)screenPos.Y - size/2, size, size), drawColor);
        }
        
        private void DrawFieldLines(SpriteBatch spriteBatch, Texture2D pixel)
        {
            // Center line
            Vector2 topCenter = WorldToMinimap(new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2, 
                MatchEngine.StadiumMargin));
            Vector2 bottomCenter = WorldToMinimap(new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2, 
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight));
            
            DrawLine(spriteBatch, pixel, topCenter, bottomCenter, Color.White, 1);
            
            // Goals
            float goalWidth = 200f;
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
            
            // Left goal
            Vector2 leftGoalTop = WorldToMinimap(new Vector2(MatchEngine.StadiumMargin, centerY - goalWidth / 2));
            Vector2 leftGoalBottom = WorldToMinimap(new Vector2(MatchEngine.StadiumMargin, centerY + goalWidth / 2));
            DrawLine(spriteBatch, pixel, leftGoalTop, leftGoalBottom, Color.Yellow, 2);
            
            // Right goal
            Vector2 rightGoalTop = WorldToMinimap(new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth, centerY - goalWidth / 2));
            Vector2 rightGoalBottom = WorldToMinimap(new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth, centerY + goalWidth / 2));
            DrawLine(spriteBatch, pixel, rightGoalTop, rightGoalBottom, Color.Yellow, 2);
        }
        
        private void DrawViewportIndicator(SpriteBatch spriteBatch, Texture2D pixel, Camera camera)
        {
            // Calculate viewport bounds in world space
            float viewportWidth = 800f / camera.Zoom;
            float viewportHeight = 600f / camera.Zoom;
            
            Vector2 topLeft = new Vector2(
                camera.Position.X - viewportWidth / 2,
                camera.Position.Y - viewportHeight / 2
            );
            
            Vector2 topRight = new Vector2(
                camera.Position.X + viewportWidth / 2,
                camera.Position.Y - viewportHeight / 2
            );
            
            Vector2 bottomLeft = new Vector2(
                camera.Position.X - viewportWidth / 2,
                camera.Position.Y + viewportHeight / 2
            );
            
            Vector2 bottomRight = new Vector2(
                camera.Position.X + viewportWidth / 2,
                camera.Position.Y + viewportHeight / 2
            );
            
            // Convert to minimap coordinates
            Vector2 mmTopLeft = WorldToMinimap(topLeft);
            Vector2 mmTopRight = WorldToMinimap(topRight);
            Vector2 mmBottomLeft = WorldToMinimap(bottomLeft);
            Vector2 mmBottomRight = WorldToMinimap(bottomRight);
            
            // Draw viewport rectangle
            DrawLine(spriteBatch, pixel, mmTopLeft, mmTopRight, Color.Cyan, 1);
            DrawLine(spriteBatch, pixel, mmTopRight, mmBottomRight, Color.Cyan, 1);
            DrawLine(spriteBatch, pixel, mmBottomRight, mmBottomLeft, Color.Cyan, 1);
            DrawLine(spriteBatch, pixel, mmBottomLeft, mmTopLeft, Color.Cyan, 1);
        }
        
        private Vector2 WorldToMinimap(Vector2 worldPosition)
        {
            // Convert world position to minimap position
            float x = (worldPosition.X - MatchEngine.StadiumMargin) * _scaleX + _minimapRect.X;
            float y = (worldPosition.Y - MatchEngine.StadiumMargin) * _scaleY + _minimapRect.Y;
            return new Vector2(x, y);
        }
        
        private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)System.Math.Atan2(edge.Y, edge.X);
            
            spriteBatch.Draw(pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
