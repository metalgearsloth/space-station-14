using Content.Shared.Throwing;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevels;

/// <summary>
/// Handles z-level motion.
/// </summary>
public sealed partial class ZLevelPhysicsContentSystem : EntitySystem
{
    [Dependency] private ZLevelPhysicsSystem _zPhysics = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<ThrownItemComponent> _thrownQuery = default!;
    [Dependency] private EntityQuery<ZLevelMapComponent> _zMapQuery = default!;

    [SubscribeLocalEvent]
    private void OnThrown(Entity<ZLevelPhysicsComponent> entity, ref ThrownEvent args)
    {
        if (!entity.Comp.Fallable ||
            !entity.Comp.VelocityGravity ||
            entity.Comp.GravityMultiplier <= 0f ||
            !_thrownQuery.TryComp(entity.Owner, out var thrown) ||
            Transform(entity.Owner).MapUid is not { } mapUid ||
            !_zMapQuery.HasComp(mapUid))
        {
            return;
        }

        thrown.VerticalPhysics = true;
        Dirty(entity.Owner, thrown);

        var duration = thrown.LandTime - thrown.ThrownTime;
        if (duration is { } flightTime && flightTime > TimeSpan.Zero)
        {
            var launchVelocity = _zPhysics.Gravity *
                                 entity.Comp.GravityMultiplier *
                                 (float) flightTime.TotalSeconds /
                                 2f;
            _zPhysics.SetZVelocity(entity, MathF.Max(entity.Comp.Velocity, launchVelocity));
        }

        _zPhysics.RefreshSupport(entity, true);
        _zPhysics.RefreshBody(entity);
    }

    [SubscribeLocalEvent]
    private void OnLanding(Entity<ZLevelPhysicsComponent> entity, ref ZLevelLandingEvent args)
    {
        if (args.Surface != ZLevelImpactSurface.Floor)
            return;

        if (_thrownQuery.TryComp(entity.Owner, out var thrown) &&
            _physicsQuery.TryComp(entity.Owner, out var physics))
        {
            // Z support can land throws on a weightless map.
            _thrown.LandComponent(entity.Owner, thrown, physics, thrown.PlayLandSound, ignoreWeightlessness: true);
            if (thrown.Landed && !thrown.Deleted)
                _thrown.StopThrow(entity.Owner, thrown);
            return;
        }

        var land = new LandEvent(null, true);
        RaiseLocalEvent(entity.Owner, ref land);
    }
}
