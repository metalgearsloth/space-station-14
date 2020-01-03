using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public sealed class EquippedLaserWeapon : StateData<HitscanWeaponComponent>
    {
        public override string Name => "EquippedLaserWeapon";

        public override HitscanWeaponComponent GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return null;
            }

            var equippedItem = handsComponent.GetActiveHand?.Owner;

            if (equippedItem != null && equippedItem.TryGetComponent(out HitscanWeaponComponent hitscan))
            {

                return hitscan;
            }

            return null;
        }
    }
}
