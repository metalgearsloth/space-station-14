using System;
using System.Linq;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
{
    /// <summary>
    ///  Will loiter around a particular spot indefinitely.
    /// If you want this to wander the station just increase the range
    /// </summary>
    public class IdleAround : PrimitiveTask
    {
        // TODO: Make a separate one for wander station that's identical
        private float range = 100.0f;
        private GridCoordinates _idleCenter;
        // Where we will wander to (within range)
        private GridCoordinates _idleSpot;
        // How long we will stay at a spot before moving to a new one within range
        private float _holdTime = 4.0f;
        private float _holdTimeRemaining;

        private MoveToGridOperator _operator;
        private bool _movementFinished = false;

        public IdleAround(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            _holdTimeRemaining = _holdTime;
            _idleCenter = Owner.Transform.GridPosition;
            _idleSpot = _idleCenter;
            return true;
        }

        public override void SetupOperator()
        {
            _operator = new MoveToGridOperator(Owner, _idleSpot);
            TaskOperator = _operator;
            _operator.SpeedMultiplier = 0.3f;
            _operator.Stuck += () =>
            {
                _idleSpot = Owner.Transform.GridPosition;
                _operator.UpdateTarget(_idleSpot);
            }; // TODO: Memleak?
        }

        public override Outcome Execute(float frameTime)
        {
            if (!_movementFinished)
            {
                var result = base.Execute(frameTime);

                switch (result)
                {
                    case Outcome.Success:
                        break;
                    case Outcome.Continuing:
                        return result;
                    case Outcome.Failed:
                        // If pathing fails it's no biggy
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            _movementFinished = true;

            // Sit at the spot for a bit then move to a new spot
            _holdTimeRemaining -= frameTime;

            if (_holdTimeRemaining > 0)
            {
                return Outcome.Continuing;
            }

            _movementFinished = false;
            _holdTimeRemaining = _holdTime;
            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(Owner.Transform.GridID);
            var validTiles = grid.GetTilesIntersecting(new Circle(_idleCenter.Position, range)).ToList();

            if (validTiles.Count == 0)
            {
                return Outcome.Failed;
            }

            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            var randomTile = validTiles[robustRandom.Next(validTiles.Count - 1)];
            _idleSpot = grid.GridTileToLocal(randomTile.GridIndices);
            _operator.UpdateTarget(_idleSpot);
            return Outcome.Continuing;
        }
    }
}
