using Robust.Shared.Map;

namespace Content.Server.AI.Routines.Movers
{
    public class MoveToGridCoordsAiRoutine : BaseMoverAiRoutine
    {

        private GridCoordinates _targetPosition;
        // How close we need to get. Would not recommend setting it too low or else you'll get back and forth
        public float TargetTolerance { get; set; } = 0.5f;

        /// <summary>
        /// Gets a route to the specified target grid
        /// </summary>
        /// <param name="gridCoordinates"></param>
        public void GetRoute(GridCoordinates gridCoordinates)
        {
            HaveArrived();
            _arrived = false;

            foreach (var tile in _pathfinder.FindPath(Owner.Transform.GridPosition, gridCoordinates))
            {
                _route.Enqueue(tile);
            }

            if (_route.Count <= 1)
            {
                return;
            }

            // See MoveToEntityAiRoutine for why first tile is dropped
            _route.Dequeue();

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
            _targetPosition = gridCoordinates;
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// </summary>
        public override void HandleMovement(float frameTime)
        {
            if (_arrived)
            {
                return;
            }

            if ((Owner.Transform.GridPosition.Position - _targetPosition.Position).Length <= TargetTolerance)
            {
                HaveArrived();
                return;
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

            // If we've expended the route and gotten this far that must mean we're close? IDEK
            // TODO: If you change this probs change the move to entity as well
            if (_route.Count == 0)
            {
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
