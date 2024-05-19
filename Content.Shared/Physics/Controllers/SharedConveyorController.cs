using System.Numerics;
using Content.Shared.Conveyor;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Threading;

namespace Content.Shared.Physics.Controllers;

public abstract class SharedConveyorController : VirtualController
{
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] protected readonly EntityLookupSystem Lookup = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] protected readonly SharedPhysicsSystem Physics = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    protected const string ConveyorFixture = "conveyor";

    private static readonly Vector2 _expansion = new Vector2(0.1f, 0.1f);

    private ConveyorJob _job;
    private readonly HashSet<EntityUid> _conveyed = new();

    public override void Initialize()
    {
        _job = new()
        {
            Controller = this,
            Gravity = _gravity,
            Lookup = Lookup,
            Maps = _maps,
            _gridQuery = GetEntityQuery<MapGridComponent>(),
            _physicsQuery = GetEntityQuery<PhysicsComponent>(),
            _xformQuery = GetEntityQuery<TransformComponent>(),
        };

        UpdatesAfter.Add(typeof(SharedMoverController));

        SubscribeLocalEvent<ConveyorComponent, ComponentStartup>(OnConveyorStartup);
        SubscribeLocalEvent<ConveyorComponent, ComponentShutdown>(OnConveyorShutdown);
        SubscribeLocalEvent<ConveyorComponent, StartCollideEvent>(OnConveyorStartCollide);
        SubscribeLocalEvent<ConveyorComponent, EndCollideEvent>(OnConveyorEndCollide);

        base.Initialize();
    }

    protected virtual void OnConveyorStartup(Entity<ConveyorComponent> ent, ref ComponentStartup args)
    {
        _job.Conveyors.Add(ent);
    }

    protected virtual void OnConveyorShutdown(Entity<ConveyorComponent> ent, ref ComponentShutdown args)
    {
        _job.Conveyors.Remove(ent);
    }

    private void OnConveyorStartCollide(EntityUid uid, ConveyorComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (args.OtherBody.BodyType == BodyType.Static || component.State == ConveyorState.Off)
            return;

        if (!component.Intersecting.Add(otherUid))
            return;

        Dirty(uid, component);
    }

    private void OnConveyorEndCollide(EntityUid uid, ConveyorComponent component, ref EndCollideEvent args)
    {
        if (!component.Intersecting.Remove(args.OtherEntity))
            return;

        Dirty(uid, component);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _conveyed.Clear();
        _job.LocalPositions.Clear();
        _job.Prediction = prediction;
        _job.FrameTime = frameTime;
        _parallel.ProcessNow(_job, _job.Conveyors.Count);

        foreach (var (entity, position) in _job.LocalPositions)
        {
            // Parallel will cause a 1-tick delay for some stuff
            // e.g. if something gets conveyed and turns into a bomb, most of the time it shouldn't be noticeable
            // and we only need to check deletion.
            if (Deleted(entity.Owner))
                continue;

            if (!_conveyed.Add(entity.Owner))
                continue;

            TransformSystem.SetLocalPosition(entity.Owner, position, entity.Comp2);

            // Force it awake for collisionwake reasons.
            Physics.SetAwake((entity, entity.Comp1), true);
            Physics.SetSleepTime(entity.Comp1, 0f);
        }
    }

    private static Vector2 Convey(Vector2 direction, float speed, float frameTime, Vector2 itemRelative)
    {
        if (speed == 0 || direction.Length() == 0)
            return Vector2.Zero;

        /*
         * Basic idea: if the item is not in the middle of the conveyor in the direction that the conveyor is running,
         * move the item towards the middle. Otherwise, move the item along the direction. This lets conveyors pick up
         * items that are not perfectly aligned in the middle, and also makes corner cuts work.
         *
         * We do this by computing the projection of 'itemRelative' on 'direction', yielding a vector 'p' in the direction
         * of 'direction'. We also compute the rejection 'r'. If the magnitude of 'r' is not (near) zero, then the item
         * is not on the centerline.
         */

        var p = direction * (Vector2.Dot(itemRelative, direction) / Vector2.Dot(direction, direction));
        var r = itemRelative - p;

        if (r.Length() < 0.1)
        {
            var velocity = direction * speed;
            return velocity * frameTime;
        }
        else
        {
            // Give a slight nudge in the direction of the conveyor to prevent
            // to collidable objects (e.g. crates) on the locker from getting stuck
            // pushing each other when rounding a corner.
            var velocity = (r + direction*0.2f).Normalized() * speed;
            return velocity * frameTime;
        }
    }

    public bool CanRun(ConveyorComponent component)
    {
        // Use an event for conveyors to know what needs to run
        return component.Speed > 0f && component.State != ConveyorState.Off && component.Powered;
    }

    private record struct ConveyorJob() : IParallelRobustJob
    {
        public int BatchSize => 16;

        public SharedConveyorController Controller;
        public EntityLookupSystem Lookup;
        public SharedGravitySystem Gravity;
        public SharedMapSystem Maps;

        public EntityQuery<MapGridComponent> _gridQuery;
        public EntityQuery<PhysicsComponent> _physicsQuery;
        public EntityQuery<TransformComponent> _xformQuery;

        public readonly List<Entity<ConveyorComponent>> Conveyors = new();
        public readonly List<(Entity<PhysicsComponent, TransformComponent>, Vector2)> LocalPositions = new();

        public float FrameTime;
        public bool Prediction;

        public void Execute(int index)
        {
            var conveyor = Conveyors[index];
            var comp = conveyor.Comp;

            if (comp.Intersecting.Count == 0 || !Controller.CanRun(comp))
                return;

            if (!_xformQuery.TryGetComponent(conveyor, out var xform) || !_gridQuery.TryComp(xform.GridUid, out var grid))
                return;

            var speed = comp.Speed;
            var conveyorPos = xform.LocalPosition;
            var conveyorRot = xform.LocalRotation;

            conveyorRot += comp.Angle;

            if (comp.State == ConveyorState.Reverse)
                conveyorRot += MathF.PI;

            var direction = conveyorRot.ToWorldVec();

            foreach (var (entity, transform, body) in GetEntitiesToMove(comp, xform, (xform.GridUid.Value, grid)))
            {
                if (Prediction && !body.Predict)
                    continue;

                var localPos = transform.LocalPosition;
                var itemRelative = conveyorPos - localPos;

                localPos += Convey(direction, speed, FrameTime, itemRelative);

                lock (LocalPositions)
                {
                    LocalPositions.Add(((entity, body, transform), localPos));
                }
            }
        }

        private IEnumerable<(EntityUid, TransformComponent, PhysicsComponent)> GetEntitiesToMove(
            ConveyorComponent comp,
            TransformComponent xform,
            Entity<MapGridComponent> grid)
        {
            // Check if the thing's centre overlaps the grid tile.
            var tile = Maps.GetTileRef(grid.Owner, grid.Comp, xform.Coordinates);
            var conveyorBounds = Lookup.GetLocalBounds(tile, grid.Comp.TileSize);

            foreach (var entity in comp.Intersecting)
            {
                if (!_xformQuery.TryGetComponent(entity, out var entityXform) || entityXform.ParentUid != xform.GridUid!.Value)
                    continue;

                if (!_physicsQuery.TryGetComponent(entity, out var physics) || physics.BodyType == BodyType.Static || physics.BodyStatus == BodyStatus.InAir || Gravity.IsWeightless(entity, physics, entityXform))
                    continue;

                // Yes there's still going to be the occasional rounding issue where it stops getting conveyed
                // When you fix the corner issue that will fix this anyway.
                var gridAABB = new Box2(entityXform.LocalPosition - _expansion, entityXform.LocalPosition + _expansion);

                if (!conveyorBounds.Intersects(gridAABB))
                    continue;

                yield return (entity, entityXform, physics);
            }
        }
    }
}
