#nullable enable
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedBatteryBarrelComponent : SharedRangedWeaponComponent
    {
        public override string Name => "BatteryBarrel";
        public override uint? NetID => ContentNetIDs.BATTERY_BARREL;
        
        // The minimum change we need before we can fire
        [ViewVariables] protected float LowerChargeLimit;
        [ViewVariables] protected uint BaseFireCost;
        
        // What gets fired
        [ViewVariables] public string AmmoPrototype { get; private set; } = default!;
        
        // Could use an interface instead but eh, if there's more than hitscan / projectiles in the future you can change it.
        protected bool AmmoIsHitscan;

        // Sounds
        public string? SoundPowerCellInsert { get; private set; }
        public string? SoundPowerCellEject { get; private set; }
        
        // Audio profile
        protected const float CellInsertVariation = 0.1f;
        protected const float CellEjectVariation = 0.1f;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("ammoPrototype", string.Empty, value => AmmoPrototype = value, () => AmmoPrototype);
            serializer.DataField(ref LowerChargeLimit, "lowerChargeLimit", 10);
            
            serializer.DataReadWriteFunction("soundPowerCellInsert", null, value => SoundPowerCellInsert = value, () => SoundPowerCellInsert);
            serializer.DataReadWriteFunction("soundPowerCellEject", null, value => SoundPowerCellEject = value, () => SoundPowerCellEject);
        }
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
