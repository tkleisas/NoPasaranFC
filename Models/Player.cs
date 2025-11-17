using Microsoft.Xna.Framework;

namespace NoPasaranFC.Models
{
    public enum PlayerPosition
    {
        Goalkeeper,
        Defender,
        Midfielder,
        Forward
    }
    
    public class Player
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string Name { get; set; }
        public PlayerPosition Position { get; set; }
        
        // Field position
        public Vector2 FieldPosition { get; set; }
        public Vector2 HomePosition { get; set; } // Default formation position
        
        // Attributes (0-100)
        public int Speed { get; set; }
        public int Shooting { get; set; }
        public int Passing { get; set; }
        public int Defending { get; set; }
        public int Agility { get; set; }
        public int Technique { get; set; }
        public int Stamina { get; set; }
        
        // In-game state
        public bool IsControlled { get; set; }
        public Vector2 Velocity { get; set; }
        
        // Collision and knockdown state
        public bool IsKnockedDown { get; set; }
        public float KnockdownTimer { get; set; }
        
        // Animation state (NEW SYSTEM)
        public PlayerAnimationSystem AnimationSystem { get; set; }
        public string CurrentAnimationState { get; set; } // "walk", "fall", "shoot", "tackle"
        
        // Animation state (OLD SYSTEM - kept for compatibility)
        public float AnimationFrame { get; set; }
        public float AnimationSpeed { get; set; }
        public int SpriteDirection { get; set; } // 0=down, 1=up, 2=left, 3=right
        
        // Sprite customization
        public string SpriteFileName { get; set; } // e.g., "player1.png", "goalkeeper1.png"
        public Color SpriteColor { get; set; } // Tint color for team differentiation
        
        public Player(string name, PlayerPosition position)
        {
            Name = name;
            Position = position;
            Speed = 50;
            Shooting = 50;
            Passing = 50;
            Defending = 50;
            Agility = 50;
            Technique = 50;
            Stamina = 100;
            IsControlled = false;
            Velocity = Vector2.Zero;
            AnimationFrame = 0;
            AnimationSpeed = 0.15f;
            SpriteDirection = 0;
            SpriteFileName = "player_default.png"; // Default sprite
            SpriteColor = Color.White; // No tint by default
            CurrentAnimationState = "walk";
        }
        
        public Player() { }
    }
}
