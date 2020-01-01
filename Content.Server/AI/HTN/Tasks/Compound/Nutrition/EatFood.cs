using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Hunger;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Nutrition
{
    public class EatFood : CompoundTask
    {
        private IEntity _nearestFood;
        public EatFood(IEntity owner) : base(owner)
        {
        }

        public override string Name => "EatFood";
        public override bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new UseFoodInInventory(Owner),
                new PickupNearestFood(Owner),
                new ClearHands(Owner),
            };
        }
    }
}
