using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.HTN.Tasks.Primitive.Nutrition;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Nutrition
{
    public class SatisfyHunger : CompoundTask
    {
        public override string Name { get; }

        public SatisfyHunger(IEntity owner) : base(owner) {}

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Food on person or nearby.
            return true;
        }

        public override List<IAiTask> Methods => new List<IAiTask>()
        {
            new MoveToNearestFood(Owner)
        };
    }
}
