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
            // Referee: waypoint patrol on a diagonal lane (how real refs cover the
            // pitch) - reposition when the ball moves, face play when settled
            Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight);
            Vector3 waypoint = GetRefereeWaypoint(ballWorld);
            MoveOfficial(_referee, waypoint, dt, 4.5f, faceTarget: ballWorld);
            engine.RefereePosition = new Vector2(
                WorldUnits.MToPx(_referee.Position.X) + MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
                WorldUnits.MToPx(_referee.Position.Z) + MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
            
            // Linesmen: track the ball's length (X) on their half of the touchline
            float ballWorldX = ballWorld.X;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            
            Vector2 northTarget = new Vector2(
                Math.Clamp(ballWorldX, -WorldUnits.PitchLengthMeters / 2f, 0f), -halfW - 0.8f);
            Vector2 southTarget = new Vector2(
                Math.Clamp(ballWorldX, 0f, WorldUnits.PitchLengthMeters / 2f), halfW + 0.8f);
            
            MoveOfficial(_linesmanNorth, new Vector3(northTarget.X, 0f, northTarget.Y), dt, LinesmanSpeed, null);
            MoveOfficial(_linesmanSouth, new Vector3(southTarget.X, 0f, southTarget.Y), dt, LinesmanSpeed, null);
        }
        
        // Diagonal patrol lane (center circle to both corners-ish), real refs
        // cover a diagonal rather than shadowing the ball
        private static readonly Vector3[] PatrolLane =
        {
            new Vector3(-28f, 0f, -20f),
            new Vector3(-12f, 0f, -8f),
            new Vector3(0f, 0f, 4f),
            new Vector3(12f, 0f, 14f),
            new Vector3(28f, 0f, 24f),
        };
        private const float MinBallDistance = 8f; // meters, refs keep off the play
        
        private static Vector3 GetRefereeWaypoint(Vector3 ball)
        {
            // Closest lane point to the ball, nudged away if too close to play
            Vector3 best = PatrolLane[0];
            float bestDist = float.MaxValue;
            foreach (var point in PatrolLane)
            {
                float d = Vector3.DistanceSquared(point, ball);
                if (d < bestDist) { bestDist = d; best = point; }
            }
            
            Vector3 away = best - ball;
            away.Y = 0f;
            if (away.LengthSquared() < MinBallDistance * MinBallDistance && away.LengthSquared() > 0.001f)
            {
                away.Normalize();
                best = ball + away * MinBallDistance;
            }
            return best;
        }
        
        private void MoveOfficial(Official official, Vector3 target, float dt, float speed, Vector3? faceTarget)
        {
            Vector3 delta = target - official.Position;
            delta.Y = 0f;
            float distance = delta.Length();
            
            // Only walk when the target is meaningfully away (no hovering jitter)
            if (distance > 2f)
            {
                Vector3 direction = delta / distance;
                float step = Math.Min(speed * dt, distance);
                official.Position += direction * step;
                official.Yaw = (float)Math.Atan2(direction.X, direction.Z);
                official.Instance.Play(official.WalkClip);
            }
            else
            {
                // Face the play when settled (refs watch the ball, not their path)
                if (faceTarget.HasValue)
                {
                    Vector3 look = faceTarget.Value - official.Position;
                    if (look.LengthSquared() > 0.01f)
                        official.Yaw = (float)Math.Atan2(look.X, look.Z);
                }
                else
                {
                    official.Yaw = official.Position.Z > 0 ? (float)Math.PI : 0f;
                }
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
