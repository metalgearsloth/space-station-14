using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat
{
    public class SwingMeleeWeapon : IOperator
    {
        // Out variables

        // Instance variables

        // Input variables
        private readonly IEntity _owner;
        private readonly GridCoordinates _target;

        public SwingMeleeWeapon(IEntity owner, GridCoordinates target)
        {
            _owner = owner;
            _target = target;
        }

        public Outcome Execute(float frameTime)
        {
            if (!_owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                return Outcome.Failed;
            }

            if (!combatModeComponent.IsInCombatMode)
            {
                combatModeComponent.IsInCombatMode = true;
            }

            if (!_owner.TryGetComponent(out HandsComponent hands) || hands.GetActiveHand == null)
            {
                return Outcome.Failed;
            }
            var meleeWeapon = hands.GetActiveHand.Owner;
            meleeWeapon.TryGetComponent(out MeleeWeaponComponent meleeWeaponComponent);

            if ((_target.Position - _owner.Transform.GridPosition.Position).Length >
                meleeWeaponComponent.Range)
            {
                // Didn't even hit them once!
                return Outcome.Failed;
            }

            var interactionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InteractionSystem>();
            //interactionSystem.UseItemInHand(_owner, _target, _target.Uid); // TODO
            return Outcome.Success;
        }
    }

}
