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
    /// Animated supporters on the Bahramis main stand: a handful of adults and
    /// children (scaled down) in NO PASARAN red. Seated fans use Sit_Chair_Idle,
    /// standing ones Idle; everybody Cheers during goal celebrations.
    /// </summary>
    public class FanSection
    {
        private class Fan
        {
            public SkinnedModelInstance Instance;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public bool Seated;
            public string IdleClip;
        }
        
        private readonly List<Fan> _fans = new List<Fan>();
        private bool _wasCelebrating;
        
        // Stand layout (must match World3D.BuildMainStand)
        private const float StandFrontZ = -40.0f;
        private const float StandStepDepth = 0.85f;
        private const float StandStepHeight = 0.4f;
        private const int Rows = 3;
        
        public FanSection(GraphicsDevice device, SkinnedModel playerModel, SkinnedModel femaleModel = null)
        {
            var rng = new Random(4242);
            
            // NO PASARAN supporters: mostly red shirts, some white/black, kids too
            Color[] shirtColors =
            {
                new Color(224, 0, 0), new Color(224, 0, 0), new Color(200, 20, 20),
                new Color(240, 240, 240), new Color(35, 35, 40),
            };
            
            // Two seated rows + a standing row in front of the stand
            int fanCount = 0;
            for (int row = 0; row < Rows; row++)
            {
                float stepTopY = (row + 1) * StandStepHeight;
                float rowZ = StandFrontZ - 0.45f - row * StandStepDepth;
                
                int seatsInRow = 5 + rng.Next(3);
                for (int s = 0; s < seatsInRow; s++)
                {
                    float x = -12f + fanCount * 2.1f + (float)rng.NextDouble() * 0.8f;
                    if (x > 13f) continue;
                    
                    bool isChild = rng.NextDouble() < 0.25;
                    bool seated = row > 0; // front row stands, upper rows sit
                    // ~40% female fans when the female model is available
                    var fanModel = femaleModel != null && rng.NextDouble() < 0.4 ? femaleModel : playerModel;
                    var atlas = fanModel.Parts[0].Texture;
                    
                    var fan = new Fan
                    {
                        Instance = new SkinnedModelInstance(fanModel),
                        // Seated fans sit on the 0.45m seat boxes; standing ones on the step
                        Position = new Vector3(x, stepTopY + (seated ? 0.45f : 0f), rowZ),
                        Yaw = 0f, // face +Z (the pitch)
                        Scale = isChild ? 0.55f : 0.72f + (float)rng.NextDouble() * 0.06f,
                        Seated = seated,
                        IdleClip = seated ? "Sit_Chair_Idle" : "Idle",
                    };
                    
                    // Team-colored casual clothes
                    Color shirt = shirtColors[rng.Next(shirtColors.Length)];
                    fan.Instance.SetPartTexture("Soccer_Shirt",
                        KitTextureFactory.GetKitTexture(device, atlas, shirt, new Rectangle(0, 0, 256, 256)));
                    fan.Instance.SetPartTexture("Soccer_Shorts",
                        KitTextureFactory.GetKitTexture(device, atlas,
                            KitTextureFactory.Darken(shirt, 0.4f), new Rectangle(256, 0, 256, 256)));
                    
                    // Randomize clip phase so they don't move in sync
                    fan.Instance.Play(fan.IdleClip);
                    fan.Instance.Update((float)rng.NextDouble() * 3f);
                    
                    _fans.Add(fan);
                    fanCount++;
                }
            }
        }
        
        public void Update(float dt, MatchEngine engine)
        {
            bool celebrating = engine.CurrentState == MatchEngine.MatchState.GoalCelebration;
            
            foreach (var fan in _fans)
            {
                if (celebrating && !_wasCelebrating)
                {
                    fan.Instance.Play("Cheer");
                }
                else if (!celebrating && _wasCelebrating)
                {
                    fan.Instance.Play(fan.IdleClip);
                }
                fan.Instance.Update(dt);
            }
            
            _wasCelebrating = celebrating;
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            foreach (var fan in _fans)
            {
                fan.Instance.Environment = environment;
                Matrix world = Matrix.CreateScale(fan.Scale)
                    * Matrix.CreateRotationY(fan.Yaw)
                    * Matrix.CreateTranslation(fan.Position);
                fan.Instance.Draw(device, world, view, projection);
            }
        }
    }
}
