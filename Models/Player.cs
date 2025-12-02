using System.Collections.Generic;
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
    
    public enum PlayerRole
    {
        // Goalkeeper
        Goalkeeper,
        
        // Defenders
        LeftBack,
        RightBack,
        CenterBack,
        Sweeper,
        
        // Midfielders
        DefensiveMidfielder,
        CentralMidfielder,
        AttackingMidfielder,
        LeftMidfielder,
        RightMidfielder,
        LeftWinger,
        RightWinger,
        
        // Forwards
        Striker,
        CenterForward,
        
        // Generic fallback
        Generic
    }
    
    public class Player
    {
        public int Id { get; set; }
        public int TeamId { get; set; } // For database persistence
        public Team Team { get; set; } // Runtime reference to team
        public string Name { get; set; }
        public PlayerPosition Position { get; set; }
        public PlayerRole Role { get; set; } // Specific tactical role
        
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
        public float Stamina { get; set; }
        
        // In-game state
        public bool IsControlled { get; set; }
        public Vector2 Velocity { get; set; }
        
        // Collision and knockdown state
        public bool IsKnockedDown { get; set; }
        public float KnockdownTimer { get; set; }
        
        // Ball control cooldown
        public float LastKickTime { get; set; }
        
        // Throw-in state
        public bool IsThrowingIn { get; set; }
        
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
        
        // Roster management
        public bool IsStarting { get; set; } // Whether player is in starting lineup
        public int ShirtNumber { get; set; } // Player's shirt number

        // Celebration system
        public List<string> CelebrationIds { get; set; } // Player-specific celebration IDs (null/empty = use team/generic)
        
        // AI Controller (not serialized to database)
        [System.Text.Json.Serialization.JsonIgnore]
        public object AIController { get; set; } // Using object to avoid circular dependency
        
        // AI target position tracking (prevents oscillation)
        [System.Text.Json.Serialization.JsonIgnore]
        public Vector2 AITargetPosition { get; set; }
        
        // AI target position set flag
        [System.Text.Json.Serialization.JsonIgnore]
        public bool AITargetPositionSet { get; set; }
        
        // AI cached ball half state (prevents oscillation from centerline crossing)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool AICachedBallInDefensiveHalf { get; set; }
        
        // AI cache initialization flag
        [System.Text.Json.Serialization.JsonIgnore]
        public bool AICacheInitialized { get; set; }
        
        public Player(string name, PlayerPosition position)
        {
            Name = name;
            Position = position;
            Role = PlayerRole.Generic;
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
            IsStarting = false;
            ShirtNumber = 0;
            LastKickTime = 0f;
        }
        
        public Player() { }
    }
}
