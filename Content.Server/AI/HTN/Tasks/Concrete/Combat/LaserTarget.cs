using System;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public class LaserTarget : ConcreteTask
    {
        private IEntity _target;

        public LaserTarget(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = context.GetState<EquippedLaserWeapon>().GetValue();
            if (equippedWeapon == null)
            {
                return false;
            }

            return Math.Abs(equippedWeapon.CapacitorComponent.Charge) >= 0.02f;
        }

        public override void SetupOperator()
        {
            TaskOperator = new ShootLaserAtEntity(Owner, _target);
        }

        public override Outcome Execute(float frameTime)
        {
            // TODO: Need to handle running out of ammo
            var outcome = base.Execute(frameTime);
            return outcome != Outcome.Failed ? Outcome.Continuing : outcome; // Keep on hitting
        }
    }
}
