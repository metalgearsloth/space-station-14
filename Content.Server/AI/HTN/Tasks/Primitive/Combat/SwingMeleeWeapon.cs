using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public sealed class SwingMeleeWeapon : PrimitiveTask
    {

        private IEntity _target;

        public SwingMeleeWeapon(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var hasWeapon = context.GetState<MeleeWeaponEquipped>();
            return hasWeapon.GetValue();
        }

        public override void SetupOperator()
        {
            TaskOperator = new Operators.Combat.SwingMeleeWeapon(Owner, _target.Transform.GridPosition);
        }
    }
}
