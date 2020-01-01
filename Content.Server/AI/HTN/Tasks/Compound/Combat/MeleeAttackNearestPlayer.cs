using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Combat;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Combat
{
    public class MeleeAttackNearestPlayer : CompoundTask
    {
        public MeleeAttackNearestPlayer(IEntity owner) : base(owner)
        {
        }

        public override string Name => "MeleeAttackNearestPlayer";
        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Has weapon
            // TODO: Get nearest person and add it to method
            return true;
        }

        public override void SetupMethods()
        {
            // TODO
            Methods = new List<IAiTask>()
            {

            };
        }
    }
}
