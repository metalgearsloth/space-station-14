using System.Numerics;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Physics;

/// <summary>
/// Draws a texture on top of a joint.
/// </summary>
public sealed class JointVisualsOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private IEntityManager _entManager;

    public JointVisualsOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        var spriteSystem = _entManager.System<SpriteSystem>();
        var joints = _entManager.EntityQueryEnumerator<JointVisualsComponent, TransformComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);

        while (joints.MoveNext(out var uid, out var visuals, out var xform))
        {
            if (!args.TryGetEntityRenderLayer(uid, out var layerA))
                continue;

            if (visuals.Target is not { } other)
                continue;

            if (!xformQuery.TryGetComponent(other, out var otherXform))
                continue;

            if (!args.TryGetEntityRenderLayer(other, out var layerB))
                continue;

            var texture = spriteSystem.Frame0(visuals.Sprite);
            var width = texture.Width / (float)EyeManager.PixelsPerMeter;

            var posA = layerA.Position + layerA.Rotation.RotateVec(visuals.OffsetA);
            var posB = layerB.Position + layerB.Rotation.RotateVec(visuals.OffsetB);
            var diff = posB - posA;
            var length = diff.Length();

            var midPoint = diff / 2f + posA;
            var angle = (posB - posA).ToWorldAngle();
            var box = new Box2(-width / 2f, -length / 2f, width / 2f, length / 2f);
            var rotate = new Box2Rotated(box.Translated(midPoint), angle, midPoint);

            var opacity = Math.Min(layerA.Opacity, layerB.Opacity);
            worldHandle.DrawTextureRect(texture, rotate, Color.White.WithAlpha(opacity));
        }
    }
}
