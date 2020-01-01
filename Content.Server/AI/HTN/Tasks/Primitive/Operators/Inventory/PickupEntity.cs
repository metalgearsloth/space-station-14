using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.GameObjects;
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

        // Just to avoid continuously chaining movement operators with other operators we'll just add it in where needed.
        private MoveToEntity _movementHandler;

        public PickupEntity(IEntity owner, IEntity target)
        {
            _owner = owner;
            _target = target;
            _movementHandler = new MoveToEntity(_owner, _target);
        }

        // TODO: When I spawn new entities they seem to duplicate clothing or something?
        public Outcome Execute(float frameTime)
        {
            if (_target.Deleted ||
                !_target.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped)
            {
                _movementHandler.HaveArrived();
                return Outcome.Failed;
            }

            var movementOutcome = _movementHandler.Execute(frameTime);

            if (movementOutcome != Outcome.Success)
            {
                return movementOutcome;
            }

            // We're in range
            _movementHandler = null;

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
