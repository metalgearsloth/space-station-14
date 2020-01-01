using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Nutrition
{
    public class EatNearestFood : CompoundTask
    {
        public EatNearestFood(IEntity owner) : base(owner)
        {
        }

        public override string Name => "EatNearestFood";
        public override bool PreconditionsMet(AiWorldState context)
        {
            throw new System.NotImplementedException();
        }

        public override List<IAiTask> Methods { get; }
    }
}
