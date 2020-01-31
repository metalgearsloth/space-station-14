using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.Tasks.Sequence.Combat;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Robust.Shared.Interfaces.GameObjects;
using MeleeAttackTarget = Content.Server.AI.HTN.Tasks.Sequence.Combat.MeleeAttackTarget;

namespace Content.Server.AI.HTN.Tasks.Selector.Combat
{
    public class MeleeCombat : SelectorTask
    {
        private IEntity _target;
        public override string Name => "MeleeCombat";

        public MeleeCombat(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return _target != null;
        }

        public override void ProceduralEffects(in AiWorldState context)
        {
            base.ProceduralEffects(in context);
            context.GetState<AttackTarget, IEntity>().Value = _target;
        }

        public override void SetupMethods(AiWorldState context)
        {
            // TODO: Change tasks to use world states instead of passing variables??
            Methods = new List<IAiTask>
            {
                new MeleeAttackTarget(Owner),
                new PickupNearestMeleeWeapon(Owner),
            };
        }
    }
}
