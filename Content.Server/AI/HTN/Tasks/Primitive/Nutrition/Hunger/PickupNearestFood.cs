using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Hunger
{
    public sealed class PickupNearestFood : PrimitiveTask
    {
        private IEntity _nearestFood;
        public PickupNearestFood(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyFood = context.GetState<NearbyFood>();

            foreach (var entity in nearbyFood.GetValue())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestFood = entity;
                return true;
            }

            return false;

        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntity(Owner, _nearestFood);
        }
    }
}
