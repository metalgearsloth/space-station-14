using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States
{
    public sealed class EquippedMeleeWeapon : IStateData
    {
        public string Name => "EquippedMeleeWeapon";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public MeleeWeaponComponent GetValue()
        {
            if (!_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return null;
            }

            var equippedItem = handsComponent.GetActiveHand?.Owner;

            if (equippedItem != null && equippedItem.TryGetComponent(out MeleeWeaponComponent meleeWeaponComponent))
            {

                return meleeWeaponComponent;
            }

            return null;
        }
    }
}
