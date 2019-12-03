using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.Routines.Movers
{
    public sealed class MoveToEntityAiRoutine : BaseMoverAiRoutine
    {
        public IEntity TargetEntity => _targetEntity;
        private IEntity _targetEntity;
        private float _entityRouteThrottle = 2.0f;

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

            if (_route.Count <= 1)
            {
                // Couldn't find a route to target
                return;
            }

            // Because the entity may be half on 2 tiles we'll just cut out the first tile.
            // This may not be the best solution but sometimes if the AI is chasing for example it will
            // stutter backwards to the first tile again.
            _route.Dequeue();

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// Other routines should call GetRoute first
        /// </summary>
        public override void HandleMovement(float frameTime)
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

            _entityRouteThrottle -= frameTime;

            // Throttler - Check if we need to re-assess the route.
            if (_entityRouteThrottle <= 0)
            {
                _entityRouteThrottle = 1.0f;

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

            AntiStuck(frameTime);
            if (IsStuck)
            {
                return;
            }

            if (TryMove())
            {
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

        public override void Update(float frameTime)
        {
            HandleMovement(frameTime);
            base.Update(frameTime);
        }
    }
}
