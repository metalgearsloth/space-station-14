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
    public class MoveToGridCoordsAiRoutine : BaseMoverAiRoutine
    {

        private GridCoordinates _targetPosition;
        // How close we need to get
        public float TargetTolerance
        {
            get;
            set;
        }

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

            var nextTile = _route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
            _targetPosition = gridCoordinates;
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// </summary>
        public override void HandleMovement()
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

            // If we've expended the route and gotten this far that must mean we're close? IDEK
            // TODO: If you change this probs change the move to entity as well
            if (_route.Count == 0)
            {
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
