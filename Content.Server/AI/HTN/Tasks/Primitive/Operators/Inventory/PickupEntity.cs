using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory
{
    public class PickupEntity : IOperator
    {
        // Input variables
        private IEntity _owner;
        private IEntity _target;

        public PickupEntity(IEntity owner, IEntity target)
        {
            _owner = owner;
            _target = target;
        }

        public Outcome Execute(float frameTime)
        {
            // TODO: If these are on different maps they'll fail, also do elsewhere
            if (_owner.Transform.GridID != _target.Transform.GridID)
            {
                return Outcome.Failed;
            }

            if ((_owner.Transform.GridPosition.Position - _target.Transform.GridPosition.Position).Length >
                InteractionSystem.InteractionRange)
            {
                return Outcome.Failed;
            }

            var interactionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InteractionSystem>();
            interactionSystem.Interaction(_owner, _target);
            return Outcome.Success;
        }
    }
}
