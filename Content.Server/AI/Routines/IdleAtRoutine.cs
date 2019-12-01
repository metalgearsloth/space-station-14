using System;
using System.Collections.Generic;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.Routines
{
    /// <summary>
    /// Move to the specified spot and will wander around that area indefinitely.
    /// </summary>
    public class IdleAtRoutine : AiRoutine
    {

        private MoveToGridCoordsAiRoutine _mover = new MoveToGridCoordsAiRoutine();

        // Similar to Idle but will lounge around the idle spot
        public GridCoordinates IdleSpot
        {
            get => _idleSpot;
            set
            {
                // If it's in space or whatever. Maybe update it anyway?
                var mapManager = IoCManager.Resolve<IMapManager>();
                var tile = mapManager.GetGrid(value.GridID).GetTileRef(value);
                if (!PathUtils.IsTileTraversable(tile))
                {
                    return;
                }
                _idleSpot = value;
            }
        }

        private GridCoordinates _idleSpot ;

        public float IdleRadius = 5.0f;
        private DateTime _lastWander = DateTime.Now;
        public float TimeBetweenWander { get; set; } = 2.0f;

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IdleSpot = Owner.Transform.GridPosition;
            _mover.Setup(owner);
        }

        public override void Update()
        {
            base.Update();

            if (!_mover.Arrived)
            {
                _mover.HandleMovement();
            }

            // If we're in range then check if we need to wander around this area
            if ((Owner.Transform.GridPosition.Position - IdleSpot.Position).Length <= IdleRadius)
            {
                if ((DateTime.Now - _lastWander).TotalSeconds < TimeBetweenWander)
                {
                    return;
                }
                // Need to get a new spot to wander to
                var mapManager = IoCManager.Resolve<IMapManager>();
                List<TileRef> tilesInRange = new List<TileRef>();
                foreach (var tile in mapManager.GetGrid(IdleSpot.GridID).GetTilesIntersecting(new Circle(IdleSpot.Position, IdleRadius)))
                {
                    tilesInRange.Add(tile);
                }

                foreach (var tile in tilesInRange)
                {
                    if (!PathUtils.IsTileTraversable(tile))
                    {
                        continue;
                    }

                    var wanderGrid = mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);

                    // Potentially keep pathing to each tile until a valid one is found maybe
                    // Probably not worth it for an idler
                    _mover.GetRoute(wanderGrid);
                    _lastWander = DateTime.Now;
                    return;
                }

                return;
            }

            _mover.GetRoute(IdleSpot);
        }
    }
}
