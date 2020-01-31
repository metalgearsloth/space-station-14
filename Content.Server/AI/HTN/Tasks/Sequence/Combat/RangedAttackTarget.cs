using Content.Server.AI.HTN.Tasks.Primitive.Combat;
using Content.Server.AI.HTN.Tasks.Primitive.Combat.Melee;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Combat
{
    public class RangedAttackTarget : SequenceTask
    {
        public RangedAttackTarget(IEntity owner) : base(owner)
        {
        }

        public override string Name => "RangedAttackTarget";

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (context.GetState<AttackTarget, IEntity>().Value == null)
            {
                return false;
            }

            return base.PreconditionsMet(context);
        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new SwingMeleeWeapon(Owner),
                new MoveToEntity(Owner, context.GetStateValue<AttackTarget, IEntity>()),
            };
        }
    }
}
