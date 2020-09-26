#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedMagazineBarrelComponent : SharedRangedWeaponComponent
    {
        public override string Name => "MagazineBarrel";

        public override uint? NetID => ContentNetIDs.MAGAZINE_BARREL;

        /*
        [ViewVariables]
        private ContainerSlot _chamberContainer;
        [ViewVariables] public bool HasMagazine => _magazineContainer.ContainedEntity != null;
        private ContainerSlot _magazineContainer;
        */

        [ViewVariables] public MagazineType MagazineTypes => _magazineTypes;
        private MagazineType _magazineTypes;
        [ViewVariables] public BallisticCaliber Caliber => _caliber;
        private BallisticCaliber _caliber;

        public ushort Capacity { get; set; }

        protected string? MagFillPrototype;

        public bool BoltOpen { get; protected set; }

        protected bool AutoEjectMag;
        // If the bolt needs to be open before we can insert / remove the mag (i.e. for LMGs)
        public bool MagNeedsOpenBolt { get; private set; }

        protected float AmmoSpreadRatio;

        // Sounds
        protected string? SoundBoltOpen;
        protected string? SoundBoltClosed;
        protected string? SoundRack;
        protected string? SoundCycle;
        protected string? SoundMagInsert;
        protected string? SoundMagEject;
        protected string? SoundAutoEject;

        protected const float AutoEjectVariation = 0.1f;
        protected const float MagVariation = 0.1f;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataReadWriteFunction(
                "magazineTypes",
                new List<MagazineType>(),
                types => types.ForEach(mag => _magazineTypes |= mag), GetMagazineTypes);

            serializer.DataReadWriteFunction("magNeedsOpenBolt", false, value => MagNeedsOpenBolt = value,
                () => MagNeedsOpenBolt);
            
            serializer.DataField(ref _caliber, "caliber", BallisticCaliber.Unspecified);
            serializer.DataField(ref MagFillPrototype, "magFillPrototype", null);
            serializer.DataField(ref AutoEjectMag, "autoEjectMag", false);
            
            serializer.DataReadWriteFunction("ammoSpreadRatio", 1.0f, value => AmmoSpreadRatio = value, () => AmmoSpreadRatio);
            
            serializer.DataField(ref SoundBoltOpen, "soundBoltOpen", null);
            serializer.DataField(ref SoundBoltClosed, "soundBoltClosed", null);
            serializer.DataField(ref SoundRack, "soundRack", null);
            serializer.DataField(ref SoundMagInsert, "soundMagInsert", null);
            serializer.DataField(ref SoundMagEject, "soundMagEject", null);
            serializer.DataField(ref SoundAutoEject, "soundAutoEject", "/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg");
        }

        protected List<MagazineType> GetMagazineTypes()
        {
            var types = new List<MagazineType>();

            foreach (MagazineType mag in Enum.GetValues(typeof(MagazineType)))
            {
                if ((_magazineTypes & mag) != 0)
                {
                    types.Add(mag);
                }
            }

            return types;
        }

        // TODO: Copy these 2 from bolt
        protected abstract void SetBolt(bool value);

        protected abstract void Cycle(bool manual = false);

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            return UseEntity(eventArgs.User);
        }

        protected abstract bool UseEntity(IEntity user);

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            return !BoltOpen;
        }

        protected abstract void TryEjectChamber();

        protected abstract void TryFeedChamber();

        protected abstract void RemoveMagazine(IEntity user);

        protected abstract bool TryInsertMag(IEntity user, IEntity mag);

        protected abstract bool TryInsertAmmo(IEntity user, IEntity ammo);

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (TryInsertMag(eventArgs.User, eventArgs.Using))
            {
                return true;
            }

            if (TryInsertAmmo(eventArgs.User, eventArgs.Using))
            {
                return true;
            }

            return false;
        }
    }
    

    [Flags]
    public enum MagazineType
    {

        Unspecified = 0,
        LPistol = 1 << 0, // Placeholder?
        Pistol = 1 << 1,
        HCPistol = 1 << 2,
        Smg = 1 << 3,
        SmgTopMounted = 1 << 4,
        Rifle = 1 << 5,
        IH = 1 << 6, // Placeholder?
        Box = 1 << 7,
        Pan = 1 << 8,
        Dart = 1 << 9, // Placeholder
        CalicoTopMounted = 1 << 10,
    }
    
    [Serializable, NetSerializable]
    public enum AmmoVisuals
    {
        AmmoCount,
        AmmoMax,
        Spent,
    }

    [Serializable, NetSerializable]
    public enum MagazineBarrelVisuals
    {
        MagLoaded
    }

    [Serializable, NetSerializable]
    public enum BarrelBoltVisuals
    {
        BoltOpen,
    }

    [Serializable, NetSerializable]
    public class MagazineBarrelComponentState : ComponentState
    {
        public bool Chambered { get; }
        public FireRateSelector FireRateSelector { get; }
        public (int count, int max)? Magazine { get; }
        public string SoundGunshot { get; }
        
        public MagazineBarrelComponentState(
            bool chambered, 
            FireRateSelector fireRateSelector, 
            (int count, int max)? magazine,
            string soundGunshot) : 
            base(ContentNetIDs.MAGAZINE_BARREL)
        {
            Chambered = chambered;
            FireRateSelector = fireRateSelector;
            Magazine = magazine;
            SoundGunshot = soundGunshot;
        }
    }
}