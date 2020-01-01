using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Thirst
{
    public sealed class UseDrinkInInventory : PrimitiveTask
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

            // Find any food in hands
            foreach (var hand in handsComponent.GetHandIndices())
            {
                var item = handsComponent.GetHand(hand);
                if (item == null) continue;

                if (!item.Owner.HasComponent<DrinkComponent>()) continue;

                _targetDrink = item.Owner;
                return true;
            }
            return false;
        }

        public override HashSet<IStateData> ProceduralEffects => new HashSet<IStateData>
        {
            // new ThirstyState(false)
        };

        // TODO: Copy from UseFoodInInventory when it's gucci

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _targetDrink);
        }
    }
}
