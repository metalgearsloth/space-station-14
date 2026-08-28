using Content.Shared.Climbing.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevels;

/// <summary>
/// Connects engine z-level physics events to content-specific throwing, landing, and climbing behavior.
/// </summary>
public sealed partial class ZLevelPhysicsContentSystem : EntitySystem
{
    [Dependency] private ZLevelPhysicsSystem _zPhysics = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;

    [Dependency] private EntityQuery<ClimbableComponent> _climbableQuery = default!;
    [Dependency] private EntityQuery<GhostComponent> _ghostQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    [SubscribeLocalEvent]
    private void OnThrown(Entity<ZLevelPhysicsComponent> entity, ref ThrownEvent args)
    {
        if (entity.Comp.Disabled)
            return;

        entity.Comp.Disabled = true;
        DirtyField(entity.Owner, entity.Comp, nameof(ZLevelPhysicsComponent.Disabled));
    }

    [SubscribeLocalEvent]
    private void OnStopThrow(Entity<ZLevelPhysicsComponent> entity, ref StopThrowEvent args)
    {
        if (!entity.Comp.Disabled)
            return;

        entity.Comp.Disabled = false;
        DirtyField(entity.Owner, entity.Comp, nameof(ZLevelPhysicsComponent.Disabled));
        _zPhysics.RefreshGround(entity, true);
        _zPhysics.RefreshBody(entity);
    }

    [SubscribeLocalEvent]
    private void OnImpact(Entity<ZLevelPhysicsComponent> entity, ref ZLevelImpactEvent args)
    {
        if (args.Surface != ZLevelImpactSurface.Floor || _ghostQuery.HasComp(entity.Owner))
            return;

        if (TryComp(entity.Owner, out ThrownItemComponent? thrown) &&
            _physicsQuery.TryComp(entity.Owner, out var physics))
        {
            _thrown.LandComponent(entity.Owner, thrown, physics, thrown.PlayLandSound);
            return;
        }

        var land = new LandEvent(null, true);
        RaiseLocalEvent(entity.Owner, ref land);
    }

    [SubscribeLocalEvent]
    private void OnPreventCollide(Entity<ZLevelPhysicsComponent> entity, ref PreventCollideEvent args)
    {
        if (_ghostQuery.HasComp(entity.Owner))
            return;

        if (_physicsQuery.TryComp(entity.Owner, out var physics) &&
            physics.BodyStatus == BodyStatus.InAir &&
            _climbableQuery.HasComp(args.OtherEntity))
        {
            args.Cancelled = true;
        }
    }
}
