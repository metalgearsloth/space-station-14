#nullable enable
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using Content.Shared.GameObjects.Components.Power;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedBatteryBarrelComponent : SharedRangedWeaponComponent, IBatteryGun
    {
        public override string Name => "BatteryBarrel";
        public override uint? NetID => ContentNetIDs.BATTERY_BARREL;

        public SharedBatteryComponent? Battery { get; }

        /// <summary>
        ///     The minimum change we need before we can fire
        /// </summary>
        [ViewVariables]
        [DataField("lowerChargeLimit")]
        public float LowerChargeLimit { get; set; } = 10.0f;

        /// <summary>
        ///     How much energy it costs to fire a full shot.
        ///     We can also fire partial shots if LowerChargeLimit is met.
        /// </summary>
        [ViewVariables]
        [DataField("baseFireCost")]
        public float BaseFireCost { get; set; } = 300.0f;

        /// <inheritdoc />
        [ViewVariables]
        [DataField("ammoPrototype")]
        public string AmmoPrototype
        {
            get => _ammoPrototype;
            set
            {
                if (_ammoPrototype.Equals(value)) return;
                _ammoPrototype = value;
                AmmoIsHitscan = IoCManager.Resolve<IPrototypeManager>().HasIndex<HitscanPrototype>(AmmoPrototype);
            }
        }

        private string _ammoPrototype = string.Empty;

        // Could use an interface instead but eh, if there's more than hitscan / projectiles in the future you can change it.
        public bool AmmoIsHitscan { get; private set; }

        [ViewVariables]
        [DataField("powerCellPrototype")]
        public string? PowerCellPrototype { get; set; }

        [ViewVariables]
        [DataField("powerCellRemovable")]
        public bool PowerCellRemovable { get; } = false;

        // Sounds
        [ViewVariables]
        [DataField("soundPowerCellInsert")]
        public string? SoundPowerCellInsert { get; set; }

        [ViewVariables]
        [DataField("soundPowerCellEject")]
        public string? SoundPowerCellEject { get; set; }

        public abstract void UpdateAppearance();
    }

    [Serializable, NetSerializable]
    public sealed class BatteryBarrelComponentState : ComponentState
    {
        public FireRateSelector FireRateSelector { get; }
        public (float current, float max)? PowerCell { get; }

        public BatteryBarrelComponentState(
            FireRateSelector fireRateSelector,
            (float current, float max)? powerCell) :
            base(ContentNetIDs.BATTERY_BARREL)
        {
            FireRateSelector = fireRateSelector;
            PowerCell = powerCell;
        }
    }
}
