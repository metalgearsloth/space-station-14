using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.NPC;

public sealed partial class NPCSteeringSystem : SharedNPCSteeringSystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public bool DebugEnabled
    {
        get => _debugEnabled;
        set
        {
            if (_debugEnabled == value)
                return;

            _debugEnabled = value;

            if (_debugEnabled)
            {
                _overlay.AddOverlay(new NPCSteeringOverlay(EntityManager));
                RaiseNetworkEvent(new RequestNPCSteeringDebugEvent()
                {
                    Enabled = true
                });
            }
            else
            {
                _overlay.RemoveOverlay<NPCSteeringOverlay>();
                RaiseNetworkEvent(new RequestNPCSteeringDebugEvent()
                {
                    Enabled = false
                });

                var query = AllEntityQuery<NPCSteeringComponent>();
                while (query.MoveNext(out var uid, out var npc))
                {
                    RemCompDeferred<NPCSteeringComponent>(uid);
                }
            }
        }
    }

    private bool _debugEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NPCSteeringDebugEvent>(OnDebugEvent);
    }

    private void OnDebugEvent(NPCSteeringDebugEvent ev)
    {
        if (!DebugEnabled)
            return;

        foreach (var data in ev.Data)
        {
            var entity = GetEntity(data.EntityUid);

            if (!Exists(entity))
                continue;

            var comp = EnsureComp<NPCSteeringComponent>(entity);
            comp.Direction = data.Direction;
            comp.DangerMap = data.Danger;
            comp.InterestMap = data.Interest;
            comp.DangerPoints = data.DangerPoints;
        }
    }
}

public sealed class NPCSteeringOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entManager;
    private readonly TransformSystem _transformSystem;

    public NPCSteeringOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transformSystem = _entManager.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (comp, mover, xform) in _entManager.EntityQuery<NPCSteeringComponent, InputMoverComponent, TransformComponent>(true))
        {
            if (!args.TryGetEntityRenderLayer(comp.Owner, out var renderLayer))
                continue;

            var (canonicalPosition, _) = _transformSystem.GetWorldPositionRotation(xform);
            var worldPos = renderLayer.Position;
            var canonicalToPresented = Matrix3x2.CreateTranslation(worldPos - canonicalPosition);
            var rotationOffset = mover.RelativeRotation;

            if (mover.RelativeEntity is { } relative)
            {
                rotationOffset += _transformSystem.GetRenderWorldRotation(relative);
                if (args.TryGetEntityRenderMatrix(relative, out var renderMatrix, out _) &&
                    Matrix3x2.Invert(_transformSystem.GetWorldMatrix(relative), out var invCanonicalMatrix))
                {
                    canonicalToPresented = invCanonicalMatrix * renderMatrix;
                }
            }

            if (!args.WorldAABB.Contains(worldPos))
                continue;

            args.WorldHandle.DrawCircle(worldPos, 1f, Color.Green.WithAlpha(renderLayer.Opacity), false);

            foreach (var point in comp.DangerPoints)
            {
                var presentedPoint = Vector2.Transform(point, canonicalToPresented);
                args.WorldHandle.DrawCircle(presentedPoint, 0.1f, Color.Red.WithAlpha(0.6f * renderLayer.Opacity));
            }

            for (var i = 0; i < SharedNPCSteeringSystem.InterestDirections; i++)
            {
                var danger = comp.DangerMap[i];
                var interest = comp.InterestMap[i];
                var angle = Angle.FromDegrees(i * (360 / SharedNPCSteeringSystem.InterestDirections));
                args.WorldHandle.DrawLine(worldPos, worldPos + (rotationOffset + angle).RotateVec(new Vector2(interest, 0f)), Color.LimeGreen.WithAlpha(renderLayer.Opacity));
                args.WorldHandle.DrawLine(worldPos, worldPos + (rotationOffset + angle).RotateVec(new Vector2(danger, 0f)), Color.Red.WithAlpha(renderLayer.Opacity));
            }

            args.WorldHandle.DrawLine(worldPos, worldPos + rotationOffset.RotateVec(comp.Direction), Color.Cyan.WithAlpha(renderLayer.Opacity));
        }
    }
}
