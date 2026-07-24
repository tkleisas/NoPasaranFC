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
    /// Team dugout: a small shelter on the far touchline with the substitute
    /// players seated (Sit_Chair_Idle) and the coach standing nearby.
    /// </summary>
    public class TeamBench
    {
        private class BenchMember
        {
            public SkinnedModelInstance Instance;
            public Vector3 Position;
            public bool Seated;
        }
        
        private readonly List<BenchMember> _members = new List<BenchMember>();
        private readonly BasicEffect _shelterEffect;
        private readonly VertexPositionColor[] _shelterVertices;
        private readonly int[] _shelterIndices;
        private readonly Random _random = new Random();
        private float _coachHomeX;
        private bool _coachMoving;
        
        public TeamBench(GraphicsDevice device, Team team, Vector2 center, SkinnedModel playerModel,
            SkinnedModel femaleModel, Texture2D baseAtlas, Color shirt, Color shorts, Color socks)
        {
            _shelterEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            
            BuildShelter(center, shirt, out _shelterVertices, out _shelterIndices);
            
            // Seated substitutes on the bench
            var subs = team.Players.FindAll(p => !p.IsStarting);
            int seated = Math.Min(6, subs.Count);
            for (int i = 0; i < seated; i++)
            {
                var model = PickModel(subs[i], playerModel, femaleModel, i);
                var instance = new SkinnedModelInstance(model);
                ApplyKitToInstance(device, instance, baseAtlas, shirt, shorts, socks, subs[i].ShirtNumber);
                instance.Play("Sit_Chair_Idle");
                instance.Update((float)_random.NextDouble() * 3f); // desync
                _members.Add(new BenchMember
                {
                    Instance = instance,
                    Position = new Vector3(center.X - 1.5f + i * 0.6f, 0.45f, center.Y),
                    Seated = true,
                });
            }
            
            // Coach standing in front of the bench, directing the team
            {
                var coach = new SkinnedModelInstance(playerModel);
                ApplySolidKit(device, coach, baseAtlas, new Color(35, 35, 40));
                coach.Play("Spellcasting"); // "directing hands" gesture
                _coachHomeX = center.X + 2.2f;
                _members.Add(new BenchMember
                {
                    Instance = coach,
                    Position = new Vector3(_coachHomeX, 0f, center.Y + 0.5f),
                    Seated = false,
                });
            }
        }
        
        private static SkinnedModel PickModel(Player player, SkinnedModel male, SkinnedModel female, int index)
        {
            if (female != null && player.Name != null && (player.Name.GetHashCode() & 3) == 0)
                return female;
            return male;
        }
        
        private static void ApplyKitToInstance(GraphicsDevice device, SkinnedModelInstance instance,
            Texture2D baseAtlas, Color shirt, Color shorts, Color socks, int shirtNumber)
        {
            var shirtTex = KitTextureFactory.GetKitTexture(device, baseAtlas, shirt, new Rectangle(0, 0, 256, 256));
            var numbered = KitTextureFactory.GetNumberedShirtTexture(device, shirtTex, shirtNumber,
                KitTextureFactory.ContrastFor(shirt));
            var shortsTex = KitTextureFactory.GetKitTexture(device, baseAtlas, shorts, new Rectangle(256, 0, 256, 256));
            var socksTex = KitTextureFactory.GetKitTexture(device, baseAtlas, socks, new Rectangle(0, 256, 256, 256));
            instance.SetPartTexture("Soccer_Shirt", numbered);
            instance.SetPartTexture("Soccer_Shorts", shortsTex);
            instance.SetPartTexture("Soccer_SockLeft", socksTex);
            instance.SetPartTexture("Soccer_SockRight", socksTex);
        }
        
        /// <summary>Coach wears a dark suit (all kit regions dark).</summary>
        private static void ApplySolidKit(GraphicsDevice device, SkinnedModelInstance instance, Texture2D baseAtlas, Color color)
        {
            var tex = KitTextureFactory.GetKitTexture(device, baseAtlas, color, new Rectangle(0, 0, 512, 512));
            instance.SetPartTexture("Soccer_Shirt", tex);
            instance.SetPartTexture("Soccer_Shorts", tex);
            instance.SetPartTexture("Soccer_SockLeft", tex);
            instance.SetPartTexture("Soccer_SockRight", tex);
        }
        
        /// <summary>Simple dugout: two posts, a flat roof (team color), a bench box.</summary>
        private static void BuildShelter(Vector2 center, Color teamColor,
            out VertexPositionColor[] vertices, out int[] indices)
        {
            var verts = new List<VertexPositionColor>();
            var inds = new List<int>();
            Color frame = new Color(60, 60, 65);
            
            void AddBox(Vector3 min, Vector3 max, Color color)
            {
                // Front/back/left/right/top faces (bottom skipped)
                void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    int i0 = verts.Count;
                    verts.Add(new VertexPositionColor(a, color));
                    verts.Add(new VertexPositionColor(b, color));
                    verts.Add(new VertexPositionColor(c, color));
                    verts.Add(new VertexPositionColor(d, color));
                    inds.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
                Quad(new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z));
                Quad(new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z));
                Quad(new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z));
                Quad(new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z));
                Quad(new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z));
            }
            
            float cx = center.X, cz = center.Y;
            // Posts (2.7m: seated heads at scale 0.72 reach ~2.4m)
            AddBox(new Vector3(cx - 2f, 0f, cz - 0.05f), new Vector3(cx - 1.9f, 2.7f, cz + 0.05f), frame);
            AddBox(new Vector3(cx + 1.9f, 0f, cz - 0.05f), new Vector3(cx + 2f, 2.7f, cz + 0.05f), frame);
            // Roof (team color)
            AddBox(new Vector3(cx - 2.2f, 2.55f, cz - 0.65f), new Vector3(cx + 2.2f, 2.7f, cz + 0.45f), teamColor);
            // Bench box
            AddBox(new Vector3(cx - 1.8f, 0f, cz - 0.2f), new Vector3(cx + 1.8f, 0.45f, cz + 0.15f), new Color(90, 70, 50));
            
            vertices = verts.ToArray();
            indices = inds.ToArray();
        }
        
        public void Update(float dt, float ballWorldX)
        {
            foreach (var member in _members)
            {
                if (!member.Seated)
                {
                    // Coach: pace along the dugout following the ball
                    float targetX = MathHelper.Clamp(ballWorldX, _coachHomeX - 2.5f, _coachHomeX + 2.5f);
                    float delta = targetX - member.Position.X;
                    
                    if (Math.Abs(delta) > 0.15f)
                    {
                        // Walking to a better vantage point
                        member.Instance.Play("Walking_A");
                        float step = Math.Sign(delta) * Math.Min(1.2f * dt, Math.Abs(delta));
                        member.Position += new Vector3(step, 0f, 0f);
                        _coachMoving = true;
                    }
                    else if (_coachMoving)
                    {
                        // Arrived: back to directing
                        member.Instance.Play("Spellcasting");
                        _coachMoving = false;
                    }
                }
                member.Instance.Update(dt);
            }
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            // Shelter
            environment.ApplyTo(_shelterEffect, false);
            _shelterEffect.View = view;
            _shelterEffect.Projection = projection;
            _shelterEffect.World = Matrix.Identity;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _shelterEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _shelterVertices, 0, _shelterVertices.Length,
                    _shelterIndices, 0, _shelterIndices.Length / 3);
            }
            
            // Members (face the pitch = +Z)
            foreach (var member in _members)
            {
                member.Instance.Environment = environment;
                Matrix world = Matrix.CreateScale(0.72f)
                    * Matrix.CreateTranslation(member.Position);
                member.Instance.Draw(device, world, view, projection);
            }
        }
    }
}
