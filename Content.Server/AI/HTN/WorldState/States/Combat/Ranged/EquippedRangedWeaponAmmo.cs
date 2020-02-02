using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.Components.Weapon.Ranged.Projectile;

namespace Content.Server.AI.HTN.WorldState.States.Combat.Ranged
{
    /// <summary>
    /// Gets the discrete ammo count
    /// </summary>
    public sealed class EquippedRangedWeaponAmmo : StateData<int?>
    {
        public override string Name => "EquippedRangedWeaponAmmo";

        public override int? GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return null;
            }

            var equippedItem = handsComponent.GetActiveHand?.Owner;
            if (equippedItem == null) return null;

            if (equippedItem.TryGetComponent(out HitscanWeaponComponent hitscanWeaponComponent))
            {
                return (int) hitscanWeaponComponent.CapacitorComponent.Charge / hitscanWeaponComponent.BaseFireCost;
            }

            if (equippedItem.TryGetComponent(out BallisticMagazineWeaponComponent ballisticComponent))
            {
                return ballisticComponent.MagazineSlot.ContainedEntities.Count;
            }

            return null;
        }
    }
}
