using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevels;

public sealed partial class ZLevelPortalSystem : EntitySystem
{
    private const float PairPositionEpsilon = 0.001f;

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ZLevelPhysicsSystem _zPhysics = default!;
    [Dependency] private ZLevelSystem _zLevels = default!;

    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<ZLevelPhysicsComponent> _zPhysicsQuery = default!;
    [Dependency] private EntityQuery<ZLevelPortalComponent> _portalQuery = default!;

    private readonly HashSet<FixtureProxy> _destinationFixtures = new();
    private readonly HashSet<EntityUid> _pendingPairing = new();
    private readonly List<EntityUid> _pairingScratch = new();
    private bool _creatingCounterpart;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLevelPortalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZLevelPortalComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<ZLevelPortalComponent> entity, ref MapInitEvent args)
    {
        if (!_net.IsServer || _creatingCounterpart || entity.Comp.CounterpartPrototype == null)
            return;

        // Defer creation out of MapInit so the source and linked destination grid transforms have finished their
        // own map-init bookkeeping before we spawn an anchored counterpart onto them.
        _pendingPairing.Add(entity.Owner);
    }

    private void OnShutdown(Entity<ZLevelPortalComponent> entity, ref ComponentShutdown args)
    {
        _pendingPairing.Remove(entity.Owner);
        if (!_net.IsServer || entity.Comp.PairedEndpoint is not { } paired)
            return;

        if (!_portalQuery.TryComp(paired, out var pairedComp) || pairedComp.PairedEndpoint != entity.Owner)
            return;

        pairedComp.PairedEndpoint = null;

        // Generated endpoints share their source's lifetime. Removing a generated endpoint itself (for example as
        // part of grid deletion) only clears the surviving source reference and never recursively deletes it.
        if (pairedComp.GeneratedBy == entity.Owner && entity.Comp.GeneratedBy == null)
            QueueDel(paired);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_net.IsServer || _pendingPairing.Count == 0)
            return;

        _pairingScratch.Clear();
        foreach (var uid in _pendingPairing)
            _pairingScratch.Add(uid);

        foreach (var uid in _pairingScratch)
        {
            if (!_portalQuery.TryComp(uid, out var portal) || portal.CounterpartPrototype == null)
            {
                _pendingPairing.Remove(uid);
                continue;
            }

            if (TryEnsureCounterpart((uid, portal)))
                _pendingPairing.Remove(uid);
        }
    }

    /// <summary>
    /// Resolves and validates an adjacent destination without moving the entity.
    /// </summary>
    public bool CanTraverseZ(EntityUid entity, Entity<ZLevelPortalComponent?> portal, int offset)
    {
        if (!_net.IsServer ||
            !Resolve(portal, ref portal.Comp, false) ||
            portal.Comp.DestinationOffset != offset ||
            Math.Abs(offset) != 1 ||
            !_xformQuery.TryComp(entity, out var xform) ||
            !TryResolveDestination(
                (portal.Owner, portal.Comp),
                xform,
                offset,
                out var targetMap,
                out var targetMapId))
        {
            return false;
        }

        return !HasDestinationCollision(entity, xform, (portal.Owner, portal.Comp), targetMap, targetMapId);
    }

    /// <summary>
    /// Authoritatively traverses one z-level while preserving canonical XY. Linked destination grids are preferred;
    /// otherwise the engine move falls back to the corresponding destination-map position.
    /// </summary>
    public bool TryTraverseZ(EntityUid entity, Entity<ZLevelPortalComponent?> portal, int offset)
    {
        if (!CanTraverseZ(entity, portal, offset) ||
            !_zLevels.TryMoveEntityToMapOffset(entity, offset, out var movedMap) ||
            movedMap is not { } targetMap)
        {
            ShowBlockedPopup(entity);
            return false;
        }

        // A portal is an instantaneous level change, so finish on the destination plane. The render-pose system
        // retains the last displayed source pose and smooths this authoritative cross-parent correction.
        if (TryComp(entity, out ZLevelPresentationComponent? presentation))
            _zPhysics.SetLocalHeight((entity, presentation), 0f);

        if (_zPhysicsQuery.TryComp(entity, out var vertical))
        {
            _zPhysics.RefreshSupport((entity, vertical));
            _zPhysics.RefreshBody((entity, vertical));
        }

        var moved = new ZLevelMapMoveEvent(offset, targetMap);
        RaiseLocalEvent(entity, ref moved);
        return true;
    }

    private void ShowBlockedPopup(EntityUid entity)
    {
        if (!_net.IsServer)
            return;

        _popup.PopupEntity(Loc.GetString("z-level-portal-blocked"), entity, entity, PopupType.SmallCaution);
    }

    /// <summary>
    /// Returns a live, reciprocal adjacent endpoint link.
    /// </summary>
    public bool TryGetPairedEndpoint(
        Entity<ZLevelPortalComponent?> portal,
        out Entity<ZLevelPortalComponent> paired)
    {
        paired = default;
        if (!Resolve(portal, ref portal.Comp, false) ||
            portal.Comp.PairedEndpoint is not { } pairedUid ||
            !_portalQuery.TryComp(pairedUid, out var pairedComp) ||
            pairedComp.PairedEndpoint != portal.Owner ||
            pairedComp.DestinationOffset != -portal.Comp.DestinationOffset)
        {
            return false;
        }

        paired = (pairedUid, pairedComp);
        return true;
    }

    private bool TryResolveDestination(
        Entity<ZLevelPortalComponent> portal,
        TransformComponent entityXform,
        int offset,
        out EntityUid targetMap,
        out MapId targetMapId)
    {
        targetMap = default;
        targetMapId = MapId.Nullspace;
        if (entityXform.MapUid is not { } currentMap ||
            !_zLevels.TryGetMapOffset(currentMap, offset, out var maybeTargetMap) ||
            maybeTargetMap is not { } resolvedMap ||
            !_mapQuery.TryComp(resolvedMap, out var mapComp))
        {
            return false;
        }

        // A configured pair is part of the portal contract. Reject stale/cross-network links rather than moving to a
        // different adjacent map than the one containing the endpoint.
        if (portal.Comp.PairedEndpoint is { } paired)
        {
            if (!_xformQuery.TryComp(paired, out var pairedXform) || pairedXform.MapUid != resolvedMap)
                return false;
        }

        targetMap = resolvedMap;
        targetMapId = mapComp.MapId;
        return true;
    }

    private bool HasDestinationCollision(
        EntityUid body,
        TransformComponent bodyXform,
        Entity<ZLevelPortalComponent> portal,
        EntityUid targetMap,
        MapId targetMapId)
    {
        if (!_physicsQuery.TryComp(body, out var bodyPhysics) ||
            !_fixturesQuery.TryComp(body, out var bodyFixtures) ||
            !bodyPhysics.CanCollide ||
            !bodyPhysics.Hard)
        {
            return false;
        }

        var destinationTransform = _physics.GetPhysicsTransform(body, bodyXform);
        foreach (var bodyFixture in bodyFixtures.Fixtures.Values)
        {
            if (!bodyFixture.Hard)
                continue;

            var query = new FixtureQueryArgs(
                new QueryFilter
                {
                    LayerBits = bodyFixture.CollisionLayer,
                    MaskBits = bodyFixture.CollisionMask,
                    Flags = QueryFlags.Dynamic | QueryFlags.Static,
                    IsIgnored = candidate => IsDestinationCollisionIgnored(candidate, body, portal),
                },
                Approximate: false,
                IgnoreShapeSkin: true);

            _destinationFixtures.Clear();
            for (var child = 0; child < bodyFixture.Shape.ChildCount; child++)
            {
                _lookup.GetFixturesIntersecting(
                    targetMapId,
                    bodyFixture.Shape,
                    child,
                    destinationTransform,
                    _destinationFixtures,
                    query);
            }

            foreach (var candidate in _destinationFixtures)
            {
                if (candidate.Entity == body ||
                    IsDestinationCollisionIgnored(candidate.Entity, body, portal) ||
                    candidate.Xform.MapUid != targetMap ||
                    !candidate.Fixture.Hard ||
                    !candidate.Body.CanCollide ||
                    !candidate.Body.Hard)
                {
                    continue;
                }

                if ((bodyFixture.CollisionMask & candidate.Fixture.CollisionLayer) != 0 ||
                    (bodyFixture.CollisionLayer & candidate.Fixture.CollisionMask) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDestinationCollisionIgnored(
        EntityUid candidate,
        EntityUid body,
        Entity<ZLevelPortalComponent> portal)
    {
        return candidate == body ||
               candidate == portal.Owner ||
               candidate == portal.Comp.PairedEndpoint;
    }

    private bool TryEnsureCounterpart(Entity<ZLevelPortalComponent> portal)
    {
        // Counterparts are content-authored endpoints on the adjacent z map. Generation is guarded so the spawned
        // endpoint can initialize without recursively creating another copy.
        if (portal.Comp.CounterpartPrototype is not { } counterpartPrototype ||
            Math.Abs(portal.Comp.DestinationOffset) != 1 ||
            !_xformQuery.TryComp(portal.Owner, out var xform) ||
            xform.MapUid is not { } currentMap ||
            !_zLevels.TryGetMapOffset(currentMap, portal.Comp.DestinationOffset, out var maybeTargetMap) ||
            maybeTargetMap is not { } targetMap ||
            !_mapQuery.HasComp(targetMap))
        {
            return false;
        }

        if (TryGetPairedEndpoint((portal.Owner, (ZLevelPortalComponent?) portal.Comp), out var existingPair))
            return Transform(existingPair).MapUid == targetMap;

        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(xform);
        var parent = ResolveDestinationParent(xform.GridUid, targetMap, portal.Comp.DestinationOffset);
        var destination = GetDestinationCoordinates(parent, worldPosition);

        if (TryFindCompatibleCounterpart(portal, targetMap, worldPosition, worldRotation, out var manual))
        {
            LinkEndpoints(portal, manual);
            return true;
        }

        _creatingCounterpart = true;
        EntityUid counterpart;
        try
        {
            counterpart = SpawnAttachedTo(
                counterpartPrototype,
                destination,
                rotation: worldRotation - _transform.GetWorldRotation(parent));
        }
        finally
        {
            _creatingCounterpart = false;
        }

        if (!_portalQuery.TryComp(counterpart, out var counterpartComp) ||
            counterpartComp.DestinationOffset != -portal.Comp.DestinationOffset)
        {
            QueueDel(counterpart);
            return false;
        }

        counterpartComp.GeneratedBy = portal.Owner;
        LinkEndpoints(portal, (counterpart, counterpartComp));
        return true;
    }

    private EntityUid ResolveDestinationParent(EntityUid? sourceGrid, EntityUid targetMap, int offset)
    {
        // Preserve canonical map coordinates, but attach to the corresponding linked grid when Robust can resolve
        // one. If no linked grid exists the endpoint stays on the destination map.
        if (sourceGrid is not { } grid)
            return targetMap;

        var linked = offset > 0
            ? _zLevels.TryGetGridAbove(grid, out var destinationGrid)
            : _zLevels.TryGetGridBelow(grid, out destinationGrid);
        return linked && destinationGrid is { } destination && _transform.GetMap(destination) == targetMap
            ? destination
            : targetMap;
    }

    private EntityCoordinates GetDestinationCoordinates(EntityUid parent, Vector2 worldPosition)
    {
        if (_mapQuery.HasComp(parent))
            return new EntityCoordinates(parent, worldPosition);

        var localPosition = Vector2.Transform(worldPosition, _transform.GetInvWorldMatrix(parent));
        return new EntityCoordinates(parent, localPosition);
    }

    private bool TryFindCompatibleCounterpart(
        Entity<ZLevelPortalComponent> source,
        EntityUid targetMap,
        Vector2 worldPosition,
        Angle worldRotation,
        out Entity<ZLevelPortalComponent> counterpart)
    {
        counterpart = default;
        var sourcePrototype = MetaData(source).EntityPrototype?.ID;
        var query = EntityQueryEnumerator<ZLevelPortalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var portal, out var xform))
        {
            if (uid == source.Owner ||
                portal.PairedEndpoint != null ||
                portal.DestinationOffset != -source.Comp.DestinationOffset ||
                xform.MapUid != targetMap ||
                MetaData(uid).EntityPrototype?.ID != source.Comp.CounterpartPrototype ||
                (portal.CounterpartPrototype != null && portal.CounterpartPrototype != sourcePrototype) ||
                Vector2.DistanceSquared(_transform.GetWorldPosition(xform), worldPosition) >
                    PairPositionEpsilon * PairPositionEpsilon ||
                !MathHelper.CloseTo(_transform.GetWorldRotation(xform).Theta, worldRotation.Theta))
            {
                continue;
            }

            counterpart = (uid, portal);
            return true;
        }

        return false;
    }

    private void LinkEndpoints(
        Entity<ZLevelPortalComponent> first,
        Entity<ZLevelPortalComponent> second)
    {
        first.Comp.PairedEndpoint = second.Owner;
        second.Comp.PairedEndpoint = first.Owner;
        _pendingPairing.Remove(first.Owner);
        _pendingPairing.Remove(second.Owner);
    }
}
