using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NoPasaranFC.Models
{
    public class SpriteFrame
    {
        public string SpriteSheet { get; set; }
        public int SpriteIndex { get; set; } // 0-47 for 4x12 grid
        public int Rotation { get; set; } // 0-7 (0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°)
        public int Mirror { get; set; } // 0 = no mirror, 1 = mirror X-axis
        
        public SpriteFrame(string spriteSheet, int index, int rotation = 0, int mirror = 0)
        {
            SpriteSheet = spriteSheet;
            SpriteIndex = index;
            Rotation = rotation;
            Mirror = mirror;
        }
        
        public float GetRotationRadians()
        {
            return Rotation * (MathHelper.Pi / 4f); // 45 degrees per step
        }
    }
    
    public class SpriteAnimation
    {
        public string Name { get; set; }
        public List<SpriteFrame> Frames { get; set; }
        public float FrameDuration { get; set; } // Duration per frame in seconds
        public bool Loop { get; set; }
        
        public SpriteAnimation(string name, float frameDuration = 0.1f, bool loop = true)
        {
            Name = name;
            Frames = new List<SpriteFrame>();
            FrameDuration = frameDuration;
            Loop = loop;
        }
        
        public void AddFrame(string spriteSheet, int index, int rotation = 0, int mirror = 0)
        {
            Frames.Add(new SpriteFrame(spriteSheet, index, rotation, mirror));
        }
    }
}
