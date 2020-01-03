using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Concrete.Operators.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Nutrition.Thirst
{
    public sealed class UseDrinkInInventory : ConcreteTask
    {
        private IEntity _targetDrink;
        public override PrimitiveTaskType TaskType => PrimitiveTaskType.Interaction;

        public UseDrinkInInventory(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Check hands then check inventory
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            // Find any Drink in hands
            foreach (var hand in handsComponent.ActivePriorityEnumerable())
            {
                var item = handsComponent.GetHand(hand);
                if (item == null) continue;

                if (!item.Owner.HasComponent<DrinkComponent>()) continue;

                _targetDrink = item.Owner;
                return true;
            }
            return false;
        }

        public override HashSet<StateData> ProceduralEffects => new HashSet<StateData>
        {
            // new HungryState(false)
        };

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _targetDrink);
        }

        public override Outcome Execute(float frameTime)
        {

            var outcome = base.Execute(frameTime);
            if (outcome == Outcome.Failed)
            {
                return outcome;
            }

            if (_targetDrink.Deleted)
            {
                // TODO: Drop trash
            }

            // TODO: Keep eating it until stomach full
            return !_targetDrink.Deleted ? Outcome.Continuing : Outcome.Success;
        }
    }
}
