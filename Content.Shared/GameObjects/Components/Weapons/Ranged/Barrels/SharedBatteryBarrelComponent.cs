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
        
        // Sounds
        protected string? SoundPowerCellInsert;
        protected string? SoundPowerCellEject;

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
        public (int count, int max)? PowerCell { get; }

        public BatteryBarrelComponentState(
            FireRateSelector fireRateSelector,
            (int count, int max)? powerCell) :
            base(ContentNetIDs.BATTERY_BARREL)
        {
            FireRateSelector = fireRateSelector;
            PowerCell = powerCell;
        }
    }
}
