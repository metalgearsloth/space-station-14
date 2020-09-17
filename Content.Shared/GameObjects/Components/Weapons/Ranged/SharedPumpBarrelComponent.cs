using System.Threading.Tasks;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public abstract class SharedPumpBarrelComponent : SharedRangedWeaponComponent
    {
        public override string Name => "PumpBarrel";
        public override uint? NetID => ContentNetIDs.PUMP_BARREL;

        public ushort Capacity { get; private set; }

        // Even a point having a chamber? I guess it makes some of the below code cleaner

        [ViewVariables]
        protected BallisticCaliber Caliber;

        [ViewVariables]
        protected string FillPrototype;
        [ViewVariables]
        protected ushort UnspawnedCount;

        protected bool ManualCycle;

        // Sounds
        protected string SoundCycle;
        protected string SoundInsert;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("capacity", (ushort) 1, value => Capacity = value, () => Capacity);
            serializer.DataField(ref Caliber, "caliber", BallisticCaliber.Unspecified);
            serializer.DataField(ref FillPrototype, "fillPrototype", null);
            serializer.DataField(ref ManualCycle, "manualCycle", true);

            serializer.DataField(ref SoundCycle, "soundCycle", "/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg");
            serializer.DataField(ref SoundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/bullet_insert.ogg");
        }

        public override void Initialize()
        {
            base.Initialize();
            UnspawnedCount = FillPrototype != null ? Capacity : (ushort) 0;
        }

        protected abstract void Cycle(bool manual = false);

        public abstract bool TryInsertBullet(InteractUsingEventArgs eventArgs);

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            Cycle(true);
            return true;
        }

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsertBullet(eventArgs);
        }
    }
}
