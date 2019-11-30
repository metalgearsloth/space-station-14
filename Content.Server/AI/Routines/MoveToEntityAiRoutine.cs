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
    public sealed class MoveToEntityAiRoutine : AiRoutine
    {
        // Route handler
        public IReadOnlyCollection<TileRef> Route => _route;
        private Queue<TileRef> _route = new Queue<TileRef>();
        private GridCoordinates _nextGrid;
        private DateTime _entityRouteThrottle = DateTime.Now - TimeSpan.FromSeconds(5.0f);

        public bool MovementAllowed { get; set; }

        public override bool RequiresMover => false;

        // Stuck checkers
        private DateTime _lastStuckCheck = DateTime.Now;
        private GridCoordinates _ourLastPosition = default;

        public bool Arrived => _arrived;
        private bool _arrived = true;

        private IEntity _owner;

        [Dependency] private IMapManager _mapManager;
        [Dependency] private IPathfinder _pathfinder;

        public IEntity TargetEntity => _targetEntity;
        private IEntity _targetEntity;

        private GridCoordinates _lastTargetPosition;
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
            IoCManager.InjectDependencies(this);
            MovementAllowed = true;
            if (!owner.HasComponent<AiControllerComponent>())
            {
                Logger.FatalS("ai_routine", "MovementAiRoutine must have a valid controller on the owner");
                throw new InvalidOperationException();
            }
            _owner = owner;
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
            if ((_ourLastPosition.Position - _owner.Transform.GridPosition.Position).Length < TargetTolerance)
            {
                // Move in a random direction
                _owner.TryGetComponent(out AiControllerComponent mover);
                var robustRandom = IoCManager.Resolve<IRobustRandom>();
                var angle = Angle.FromDegrees(robustRandom.Next(359));
                mover.VelocityDir = angle.ToVec().Normalized;
            }
        }

        /// <summary>
        /// Will try and get a route to the target entity. Will try adjacent tiles if necessary
        /// If they move further than the tolerance it will get a new route.
        /// </summary>
        /// <param name="entity"></param>
        public void GetRoute(IEntity entity)
        {
            ClearRoute();
            _arrived = false;
            _targetEntity = entity;
            _lastTargetPosition = _targetEntity.Transform.GridPosition;
            GridCoordinates targetGrid = default;

            // If we can't get directly to the entity then try and go adjacent to it
            var entityTile = _mapManager.GetGrid(entity.Transform.GridID).GetTileRef(entity.Transform.GridPosition);
            if (!_pathfinder.IsTileTraversable(entityTile))
            {
                // Try and get adjacent tiles
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        var neighborTile = _mapManager
                            .GetGrid(entity.Transform.GridID)
                            .GetTileRef(new MapIndices(entityTile.X + x, entityTile.Y + y));
                        if (_pathfinder.IsTileTraversable(neighborTile))
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
                return;
            }

            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Tells this routine we don't need to keep moving
        /// </summary>
        public void ClearRoute()
        {
            _route.Clear();
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// </summary>
        public void HandleMovement()
        {
            Vector2 targetDiff;
            if (!MovementAllowed ||
                _targetEntity == null ||
                _mapManager.GetGrid(_owner.Transform.GridID).GetTileRef(_owner.Transform.GridPosition).Tile.IsEmpty)
            {
                return;
            }

            // Throttler - Need to re-check if we need to move
            if ((DateTime.Now - _entityRouteThrottle).TotalSeconds > 2.0f)
            {
                _entityRouteThrottle = DateTime.Now;

                if (_route.Count == 0 &&
                    (_targetEntity.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position)
                    .Length > TargetTolerance)
                {
                    GetRoute(_targetEntity);
                    return;
                }

                // If the entity has moved significantly will get a new route
                if ((_lastTargetPosition.Position - _targetEntity.Transform.GridPosition.Position).Length > 1)
                {
                    GetRoute(_targetEntity);
                    return;
                }
            }

            if (_arrived)
            {
                return;
            }

            if ((_owner.Transform.GridPosition.Position - _targetEntity.Transform.GridPosition.Position).Length <=
                TargetTolerance)
            {
                _arrived = true;
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = Vector2.Zero;
                return;
            }

            if (_mapManager.GetGrid(_targetEntity.Transform.GridID).GetTileRef(_targetEntity.Transform.GridPosition)
                .Tile.IsEmpty)
            {
                _route.Clear();
                return;
            }

            // need to check if it's moved.

            AntiStuck();

            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            _owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            targetDiff = _nextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > _tileTolerance)
            {
                // Move towards it
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized;
                return;
            }

            // If we've expended the route that must mean we're close
            if (_route.Count == 0)
            {
                _nextGrid = _targetEntity.Transform.GridPosition;
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
