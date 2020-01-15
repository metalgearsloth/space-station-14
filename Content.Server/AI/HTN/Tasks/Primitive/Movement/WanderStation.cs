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
    public class WanderStation : PrimitiveTask
    {
        // How long before task completes once we arrive
        private float _waitTime = 3.0f;
        private float _waitTimeRemaining;
        private GridCoordinates _targetGrid;

        public WanderStation(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (Owner.Transform.GridID == GridId.Invalid)
            {
                return false;
            }

            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(Owner.Transform.GridID);

            var tiles = grid.GetTilesIntersecting(new Circle(Owner.Transform.LocalPosition, 30.0f)).ToList();
            if (tiles.Count == 0)
            {
                return false;
            }

            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            var randomTile = tiles[robustRandom.Next(tiles.Count - 1)];
            _targetGrid = grid.GridTileToLocal(randomTile.GridIndices);
            _waitTimeRemaining = _waitTime;
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToGridOperator(Owner, _targetGrid);
        }

        public override Outcome Execute(float frameTime)
        {
            var result = base.Execute(frameTime);
            if (result != Outcome.Success)
            {
                return result;
            }

            _waitTimeRemaining -= frameTime;

            if (_waitTimeRemaining <= 0)
            {
                return Outcome.Continuing;
            }

            return Outcome.Success;
        }
    }
}
