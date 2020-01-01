using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Thirst;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Nutrition
{
    public class DrinkDrink : CompoundTask
    {
        public DrinkDrink(IEntity owner) : base(owner)
        {
        }

        public override string Name => "DrinkDrink";
        public override bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new UseDrinkInInventory(Owner),
                new PickupNearestDrink(Owner),
                new ClearHands(Owner),
            };
        }
    }
}
