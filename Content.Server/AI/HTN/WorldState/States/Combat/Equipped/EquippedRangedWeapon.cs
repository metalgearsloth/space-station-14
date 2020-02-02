using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat.Equipped
{
    public sealed class EquippedRangedWeapon : StateData<IEntity>
    {
        public override string Name => "EquippedRangedWeapon";

        public override IEntity GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return null;
            }

            var equippedItem = handsComponent.GetActiveHand?.Owner;

            if (equippedItem != null && equippedItem.HasComponent<RangedWeaponComponent>())
            {
                return equippedItem;
            }

            return null;
        }
    }
}
