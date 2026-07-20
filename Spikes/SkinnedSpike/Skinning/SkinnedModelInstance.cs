using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SkinnedSpike.Skinning;

/// <summary>
/// A per-character playback instance of a shared SkinnedModel.
/// Owns clip state, cross-fade blending, and the bone matrix palette.
/// </summary>
public class SkinnedModelInstance
{
    private readonly SkinnedModel _model;
    private readonly Matrix[] _boneMatrices;
    private readonly SkinnedModel.NodePose[] _poseA;
    private readonly SkinnedModel.NodePose[] _poseB;
    private readonly SkinnedModel.NodePose[] _poseOut;
    private SkinnedEffect _effect;

    public AnimationClip CurrentClip { get; private set; }
    public float CurrentTime { get; private set; }

    private AnimationClip _previousClip;
    private float _previousTime;
    private float _blend;          // 1 = fully in CurrentClip
    private float _blendSpeed = 1f / 0.3f; // 0.3s cross-fade

    public SkinnedModelInstance(SkinnedModel model)
    {
        _model = model;
        _boneMatrices = new Matrix[Math.Min(model.JointCount, 72)]; // SkinnedEffect limit
        _poseA = new SkinnedModel.NodePose[model.NodeCount];
        _poseB = new SkinnedModel.NodePose[model.NodeCount];
        _poseOut = new SkinnedModel.NodePose[model.NodeCount];

        CurrentClip = model.Clips.FirstOrDefault();
    }

    public void Play(string clipName)
    {
        var clip = _model.Clips.FirstOrDefault(c => string.Equals(c.Name, clipName, StringComparison.OrdinalIgnoreCase));
        if (clip == null || clip == CurrentClip) return;

        _previousClip = CurrentClip;
        _previousTime = CurrentTime;
        CurrentClip = clip;
        CurrentTime = 0f;
        _blend = 0f;
    }

    public void Update(float deltaTime)
    {
        CurrentTime += deltaTime;
        _previousTime += deltaTime;
        _blend = Math.Min(1f, _blend + _blendSpeed * deltaTime);

        if (CurrentClip == null)
        {
            _model.GetBindPose(_poseOut);
        }
        else if (_previousClip != null && _blend < 1f)
        {
            _model.SampleClip(_previousClip, _previousTime, _poseA);
            _model.SampleClip(CurrentClip, CurrentTime, _poseB);
            SkinnedModel.BlendPoses(_poseA, _poseB, _blend, _poseOut);
        }
        else
        {
            _previousClip = null;
            _model.SampleClip(CurrentClip, CurrentTime, _poseOut);
        }

        _model.ComputeBoneMatrices(_poseOut, _boneMatrices);
    }

    public void Draw(GraphicsDevice device, Matrix world, Matrix view, Matrix projection)
    {
        device.RasterizerState = RasterizerState.CullClockwise; // glTF front faces are CCW
        device.DepthStencilState = DepthStencilState.Default;
        device.SamplerStates[0] = SamplerState.LinearWrap;
        device.BlendState = BlendState.Opaque;

        _effect ??= new SkinnedEffect(device) { WeightsPerVertex = 4 };
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;
        _effect.EnableDefaultLighting();
        _effect.SetBoneTransforms(_boneMatrices);

        foreach (var part in _model.Parts)
        {
            _effect.Texture = part.Texture; // always non-null (magenta fallback)

            device.SetVertexBuffer(part.Vertices);
            device.Indices = part.Indices;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.PrimitiveCount);
            }
        }
    }
}
