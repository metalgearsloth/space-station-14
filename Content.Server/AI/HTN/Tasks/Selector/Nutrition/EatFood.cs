using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Hunger;
using Content.Server.AI.HTN.Tasks.Sequence.Nutrition;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Selector.Nutrition
{
    public class EatFood : SelectorTask
    {
        public EatFood(IEntity owner) : base(owner)
        {
        }

        public override string Name => "EatFood";
        public override bool PreconditionsMet(AiWorldState context)
        {
            return Owner.HasComponent<StomachComponent>();
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new UseFoodInInventory(Owner),
                new PickupNearestFood(Owner),
            };
        }
    }
}
