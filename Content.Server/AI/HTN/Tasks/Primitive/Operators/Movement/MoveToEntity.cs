using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement
{
    public class MoveToEntity : BaseMover
    {
        // Instance
        private GridCoordinates _lastTargetPosition;
        private IMapManager _mapManager;

        // Input
        public IEntity Target { get; set; }

        public MoveToEntity(IEntity owner, IEntity target)
        {
            base.Setup(owner);
            Target = target;
            _mapManager = IoCManager.Resolve<IMapManager>();
            // TODO: Get what you need from context once
        }

        public override Outcome Execute(float frameTime)
        {
            if (Target == null)
            {
                return Outcome.Failed;
            }

            // If they move near us
            if ((Target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length <= 2.0f)
            {
                return Outcome.Success;
            }

            // If they move too far or no route
            if (_lastTargetPosition == default ||
                (Target.Transform.GridPosition.Position - _lastTargetPosition.Position).Length > 2.0f)
            {
                _lastTargetPosition = Target.Transform.GridPosition;
                TargetGrid = Target.Transform.GridPosition;
                GetRoute(); // TODO: Look at making this async in conjunction with pathfinder
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
