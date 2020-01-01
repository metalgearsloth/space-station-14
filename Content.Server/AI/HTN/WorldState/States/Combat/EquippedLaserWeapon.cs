using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public sealed class EquippedLaserWeapon : IStateData
    {
        public string Name => "EquippedLaserWeapon";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public HitscanWeaponComponent GetValue()
        {
            if (!_owner.TryGetComponent(out HandsComponent handsComponent))
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
