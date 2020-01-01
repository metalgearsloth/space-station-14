using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Thirst
{
    public sealed class PickupNearestDrink : PrimitiveTask
    {
        private IEntity _nearestDrink;
        public PickupNearestDrink(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyFood = context.GetState<NearbyDrink>();

            foreach (var entity in nearbyFood.GetValue())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestDrink = entity;
                return true;
            }

            return false;

        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntity(Owner, _nearestDrink);
        }
    }
}
