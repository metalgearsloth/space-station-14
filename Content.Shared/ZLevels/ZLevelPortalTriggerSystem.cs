using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevels;

public sealed partial class ZLevelPortalTriggerSystem : EntitySystem
{
    private const float CrossingEpsilon = 0.0001f;

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ZLevelPortalSystem _portals = default!;

    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<ZLevelPortalComponent> _portalQuery = default!;
    [Dependency] private EntityQuery<ZLevelPortalTriggerComponent> _triggerQuery = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly List<EntityUid> _scratch = new();
    private readonly Dictionary<EntityUid, EntityUid> _latchedTriggers = new();

    public override void Initialize()
    {
        base.Initialize();
        _transform.OnGlobalMoveEvent += OnMove;
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<ZLevelPortalTriggerComponent, ComponentShutdown>(OnTriggerShutdown);
    }

    public override void Shutdown()
    {
        _transform.OnGlobalMoveEvent -= OnMove;
        _nearby.Clear();
        _scratch.Clear();
        _latchedTriggers.Clear();
        base.Shutdown();
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
        => _latchedTriggers.Remove(args.Entity.Owner);

    private void OnTriggerShutdown(Entity<ZLevelPortalTriggerComponent> entity, ref ComponentShutdown args)
    {
        _scratch.Clear();
        foreach (var (body, trigger) in _latchedTriggers)
        {
            if (trigger == entity.Owner)
                _scratch.Add(body);
        }

        foreach (var body in _scratch)
            _latchedTriggers.Remove(body);
        _scratch.Clear();
    }

    private void OnMove(ref MoveEvent args)
    {
        // Portal traversal changes support and map parentage, so only the server may trigger it. Clients keep normal
        // XY prediction and receive the authoritative z transition through entity state.
        if (!_net.IsServer ||
            args.ParentChanged ||
            args.OnlyRotation ||
            !HasComp<ZLevelPhysicsComponent>(args.Sender) ||
            !_xformQuery.TryComp(args.Sender, out var bodyXform) ||
            !_fixturesQuery.TryComp(args.Sender, out var bodyFixtures) ||
            bodyXform.MapUid is not { } map ||
            !args.OldPosition.EntityId.IsValid())
        {
            return;
        }

        var newWorld = _transform.GetWorldPosition(bodyXform);
        var oldWorld = Vector2.Transform(args.OldPosition.Position, _transform.GetWorldMatrix(args.OldPosition.EntityId));
        if (Vector2.DistanceSquared(oldWorld, newWorld) <= CrossingEpsilon * CrossingEpsilon)
            return;

        RefreshLatch(args.Sender, map, newWorld);
        if (_latchedTriggers.ContainsKey(args.Sender))
            return;

        var sweptBounds = new Box2(Vector2.Min(oldWorld, newWorld), Vector2.Max(oldWorld, newWorld))
            .Enlarged(1f);
        _nearby.Clear();
        _lookup.GetEntitiesIntersecting(
            bodyXform.MapID,
            sweptBounds,
            _nearby,
            LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Sensors);

        _scratch.Clear();
        foreach (var uid in _nearby)
        {
            // The physical blocker is a separate hard fixture. This system only considers the non-hard transition
            // shape and triggers as soon as the body moves into it in the authored direction.
            if (!_triggerQuery.TryComp(uid, out var trigger) ||
                !_portalQuery.TryComp(uid, out var portal) ||
                !_fixturesQuery.TryComp(uid, out var fixtures) ||
                !_xformQuery.TryComp(uid, out var triggerXform) ||
                triggerXform.MapUid != map ||
                Math.Abs(portal.DestinationOffset) != 1 ||
                trigger.Direction.LengthSquared() <= CrossingEpsilon * CrossingEpsilon ||
                !fixtures.Fixtures.TryGetValue(trigger.TransitionFixture, out var fixture) ||
                fixture.Hard ||
                !TryGetTriggerBounds(fixture, out var bounds) ||
                !TouchesTriggerInDirection(args.Sender, oldWorld, newWorld, bodyXform, bodyFixtures, triggerXform, trigger, portal.DestinationOffset, bounds))
            {
                continue;
            }

            _scratch.Add(uid);
        }

        _scratch.Sort();
        foreach (var triggerUid in _scratch)
        {
            _latchedTriggers[args.Sender] = triggerUid;
            var portal = _portalQuery.Comp(triggerUid);
            if (!_portals.TryTraverseZ(args.Sender, (triggerUid, portal), portal.DestinationOffset))
                continue;

            _latchedTriggers.Remove(args.Sender);
            return;
        }
    }

    private void RefreshLatch(EntityUid body, EntityUid map, Vector2 worldPosition)
    {
        if (!_latchedTriggers.TryGetValue(body, out var triggerUid) ||
            !_triggerQuery.TryComp(triggerUid, out var trigger) ||
            !_portalQuery.TryComp(triggerUid, out var portal) ||
            !_fixturesQuery.TryComp(triggerUid, out var fixtures) ||
            !_xformQuery.TryComp(triggerUid, out var triggerXform) ||
            triggerXform.MapUid != map ||
            !fixtures.Fixtures.TryGetValue(trigger.TransitionFixture, out var fixture) ||
            !TryGetTriggerBounds(fixture, out var bounds))
        {
            _latchedTriggers.Remove(body);
            return;
        }

        var direction = Vector2.Normalize(trigger.Direction);
        var local = Vector2.Transform(worldPosition, _transform.GetInvWorldMatrix(triggerXform));
        var projection = Vector2.Dot(local, direction);
        var resetDistance = MathF.Max(0f, trigger.LatchResetDistance);
        var reset = portal.DestinationOffset > 0
            ? projection < ProjectBoundsEdge(bounds, direction, maximum: false) - resetDistance
            : projection > ProjectBoundsEdge(bounds, direction, maximum: true) + resetDistance;
        if (reset)
            _latchedTriggers.Remove(body);
    }

    private bool TouchesTriggerInDirection(
        EntityUid body,
        Vector2 oldWorld,
        Vector2 newWorld,
        TransformComponent bodyXform,
        FixturesComponent bodyFixtures,
        TransformComponent triggerXform,
        ZLevelPortalTriggerComponent trigger,
        int destinationOffset,
        Box2 bounds)
    {
        var inverse = _transform.GetInvWorldMatrix(triggerXform);
        var oldLocal = Vector2.Transform(oldWorld, inverse);
        var newLocal = Vector2.Transform(newWorld, inverse);
        var direction = Vector2.Normalize(trigger.Direction);
        var motion = Vector2.Dot(newLocal - oldLocal, direction);
        var ascending = destinationOffset > 0;
        if (ascending ? motion <= CrossingEpsilon : motion >= -CrossingEpsilon)
            return false;

        return BodyTouchesBounds(body, bodyXform, bodyFixtures, triggerXform, bounds);
    }

    private bool BodyTouchesBounds(
        EntityUid body,
        TransformComponent bodyXform,
        FixturesComponent bodyFixtures,
        TransformComponent triggerXform,
        Box2 bounds)
    {
        var bodyTransform = _physics.GetPhysicsTransform(body, bodyXform);
        var triggerInverse = _transform.GetInvWorldMatrix(triggerXform);
        var triggerBounds = bounds.Enlarged(CrossingEpsilon);

        foreach (var fixture in bodyFixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            for (var child = 0; child < fixture.Shape.ChildCount; child++)
            {
                var worldBounds = fixture.Shape.ComputeAABB(bodyTransform, child);
                var localBounds = triggerInverse.TransformBox(worldBounds);
                if (localBounds.Intersects(triggerBounds))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetTriggerBounds(Fixture fixture, out Box2 bounds)
    {
        if (fixture.Shape is PhysShapeAabb aabb)
        {
            bounds = aabb.LocalBounds;
            return bounds.Width > CrossingEpsilon && bounds.Height > CrossingEpsilon;
        }

        if (fixture.Shape is not PolygonShape polygon || polygon.VertexCount != 4)
        {
            bounds = default;
            return false;
        }

        var min = polygon.Vertices[0];
        var max = min;
        for (var i = 1; i < polygon.VertexCount; i++)
        {
            min = Vector2.Min(min, polygon.Vertices[i]);
            max = Vector2.Max(max, polygon.Vertices[i]);
        }

        if (max.X - min.X <= CrossingEpsilon || max.Y - min.Y <= CrossingEpsilon)
        {
            bounds = default;
            return false;
        }

        var bottomLeft = false;
        var bottomRight = false;
        var topLeft = false;
        var topRight = false;

        for (var i = 0; i < polygon.VertexCount; i++)
        {
            var vertex = polygon.Vertices[i];
            var x = MathF.Abs(vertex.X - min.X) <= CrossingEpsilon
                ? 0
                : MathF.Abs(vertex.X - max.X) <= CrossingEpsilon ? 1 : -1;
            var y = MathF.Abs(vertex.Y - min.Y) <= CrossingEpsilon
                ? 0
                : MathF.Abs(vertex.Y - max.Y) <= CrossingEpsilon ? 1 : -1;
            if (x < 0 || y < 0)
            {
                bounds = default;
                return false;
            }

            switch (x, y)
            {
                case (0, 0) when !bottomLeft:
                    bottomLeft = true;
                    break;
                case (1, 0) when !bottomRight:
                    bottomRight = true;
                    break;
                case (0, 1) when !topLeft:
                    topLeft = true;
                    break;
                case (1, 1) when !topRight:
                    topRight = true;
                    break;
                default:
                    bounds = default;
                    return false;
            }
        }

        bounds = new Box2(min, max);
        return bottomLeft && bottomRight && topLeft && topRight;
    }

    private static float ProjectBoundsEdge(Box2 bounds, Vector2 direction, bool maximum)
    {
        var first = Vector2.Dot(bounds.BottomLeft, direction);
        var second = Vector2.Dot(bounds.BottomRight, direction);
        var third = Vector2.Dot(bounds.TopLeft, direction);
        var fourth = Vector2.Dot(bounds.TopRight, direction);
        return maximum
            ? MathF.Max(MathF.Max(first, second), MathF.Max(third, fourth))
            : MathF.Min(MathF.Min(first, second), MathF.Min(third, fourth));
    }

}
