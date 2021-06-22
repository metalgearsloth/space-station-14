#nullable enable
using Content.Server.Battery.Components;
using Content.Server.PowerCell.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Power.ApcNetComponents.PowerReceiverUsers
{
    /// <summary>
    /// Recharges the battery in a <see cref="ServerBatteryBarrelComponent"/>.
    /// </summary>
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    [ComponentReference(typeof(BaseCharger))]
    public sealed class WeaponCapacitorChargerComponent : BaseCharger
    {
        public override string Name => "WeaponCapacitorCharger";

        protected override bool IsEntityCompatible(IEntity entity)
        {
            return entity.TryGetComponent(out PowerCellSlotComponent? slot) && slot.HasCell;
        }

        protected override BatteryComponent? GetBatteryFrom(IEntity entity)
        {
            if (!entity.TryGetComponent(out PowerCellSlotComponent? slot)) return null;
            return slot.Cell ?? null;
        }
    }
}
