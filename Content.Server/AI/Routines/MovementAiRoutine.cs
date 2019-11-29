using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.Routines
{
    public sealed class MovementAiRoutine : AiRoutine
    {
        // Route handler
        public IReadOnlyCollection<TileRef> Route => _route;
        private Queue<TileRef> _route = new Queue<TileRef>();
        private GridCoordinates _nextGrid;
        private bool _availableRoute = true;

        public bool RequiresMover => false;

        // Stuck checkers
        private DateTime _lastStuckCheck = DateTime.Now;
        private GridCoordinates _lastPosition = default;

        public bool Arrived => _arrived;
        private bool _arrived = true;

        private IEntity _owner;

        private IMapManager _mapManager;
        private IPathfinder _pathfinder;
        private IEntity _trackedEntity;
        // As long as the target hasn't moved further than this away
        public float TargetTolerance
        {
            get;
            set;
        }
        // How close to the route X / Y (centre of tile, at least currently) before good enough.
        private float _tileTolerance = 0.1f;

        /// <summary>
        /// Called once when the routine is being setup
        /// </summary>
        /// <param name="owner"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            if (!owner.HasComponent<AiControllerComponent>())
            {
                Logger.FatalS("ai_routine", "MovementAiRoutine must have a valid controller on the owner");
                throw new InvalidOperationException();
            }
            _owner = owner;
            _mapManager = IoCManager.Resolve<IMapManager>();
            _pathfinder = IoCManager.Resolve<IPathfinder>();
            TargetTolerance = 2.0f;
        }

        /// <summary>
        /// Will try and wiggle around if it seems like we're stuck
        /// </summary>
        private void AntiStuck()
        {
            if (!((DateTime.Now - _lastStuckCheck).TotalSeconds > 5.0f))
            {
                return;
            }

            _lastStuckCheck = DateTime.Now;

            // Are we actually stuck
            if ((_lastPosition.Position - _owner.Transform.GridPosition.Position).Length < TargetTolerance)
            {
                // Move in a random direction
                _owner.TryGetComponent(out AiControllerComponent mover);
                var robustRandom = IoCManager.Resolve<IRobustRandom>();
                var angle = Angle.FromDegrees(robustRandom.Next(359));
                mover.VelocityDir = angle.ToVec().Normalized;
            }
        }

        /// <summary>
        /// Gets a route to the specified target grid
        /// </summary>
        /// <param name="gridCoordinates"></param>
        public void GetRoute(GridCoordinates gridCoordinates)
        {
            _route.Clear();
            _arrived = false;
            _trackedEntity = null;

            foreach (var tile in _pathfinder.FindPath(_owner.Transform.GridPosition, gridCoordinates))
            {
                _route.Enqueue(tile);
            }

            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Will try and get a route to the target entity. Will try adjacent tiles if necessary
        /// If they move further than the tolerance it will get a new route.
        /// </summary>
        /// <param name="entity"></param>
        public void GetRoute(IEntity entity)
        {
            _route.Clear();
            _arrived = false;
            _trackedEntity = entity;
            GridCoordinates targetGrid = default;

            // If we can't get directly to the entity then try and go adjacent to it
            var entityTile = _mapManager.GetGrid(entity.Transform.GridID).GetTileRef(entity.Transform.GridPosition);
            if (_pathfinder.GetTileCost(entityTile) == 0)
            {
                // Try and get adjacent tiles
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        var neighborTile = _mapManager
                            .GetGrid(entity.Transform.GridID)
                            .GetTileRef(new MapIndices(entityTile.X + x, entityTile.Y + y));
                        if (_pathfinder.GetTileCost(neighborTile) > 0)
                        {
                            targetGrid = _mapManager.GetGrid(neighborTile.GridIndex).GridTileToLocal(neighborTile.GridIndices);
                            break;
                        }
                    }

                    if (targetGrid != default)
                    {
                        break;
                    }
                }
            }

            if (targetGrid == default)
            {
                targetGrid = entity.Transform.GridPosition;
            }

            foreach (var tile in _pathfinder.FindPath(_owner.Transform.GridPosition, targetGrid))
            {
                _route.Enqueue(tile);
            }

            if (_route.Count == 0)
            {
                // Couldn't find a route to target
                _availableRoute = false;
                return;
            }

            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// </summary>
        public void HandleMovement()
        {
            if (!_availableRoute)
            {
                return;
            }
            // If tracking a target need to check if it's moved.
            if (_trackedEntity != null)
            {
                // TODO: Compare entity's original position
                if (_route.Count == 0 && (_trackedEntity.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position)
                    .Length > TargetTolerance)
                {
                    GetRoute(_trackedEntity);
                    return;
                }
            }

            AntiStuck();

            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            _owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            var targetDiff = _nextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > _tileTolerance)
            {
                // Move towards it
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized;
                return;
            }

            // Have we arrived at the final destination
            if (_route.Count == 0)
            {
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = Vector2.Zero;
                _arrived = true;
                return;
            }
            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        public override void Update()
        {
            base.Update();
            HandleMovement();
        }
    }
}
