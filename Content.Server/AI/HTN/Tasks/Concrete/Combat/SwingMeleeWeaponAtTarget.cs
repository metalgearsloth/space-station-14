using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public sealed class SwingMeleeWeaponAtTarget : ConcreteTask
    {
        public override PrimitiveTaskType TaskType => PrimitiveTaskType.MeleeAttack;
        private IEntity _target;
        private MeleeWeaponComponent _meleeWeapon;

        public SwingMeleeWeaponAtTarget(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (_target == null)
            {
                return false;
            }
            var equippedWeapon = context.GetState<EquippedMeleeWeapon>().GetValue();
            if (equippedWeapon == null) return false;
            _meleeWeapon = equippedWeapon;
            return true;

        }

        public override void SetupOperator()
        {
            TaskOperator = new Operators.Combat.SwingMeleeWeaponAtEntity(Owner, _target);
        }
    }
}
