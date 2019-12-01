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

namespace Content.Server.AI.Routines.Movers
{
    public sealed class MoveToEntityAiRoutine : BaseMoverAiRoutine
    {
        public IEntity TargetEntity => _targetEntity;
        private IEntity _targetEntity;
        private DateTime _entityRouteThrottle = DateTime.Now - TimeSpan.FromSeconds(5.0f);

        private GridCoordinates _lastTargetPosition;

        /// <summary>
        /// How close the target is allowed to move around before we get a new route
        /// </summary>
        public float TargetMovementTolerance = 2.0f;

        /// <summary>
        /// Will try and get a route to the target entity. Will try adjacent tiles if necessary
        /// If they move further than the tolerance it will get a new route.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="proximity">If the entity's tile is untraversable then should we get close to it</param>
        public void GetRoute(IEntity entity, float proximity = 0.0f)
        {
            HaveArrived();
            _arrived = false;
            _targetEntity = entity;
            _lastTargetPosition = _targetEntity.Transform.GridPosition;
            GridCoordinates targetGrid = default;

            if (targetGrid == default)
            {
                targetGrid = entity.Transform.GridPosition;
            }

            var route = _pathfinder.FindPath(Owner.Transform.GridPosition, targetGrid, proximity);

            foreach (var tile in route)
            {
                _route.Enqueue(tile);
            }

            if (_route.Count == 0)
            {
                // Couldn't find a route to target
                return;
            }

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// Other routines should call GetRoute first
        /// </summary>
        public override void HandleMovement()
        {
            if (_targetEntity == null || _arrived)
            {
                return;
            }

            // If the entity's moving we may run into it so check that
            if ((_targetEntity.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length <
                TargetProximity)
            {
                HaveArrived();
                return;
            }

            // Throttler - Check if we need to re-assess the route.
            if ((DateTime.Now - _entityRouteThrottle).TotalSeconds > 2.0f)
            {
                _entityRouteThrottle = DateTime.Now;

                if (_mapManager.GetGrid(_targetEntity.Transform.GridID).GetTileRef(_targetEntity.Transform.GridPosition)
                    .Tile.IsEmpty)
                {
                    // TODO: No path. Maybe use an event?
                    return;
                }

                if ((_lastTargetPosition.Position - TargetEntity.Transform.GridPosition.Position).Length > TargetMovementTolerance)
                {
                    GetRoute(_targetEntity);
                    return;
                }
            }

            AntiStuck();

            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            Owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            var targetDiff = NextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > TileTolerance)
            {
                // Move towards it
                Owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized;
                return;
            }

            // If we've expended the route and gotten this far that must mean we're close
            if (_route.Count == 0)
            {
                NextGrid = _targetEntity.Transform.GridPosition;
                return;
            }

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        public override void Update()
        {
            base.Update();
            HandleMovement();
        }
    }
}
