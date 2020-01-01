using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Combat
{
    public class MeleeAttackTarget : CompoundTask
    {
        public MeleeAttackTarget(IEntity owner) : base(owner)
        {
        }

        public override string Name { get; }
        public override bool PreconditionsMet(AiWorldState context)
        {
            throw new System.NotImplementedException();
        }

        public override void SetupMethods()
        {
            throw new System.NotImplementedException();
        }
    }
}
