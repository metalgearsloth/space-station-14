using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Concrete.Operators;
using Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement
{
    public class MoveToGrid : BaseMover
    {
        // Instance variables
        private IMapManager _mapManager;

        // Input variables
        private IEntity _owner;
        private GridCoordinates _grid;

        public MoveToGrid(IEntity owner, GridCoordinates grid)
        {
            base.Setup(owner);
            _owner = owner;
            _grid = grid;
            _mapManager = IoCManager.Resolve<IMapManager>();
        }

        public override Outcome Execute(float frameTime)
        {
            if (Route == null)
            {
                GetRoute();
                return Route == null ? Outcome.Failed : Outcome.Continuing;
            }

            AntiStuck(frameTime);

            if (IsStuck)
            {
                return Outcome.Continuing;
            }

            if (TryMove())
            {
                return Outcome.Continuing;
            }

            if (Route.Count == 0)
            {
                return Outcome.Success;
            }

            var nextTile = Route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
            return Outcome.Continuing;
        }
    }
}
