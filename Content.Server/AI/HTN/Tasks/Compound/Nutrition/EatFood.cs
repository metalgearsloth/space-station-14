using System.Collections.Generic;
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
            var nearbyFood = context.GetState<NearbyFood>();

            foreach (var entity in nearbyFood.GetValue())
            {
                _nearestFood = entity;
            }

            return _nearestFood != null;
        }

        public override List<IAiTask> Methods => new List<IAiTask>()
        {
            new UseFoodInInventory(Owner),
            new PickupNearestFood(Owner),
        };
    }
}
