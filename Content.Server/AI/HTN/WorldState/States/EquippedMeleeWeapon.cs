using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States
{
    public sealed class EquippedMeleeWeapon : StateData<MeleeWeaponComponent>
    {
        public override string Name => "EquippedMeleeWeapon";

        public override MeleeWeaponComponent GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
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
