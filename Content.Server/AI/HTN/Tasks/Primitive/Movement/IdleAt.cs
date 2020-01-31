using System;
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
    /// </summary>
    public class IdleAt : PrimitiveTask
    {
        public override string Name => "IdleAt";

        // range limit by worldbounds of grid?
        private int range = 3;
        private GridCoordinates _idleCenter;
        // Where we will wander to (within range)
        private GridCoordinates _idleSpot;
        // How long we will stay at a spot before moving to a new one within range
        private float _holdTime = 4.0f;
        private float _holdTimeRemaining;

        private MoveToGridOperator _operator;
        private bool _movementFinished;

        public IdleAt(IEntity owner) : base(owner)
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

            // todo: choose a random direction and try and get the tile there instead
            _movementFinished = false;
            _holdTimeRemaining = _holdTime;
            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(Owner.Transform.GridID);
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            var angle = Angle.FromDegrees(robustRandom.Next(0, 359));
            var randomDistance = robustRandom.Next(1, range);
            var newPosition = _idleCenter.Position + angle.ToVec() * randomDistance;
            // Conversions blah blah
            _idleSpot = grid.GridTileToLocal(grid.WorldToTile(grid.LocalToWorld(newPosition)));


            _operator.UpdateTarget(_idleSpot);
            return Outcome.Continuing;
        }
    }
}
