using Content.Server.AI.HTN.Tasks.Primitive.Combat.Ranged;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.AI.HTN.WorldState.States.Combat.Equipped;
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
            if (context.GetStateValue<AttackTarget, IEntity>() == null)
            {
                return false;
            }

            if (context.GetStateValue<EquippedRangedWeapon, IEntity>() == null)
            {
                return false;
            }

            return true;
        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new ShootLaserWeapon(Owner),
                new MoveIntoLOS(Owner),
            };
        }
    }
}
