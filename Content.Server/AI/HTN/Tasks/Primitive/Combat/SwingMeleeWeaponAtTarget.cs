using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public sealed class SwingMeleeWeaponAtTarget : PrimitiveTask
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
            var equippedWeapon = context.GetStateValue<EquippedMeleeWeapon, MeleeWeaponComponent>();
            if (equippedWeapon == null) return false;
            _meleeWeapon = equippedWeapon;
            return true;

        }

        public override void SetupOperator()
        {
            TaskOperator = new Primitive.Operators.Combat.SwingMeleeWeaponAtEntity(Owner, _target);
        }
    }
}
