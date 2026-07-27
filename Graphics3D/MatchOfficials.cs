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
            public string Name;
        }

        private readonly Official _referee;
        private readonly Official _linesmanNorth; // far side (-Z)
        private readonly Official _linesmanSouth; // near side (+Z)
        private const float LinesmanSpeed = 2.0f; // m/s

        public MatchOfficials(GraphicsDevice device, SkinnedModel playerModel, Texture2D baseAtlas, int fixtureSeed = 42)
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

            _referee = new Official { Instance = MakeOfficial(), Name = StaffNames.Referee(fixtureSeed) };
            _linesmanNorth = new Official { Instance = MakeOfficial(), Name = StaffNames.Linesman(fixtureSeed + 101) };
            _linesmanSouth = new Official { Instance = MakeOfficial(), Name = StaffNames.Linesman(fixtureSeed + 307) };
        }

        /// <summary>Staff names + world positions, for the HUD name labels.</summary>
        public IEnumerable<(string Name, Vector3 WorldPosition)> GetNamedOfficials()
        {
            yield return (_referee.Name, _referee.Position);
            yield return (_linesmanNorth.Name, _linesmanNorth.Position);
            yield return (_linesmanSouth.Name, _linesmanSouth.Position);
        }
        
        public void Update(float dt, MatchEngine engine)
        {
            Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight);
            Vector3 refTarget;
            Vector3? refFace = ballWorld;
            float refSpeed = 4.5f;

            if (engine.CardPhase != MatchEngine.RefCardPhase.None && engine.CardPlayer != null)
            {
                // Card cutscene: abandon everything and walk to the booked player
                refTarget = WorldUnits.ToWorld(engine.CardPlayer.FieldPosition);
                refFace = refTarget;
                refSpeed = 5.5f;
            }
            else if (engine.CurrentState == MatchEngine.MatchState.PenaltyKick && engine.RestartPlayer != null)
            {
                // Behind the taker (away from the goal), on the penalty-area arc
                float goalX = engine.AttackedGoalLineX(engine.RestartPlayer.Team);
                float sign = Math.Sign(goalX - ballWorld.X);
                refTarget = new Vector3(ballWorld.X - sign * 4.5f, 0f, ballWorld.Z + 3.5f);
            }
            else if (engine.CurrentState == MatchEngine.MatchState.FreeKick && engine.RestartPlayer != null)
            {
                // ~9m behind the ball (away from the goal it attacks)
                float goalX = engine.AttackedGoalLineX(engine.RestartPlayer.Team);
                float sign = Math.Sign(goalX - ballWorld.X);
                refTarget = new Vector3(ballWorld.X - sign * 9f, 0f, ballWorld.Z + 2.5f);
            }
            else if (engine.Fouls.Count > 0 &&
                     engine.MatchTime - _lastHandledFoulTime < 3f &&
                     _lastHandledFoulTime >= 0f)
            {
                // A foul just happened (no card): go to the spot
                var foul = engine.Fouls[engine.Fouls.Count - 1];
                refTarget = WorldUnits.ToWorld(foul.Position);
                refSpeed = 5.0f;
            }
            else
            {
                // Free play: diagonal patrol, sprint when play breaks away
                refTarget = GetRefereeWaypoint(ballWorld);
            }

            // Track which foul we've already responded to
            if (engine.Fouls.Count > 0 && _lastHandledFoulCount != engine.Fouls.Count)
            {
                _lastHandledFoulCount = engine.Fouls.Count;
                _lastHandledFoulTime = engine.MatchTime;
            }

            MoveOfficial(_referee, refTarget, dt, refSpeed, refFace);
            engine.RefereePosition = new Vector2(
                WorldUnits.MToPx(_referee.Position.X) + MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
                WorldUnits.MToPx(_referee.Position.Z) + MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);

            // Linesmen: track the OFFSIDE LINE (second-last defender of the team
            // defending their half), not raw ball X. Sprint when it moves fast.
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float ballWorldX = ballWorld.X;

            float northLineX = GetOffsideLineX(engine, leftHalf: true);
            float southLineX = GetOffsideLineX(engine, leftHalf: false);
            Vector2 northTarget = new Vector2(Math.Clamp(northLineX, -halfL, 0f), -halfW - 0.8f);
            Vector2 southTarget = new Vector2(Math.Clamp(southLineX, 0f, halfL), halfW + 0.8f);

            // Offside flag: the linesman on the offside side raises the flag (Wave)
            var flagLinesman = engine.OffsideFlagRaised
                ? (ballWorldX < 0f ? _linesmanNorth : _linesmanSouth)
                : null;
            if (flagLinesman != null)
            {
                flagLinesman.Instance.Play("Wave");
                flagLinesman.Yaw = flagLinesman.Position.Z > 0 ? (float)Math.PI : 0f;
                flagLinesman.Instance.Update(dt);
            }

            // Sprint on fast-moving lines (counter-attacks)
            float northSpeed = Math.Abs(northTarget.X - _linesmanNorth.Position.X) > 8f ? 4.5f : LinesmanSpeed;
            float southSpeed = Math.Abs(southTarget.X - _linesmanSouth.Position.X) > 8f ? 4.5f : LinesmanSpeed;

            MoveOfficial(_linesmanNorth, new Vector3(northTarget.X, 0f, northTarget.Y), dt, northSpeed, null,
                skip: flagLinesman == _linesmanNorth);
            MoveOfficial(_linesmanSouth, new Vector3(southTarget.X, 0f, southTarget.Y), dt, southSpeed, null,
                skip: flagLinesman == _linesmanSouth);
        }

        private int _lastHandledFoulCount;
        private float _lastHandledFoulTime = -1f;

        /// <summary>X of the offside line on a half: the second-last defender of
        /// the team defending that half (falls back to ball X without defenders).</summary>
        private static float GetOffsideLineX(MatchEngine engine, bool leftHalf)
        {
            var defending = leftHalf ? engine.LeftDefendingTeam : engine.RightDefendingTeam;
            if (defending == null) return 0f;
            float goalLineWorldX = (leftHalf ? -1f : 1f) * WorldUnits.PitchLengthMeters / 2f;

            // Two closest defenders to their goal line, in world X
            float d1 = float.MaxValue, d2 = float.MaxValue;
            float closestX = 0f, secondX = 0f;
            foreach (var p in defending.Players)
            {
                if (!p.IsStarting) continue;
                float wx = WorldUnits.ToWorld(p.FieldPosition).X;
                float d = Math.Abs(wx - goalLineWorldX);
                if (d < d1) { d2 = d1; d1 = d; secondX = closestX; closestX = wx; }
                else if (d < d2) { d2 = d; secondX = wx; }
            }
            return secondX;
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
        
        private void MoveOfficial(Official official, Vector3 target, float dt, float speed, Vector3? faceTarget,
            bool skip = false)
        {
            if (skip) return; // flag is up: hold position and play the signal
            
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
