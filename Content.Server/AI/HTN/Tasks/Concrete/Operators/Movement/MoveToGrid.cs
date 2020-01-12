using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement
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
            if (_grid.GridID != Owner.Transform.GridID)
            {
                HaveArrived();
                return Outcome.Failed;
            }

            if (RouteTask != null)
            {
                if (!RouteTask.IsCompleted)
                {
                    return Outcome.Continuing;
                }
                ReceivedRoute();
                return Outcome.Continuing; // We'll deal with it next tick
            }

            // If they move too far or no route
            if (Route.Count == 0)
            {
                TargetGrid = _grid;
                GetRoute();
                return Outcome.Continuing;
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
