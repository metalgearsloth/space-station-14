using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;

namespace Content.Client.Movement.Systems;

public sealed partial class ContentEyeSystem : SharedContentEyeSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private ZLevelPhysicsVisualSystem _zPhysicsVisuals = default!;

    private float _zLevelVerticalOffset = CVars.RenderZLevelVerticalOffset.DefaultValue;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, CVars.RenderZLevelVerticalOffset,
            value => _zLevelVerticalOffset = MathF.Max(0f, value), true);
    }

    [SubscribeLocalEvent]
    private void OnZLevelPhysicsGetEyeOffset(Entity<ZLevelPhysicsComponent> entity, ref GetEyeOffsetEvent args)
    {
        var rotation = -_eye.CurrentEye.Rotation;
        var renderPosition = _zPhysicsVisuals.GetVisualRenderPosition(entity.Owner, entity.Comp);
        args.Offset += rotation.RotateVec(new Vector2(0f, renderPosition * _zLevelVerticalOffset));
    }

    public void RequestZoom(EntityUid uid, Vector2 zoom, bool ignoreLimit, bool scalePvs, ContentEyeComponent? content = null)
    {
        if (!Resolve(uid, ref content, false))
            return;

        RaisePredictiveEvent(new RequestTargetZoomEvent()
        {
            TargetZoom = zoom,
            IgnoreLimit = ignoreLimit,
        });

        if (scalePvs)
            RequestPvsScale(Math.Max(zoom.X, zoom.Y));
    }

    public void RequestPvsScale(float scale)
    {
        RaiseNetworkEvent(new RequestPvsScaleEvent(scale));
    }

    public void RequestToggleFov()
    {
        if (_player.LocalEntity is { } player)
            RequestToggleFov(player);
    }

    public void RequestToggleFov(EntityUid uid, EyeComponent? eye = null)
    {
        if (Resolve(uid, ref eye, false))
            RequestEye(!eye.DrawFov, eye.DrawLight);
    }

    public void RequestToggleLight(EntityUid uid, EyeComponent? eye = null)
    {
        if (Resolve(uid, ref eye, false))
            RequestEye(eye.DrawFov, !eye.DrawLight);
    }


    public void RequestEye(bool drawFov, bool drawLight)
    {
        RaisePredictiveEvent(new RequestEyeEvent(drawFov, drawLight));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var eyeEntities = AllEntityQuery<ContentEyeComponent, EyeComponent>();
        while (eyeEntities.MoveNext(out var entity, out ContentEyeComponent? contentComponent, out EyeComponent? eyeComponent))
        {
            UpdateEyeOffset((entity, eyeComponent));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // TODO: Ideally we wouldn't want this to run in both FrameUpdate and Update, but we kind of have to since the visual update happens in FrameUpdate, but interaction update happens in Update. It's a workaround and a better solution should be found.
        var eyeEntities = AllEntityQuery<ContentEyeComponent, EyeComponent>();
        while (eyeEntities.MoveNext(out var entity, out ContentEyeComponent? contentComponent, out EyeComponent? eyeComponent))
        {
            UpdateEyeOffset((entity, eyeComponent));
        }
    }
}
