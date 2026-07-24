using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Match officials: the referee (dark kit, follows the engine's RefereePosition,
    /// which already keeps 150-300px from the ball) and two linesmen patrolling the
    /// touchlines, tracking the ball's length position on their half.
    /// </summary>
    public class MatchOfficials
    {
        private class Official
        {
            public SkinnedModelInstance Instance;
            public Vector3 Position;
            public float Yaw;
            public string WalkClip = "Walking_A";
        }
        
        private readonly Official _referee;
        private readonly Official _linesmanNorth; // far side (-Z)
        private readonly Official _linesmanSouth; // near side (+Z)
        private const float LinesmanSpeed = 2.0f; // m/s
        
        public MatchOfficials(GraphicsDevice device, SkinnedModel playerModel, Texture2D baseAtlas)
        {
            SkinnedModelInstance MakeOfficial()
            {
                var instance = new SkinnedModelInstance(playerModel);
                // All-black official kit: recolor every garment region
                var dark = KitTextureFactory.GetKitTexture(device, baseAtlas, new Color(25, 25, 30),
                    new Rectangle(0, 0, 256, 256));
                instance.SetPartTexture("Soccer_Shirt", dark);
                instance.SetPartTexture("Soccer_Shorts", dark);
                instance.SetPartTexture("Soccer_SockLeft", dark);
                instance.SetPartTexture("Soccer_SockRight", dark);
                instance.Play("Idle");
                return instance;
            }
            
            _referee = new Official { Instance = MakeOfficial() };
            _linesmanNorth = new Official { Instance = MakeOfficial() };
            _linesmanSouth = new Official { Instance = MakeOfficial() };
        }
        
        public void Update(float dt, MatchEngine engine)
        {
            // Referee: the engine already moves RefereePosition sensibly
            MoveOfficial(_referee, WorldUnits.ToWorld(engine.RefereePosition), dt, 4f);
            
            // Linesmen: track the ball's length (X) on their half of the touchline
            float ballWorldX = WorldUnits.ToWorld(engine.BallPosition).X;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            
            Vector2 northTarget = new Vector2(
                Math.Clamp(ballWorldX, -WorldUnits.PitchLengthMeters / 2f, 0f), -halfW - 0.8f);
            Vector2 southTarget = new Vector2(
                Math.Clamp(ballWorldX, 0f, WorldUnits.PitchLengthMeters / 2f), halfW + 0.8f);
            
            MoveOfficial(_linesmanNorth, new Vector3(northTarget.X, 0f, northTarget.Y), dt, LinesmanSpeed);
            MoveOfficial(_linesmanSouth, new Vector3(southTarget.X, 0f, southTarget.Y), dt, LinesmanSpeed);
        }
        
        private void MoveOfficial(Official official, Vector3 target, float dt, float speed)
        {
            Vector3 delta = target - official.Position;
            delta.Y = 0f;
            float distance = delta.Length();
            
            if (distance > 0.3f)
            {
                Vector3 direction = delta / distance;
                float step = Math.Min(speed * dt, distance);
                official.Position += direction * step;
                official.Yaw = (float)Math.Atan2(direction.X, direction.Z);
                official.Instance.Play(official.WalkClip);
            }
            else
            {
                // Face the pitch when stationary
                official.Yaw = official.Position.Z > 0 ? (float)Math.PI : 0f;
                official.Instance.Play("Idle");
            }
            
            official.Instance.Update(dt);
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            foreach (var official in new[] { _referee, _linesmanNorth, _linesmanSouth })
            {
                official.Instance.Environment = environment;
                Matrix world = Matrix.CreateScale(0.75f)
                    * Matrix.CreateRotationY(official.Yaw)
                    * Matrix.CreateTranslation(official.Position);
                official.Instance.Draw(device, world, view, projection);
            }
        }
    }
}
