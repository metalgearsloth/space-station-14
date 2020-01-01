using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition
{
    public sealed class PickupNearestDrink : PrimitiveTask
    {
        private IEntity _nearestFood;
        public PickupNearestDrink(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyFood = context.GetState<NearbyDrink>();

            foreach (var entity in nearbyFood.GetValue())
            {
                _nearestFood = entity;
            }

            return _nearestFood != null;

        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntity(Owner, _nearestFood);
        }
    }
}
