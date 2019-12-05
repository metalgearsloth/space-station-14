using System;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.AI.Routines.Movers
{
    /// <summary>
    /// Pathfinds to target entity / grid and handles movement there
    /// </summary>
    public class AiMover
    {
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IPathfinder _pathfinder;
#pragma warning restore 649

        // General settings

        // Percentage of walk / sprint speed to move at.
        // Should generally be 1.0 but some stuff like the idler might move slower.
        public float Speed
        {
            get => _speed;
            set
            {
                // TODO: Clamp
                if (_speed > 1.0f)
                {
                    _speed = 1.0f;
                    return;
                }

                // Not falling for that HL2 infinite negative speed
                if (_speed < 0.0f)
                {
                    _speed = 0.0f;
                    return;
                }

                _speed = value;
            }
        }

        private float _speed = 1.0f;
        public IEntity Owner { get; }

        // These are to do with the tile-to-tile movement
        public IReadOnlyCollection<TileRef> Route => _route;
        private readonly Queue<TileRef> _route = new Queue<TileRef>();
        private GridCoordinates NextGrid;

        // How close to the route X / Y (centre of tile, at least currently) before good enough.
        private float TileTolerance = 0.2f;

        // Stuck checkers
        private float _stuckTimerRemaining = 2.0f;
        private GridCoordinates OurLastPosition;
        private float _antiStuckTryTimer = 0.2f;
        // Anti-stuck measures. See the AntiStuck() method for more details
        private bool _tryingAntiStuck = false;
        public bool IsStuck => _isStuck;
        private bool _isStuck;
        private AntiStuckMethods AntiStuckMethod = AntiStuckMethods.PhaseThrough;

        public bool Arrived => _arrived;
        private bool _arrived = true;

        /// <summary>
        /// How close we need to be to the target position
        /// Doesn't affect the pathfinder
        /// </summary>
        public float Range { get; set; } = InteractionSystem.InteractionRange - 0.5f;

        /// <summary>
        /// How close the pathfinder has to get before it returns a route.
        /// Generally this may need to be overridden if the desired item is on a table for example and we
        /// just want to get into interaction range.
        /// </summary>
        public float PathfinderRange = InteractionSystem.InteractionRange - 0.5f; // Should get all 8 adjacent tiles

        // Entity movement related
        public IEntity TargetEntity { get; set; }
        private float _entityRouteThrottle = 2.0f;
        /// <summary>
        /// How close the target is allowed to move around before we get a new route
        /// </summary>
        public float TargetMovementTolerance = 2.0f;

        // Grid movement related
        private GridCoordinates _targetGrid;

        public AiMover(IEntity owner)
        {
            Owner = owner;
            IoCManager.InjectDependencies(this);
            if (!owner.HasComponent<AiControllerComponent>())
            {
                throw new InvalidOperationException("AI Mover must have controller");
            }
            OurLastPosition = default;
        }

        /// <summary>
        /// Will try and get around obstacles if stuck
        /// </summary>
        private void AntiStuck(float frameTime)
        {
            // TODO: More work because these are sketchy

            // First check if we're still in a stuck state from last frame
            if (IsStuck)
            {
                switch (AntiStuckMethod)
                {
                    case AntiStuckMethods.Jiggle:
                        if (!_tryingAntiStuck)
                        {
                            var randomRange = IoCManager.Resolve<IRobustRandom>().Next(0, 359);
                            var angle = Angle.FromDegrees(randomRange);
                            Owner.TryGetComponent(out AiControllerComponent mover);
                            mover.VelocityDir = angle.ToVec().Normalized * Speed;
                            _tryingAntiStuck = true;
                        }

                        if (_antiStuckTryTimer > 0)
                        {
                            break;
                        }

                        _isStuck = false;
                        break;
                    case AntiStuckMethods.PhaseThrough:
                        if (Owner.TryGetComponent(out CollidableComponent collidableComponent))
                        {
                            // TODO Fix this because they are yeeting themselves when they charge
                            if (!_tryingAntiStuck)
                            {
                                // TODO: If something updates this this will fuck it
                                collidableComponent.CollisionEnabled = false;
                                Logger.DebugS("ai", $"{Owner} became stuck, turning off collidable temporarily");
                                _tryingAntiStuck = true;
                            }

                            if (_antiStuckTryTimer > 0)
                            {
                                break;
                            }
                            Logger.DebugS("ai", $"Anti-stuck turning back on collidable for {Owner}");
                            collidableComponent.CollisionEnabled = true;
                            _isStuck = false;
                        }
                        break;
                    case AntiStuckMethods.Teleport:
                        Owner.Transform.DetachParent();
                        Owner.Transform.GridPosition = NextGrid;
                        _isStuck = false;
                        Logger.DebugS("ai", $"Teleported {Owner} to {NextGrid} for anti-stuck");
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            _stuckTimerRemaining -= frameTime;
            _antiStuckTryTimer -= frameTime;

            // Stuck check cooldown
            if (_stuckTimerRemaining > 0.0f)
            {
                return;
            }

            _tryingAntiStuck = false;
            _stuckTimerRemaining = 2.0f;
            _antiStuckTryTimer = 0.5f;

            // Are we actually stuck
            if ((OurLastPosition.Position - Owner.Transform.GridPosition.Position).Length < TileTolerance)
            {
                _isStuck = true;
                Logger.DebugS("ai", $"{Owner} is stuck at {Owner.Transform.GridPosition}");
            }

            // TODO: If we've been stuck say 5 seconds re-route?

            OurLastPosition = Owner.Transform.GridPosition;
        }

        /// <summary>
        /// Tells this we don't need to keep moving and resets everything
        /// </summary>
        public void HaveArrived()
        {
            _route.Clear();
            _arrived = true;
            Owner.TryGetComponent(out AiControllerComponent mover);
            mover.VelocityDir = Vector2.Zero;
        }

        /// <summary>
        /// Will move the AI towards the next position
        /// </summary>
        /// <returns>true if movement to be done</returns>
        private bool TryMove()
        {
            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            Owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            var targetDiff = NextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > TileTolerance)
            {
                // Move towards it
                Owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized * Speed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Will try and get a route to the target grid. Will try adjacent tiles if necessary
        /// If they move further than the tolerance it will get a new route.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="proximity">If the entity's tile is untraversable then should we get close to it</param>
        public void GetRoute()
        {
            HaveArrived();
            _arrived = false;

            var route = _pathfinder.FindPath(Owner.Transform.GridPosition, _targetGrid, PathfinderRange);

            if (route.Count <= 1)
            {
                // Couldn't find a route to target
                return;
            }

            foreach (var tile in route)
            {
                _route.Enqueue(tile);
            }

            // Because the entity may be half on 2 tiles we'll just cut out the first tile.
            // This may not be the best solution but sometimes if the AI is chasing for example it will
            // stutter backwards to the first tile again.
            _route.Dequeue();

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Tries and moves to the target grid. Gets a route if one needed
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="frameTime"></param>
        /// <param name="entity"></param>
        /// <returns>true if in range</returns>
        public void MoveToGrid(GridCoordinates grid, float frameTime)
        {
            TargetEntity = null;

            if ((Owner.Transform.GridPosition.Position - grid.Position).Length < Range)
            {
                return;
            }

            _targetGrid = grid;

            // If no route and entity is out of range
            if (Arrived)
            {
                GetRoute();
            }

            HandleMovement(frameTime);
        }

        /// <summary>
        /// Tries and moves to the target entity. Gets a route if one needed
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="frameTime"></param>
        /// <returns>true if in range</returns>
        public void MoveToEntity(IEntity entity, float frameTime)
        {
            if ((Owner.Transform.GridPosition.Position - entity.Transform.GridPosition.Position).Length < Range)
            {
                return;
            }

            TargetEntity = entity;

            if (_targetGrid == default)
            {
                _targetGrid = TargetEntity.Transform.GridPosition;
            }

            if (Arrived)
            {
                GetRoute();
                return;
            }

            if ((TargetEntity.Transform.GridPosition.Position - _targetGrid.Position).Length >
                TargetMovementTolerance)
            {
                _targetGrid = TargetEntity.Transform.GridPosition;
                GetRoute();
                return;
            }

            HandleMovement(frameTime);
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// Other routines should call GetRoute first
        /// </summary>
        public void HandleMovement(float frameTime)
        {
            if (Arrived)
            {
                return;
            }

            // If the entity's moving we may run into it so check that
            if ((TargetEntity.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length <
                Range)
            {
                HaveArrived();
                return;
            }

            _entityRouteThrottle -= frameTime;

            // Throttler - Check if we need to re-assess the route.
            if (_entityRouteThrottle <= 0)
            {
                _entityRouteThrottle = 1.0f;

                if (_mapManager.GetGrid(TargetEntity.Transform.GridID).GetTileRef(TargetEntity.Transform.GridPosition)
                    .Tile.IsEmpty)
                {
                    // TODO: No path. Maybe use an event?
                    return;
                }

                if ((_targetGrid.Position - TargetEntity.Transform.GridPosition.Position).Length > TargetMovementTolerance)
                {
                    GetRoute();
                    return;
                }
            }

            AntiStuck(frameTime);
            if (IsStuck)
            {
                return;
            }

            if (TryMove())
            {
                return;
            }

            // If we've expended the route and gotten this far that must mean we're close?
            if (_route.Count == 0)
            {
                NextGrid = TargetEntity.Transform.GridPosition;
                return;
            }

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }
    }

    public enum AntiStuckMethods
    {
        Jiggle, // Just pick a random direction for a bit and hope for the best
        Teleport, // The Half-Life 2 method
        PhaseThrough, // Just makes it non-collidable
    }
}
