using System.Collections.Generic;
using Content.Server.AI.HTN.Blackboard;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Movement
{
    public class FollowEntity : CompoundTask
    {
        public override string Name { get; }
        public override bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        public override List<IAiTask> Methods { get; }

        public FollowEntity(IEntity owner, IEntity target) : base(owner)
        {
        }
    }
}
