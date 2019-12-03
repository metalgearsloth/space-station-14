using System;
using System.Collections.Generic;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
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

        public float IdleRadius { get; set; } = 4.0f;
        private float _wanderCooldown = 0.0f;
        public float WanderSpeed { get; set; } = 0.25f; // Portion of WalkSpeed to wander around. This gets clamped at the mover level
        public float TimeBetweenWander { get; set; } = 2.0f; // This will proc when they're in range of the wander spot

        public override void Setup(IEntity owner, AiLogicProcessor processor)
        {
            base.Setup(owner, processor);
            IdleSpot = Owner.Transform.GridPosition;

            _mover.Setup(owner, Processor);
        }

        /// <summary>
        /// Will get a random tile in the idle radius and pathfind to it
        /// </summary>
        private void FindWanderSpot()
        {
            _mover.TargetTolerance = 0.4f; // Need a tighter tolerance for close-range

            var mapManager = IoCManager.Resolve<IMapManager>();
            List<TileRef> tilesInRange = new List<TileRef>();
            foreach (var tile in mapManager.GetGrid(IdleSpot.GridID).GetTilesIntersecting(new Circle(IdleSpot.Position, IdleRadius)))
            {
                tilesInRange.Add(tile);
            }

            // Pick a rando tile
            var robustRandom = IoCManager.Resolve<IRobustRandom>();

            while (tilesInRange.Count > 0)
            {
                var randomIndex = robustRandom.Next(tilesInRange.Count - 1);
                var randomTile = tilesInRange[randomIndex];
                if (PathUtils.IsTileTraversable(randomTile))
                {
                    var wanderGrid = mapManager.GetGrid(randomTile.GridIndex).GridTileToLocal(randomTile.GridIndices);
                    _mover.GetRoute(wanderGrid);
                    _mover.Speed = WanderSpeed;
                    break;
                }

                tilesInRange.Remove(randomTile);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_mover.Arrived)
            {
                _mover.HandleMovement(frameTime);
                return;
            }

            // If we're not in range and no route then get one, otherwise check if we need to wander about
            if ((Owner.Transform.GridPosition.Position - IdleSpot.Position).Length <= IdleRadius)
            {
                _wanderCooldown -= frameTime;

                if (_wanderCooldown > 0)
                {
                    return;
                }

                _wanderCooldown = TimeBetweenWander;
                FindWanderSpot();
                return;
            }

            // Shouldn't need this if check but just in case
            if (_mover.Arrived)
            {
                _mover.TargetTolerance = IdleRadius / 2;
                _mover.Speed = 1.0f;
                _mover.GetRoute(IdleSpot);
                return;
            }
        }

        public override void InactiveRoutine()
        {
            base.InactiveRoutine();
            _mover.Speed = 1.0f;
        }
    }
}
