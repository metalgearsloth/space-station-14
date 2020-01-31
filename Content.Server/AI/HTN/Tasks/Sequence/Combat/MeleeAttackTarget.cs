using Content.Server.AI.HTN.Tasks.Primitive.Combat;
using Content.Server.AI.HTN.Tasks.Primitive.Combat.Melee;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Combat
{
    public class MeleeAttackTarget : SequenceTask
    {
        public MeleeAttackTarget(IEntity owner) : base(owner)
        {
        }

        public override string Name => "MeleeAttackTarget";

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
                new MoveIntoMeleeRange(Owner),
            };
        }
    }
}
