using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement
{
    public sealed class MoveToEntityOperator : BaseMover
    {
        // Instance
        private GridCoordinates _lastTargetPosition;
        private IMapManager _mapManager;

        // Input
        public IEntity Target { get; set; }

        public MoveToEntityOperator(IEntity owner, IEntity target)
        {
            Setup(owner);
            Target = target;
            _mapManager = IoCManager.Resolve<IMapManager>();
            // TODO: Get what you need from context once
        }

        public override Outcome Execute(float frameTime)
        {
            if (Target == null ||
                Target.Deleted ||
                Target.Transform.GridID != Owner.Transform.GridID)
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
                return Route.Count == 0 ? Outcome.Failed : Outcome.Continuing;
            }

            var targetRange = (Target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length;

            // If they move near us
            if (targetRange <= 1.5f)
            {
                // TODO: Update MoveToGrid inline with this executor
                HaveArrived();
                return Outcome.Success;
            }

            // If they move too far or no route
            if (_lastTargetPosition == default ||
                (Target.Transform.GridPosition.Position - _lastTargetPosition.Position).Length > 1.5f)
            {
                _lastTargetPosition = Target.Transform.GridPosition;
                TargetGrid = Target.Transform.GridPosition;
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

            if (Route.Count == 0 && targetRange > 1.5f)
            {
                HaveArrived();
                return Outcome.Failed;
            }

            var nextTile = Route.Dequeue();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
            return Outcome.Continuing;
        }
    }
}
