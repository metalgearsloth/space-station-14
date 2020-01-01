using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Thirst;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Nutrition
{
    public class DrinkDrink : CompoundTask
    {
        private IEntity _nearestDrink;
        public DrinkDrink(IEntity owner) : base(owner)
        {
        }

        public override string Name => "DrinkDrink";
        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyFood = context.GetState<NearbyDrink>();

            foreach (var entity in nearbyFood.GetValue())
            {
                _nearestDrink = entity;
            }

            return _nearestDrink != null;
        }

        public override void SetupMethods()
        {
            Methods = new List<IAiTask>
            {
                new UseDrinkInInventory(Owner),
                new PickupNearestDrink(Owner),
            };
        }
    }
}
