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
        
        // TODO: Add Hitscan prototype that can be fired as well
        // What gets fired
        [ViewVariables] protected string AmmoPrototype = default!;
        
        // Could use an interface instead but eh, if there's more than hitscan / projectiles in the future you can change it.
        protected bool AmmoIsHitscan;
        
        /// <summary>
        ///     How much charge we've used for this shoot.
        /// </summary>
        protected float ToFireCharge;
        
        // Sounds
        protected string? SoundPowerCellInsert;
        protected string? SoundPowerCellEject;
        
        // Audio profile
        protected const float EjectVariation = 0.1f;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref AmmoPrototype, "ammoPrototype", string.Empty);
            serializer.DataField(ref LowerChargeLimit, "lowerChargeLimit", 10);
            serializer.DataField(ref SoundPowerCellInsert, "soundPowerCellInsert", null);
            serializer.DataField(ref SoundPowerCellEject, "soundPowerCellEject", null);
        }

        public override void Initialize()
        {
            base.Initialize();
            var protoManager = IoCManager.Resolve<IPrototypeManager>();

            if (protoManager.Index<HitscanPrototype>(AmmoPrototype) != null)
            {
                AmmoIsHitscan = true;
            }
            else if (protoManager.Index<EntityPrototype>(AmmoPrototype) != null)
            {
                AmmoIsHitscan = false;
            }
            else
            {
                throw new InvalidOperationException($"Unable to find valid AmmoPrototype {AmmoPrototype}");
            }
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
