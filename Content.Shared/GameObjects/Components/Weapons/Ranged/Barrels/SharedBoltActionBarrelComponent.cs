#nullable enable
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedBoltActionBarrelComponent : SharedRangedWeaponComponent
    {
        // Originally I had this logic shared with PumpBarrel and used a couple of variables to control things
        // but it felt a lot messier to play around with, especially when adding verbs

        public override string Name => "BoltActionBarrel";
        public override uint? NetID => ContentNetIDs.BOLTACTION_BARREL;

        [ViewVariables]
        public ushort Capacity { get; set; }

        [ViewVariables]
        public BallisticCaliber Caliber;

        [ViewVariables]
        public string? FillPrototype;
        [ViewVariables]
        protected int UnspawnedCount;

        public bool BoltOpen { get; protected set; }
        protected bool AutoCycle;

        protected float AmmoSpreadRatio;

        // Sounds
        public string? SoundCycle;
        public string? SoundBoltOpen;
        public string? SoundBoltClosed;
        public string? SoundInsert;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("caliber", BallisticCaliber.Unspecified, value => Caliber = value, () => Caliber);
            serializer.DataReadWriteFunction("capacity", Capacity, value => Capacity = value, () => Capacity);
            serializer.DataReadWriteFunction("fillPrototype", null, value => FillPrototype = value, () => FillPrototype);
            serializer.DataReadWriteFunction("autoCycle", false, value => AutoCycle = value, () => AutoCycle);
            serializer.DataReadWriteFunction("ammoSpreadRatio", 1.0f, value => AmmoSpreadRatio = value, () => AmmoSpreadRatio);

            serializer.DataReadWriteFunction("soundCycle", "/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg", value => SoundCycle = value, () => SoundCycle);
            serializer.DataReadWriteFunction("soundBoltOpen", "/Audio/Weapons/Guns/Bolt/rifle_bolt_open.ogg", value => SoundBoltOpen = value, () => SoundBoltOpen);
            serializer.DataReadWriteFunction("soundBoltClosed", "/Audio/Weapons/Guns/Bolt/rifle_bolt_closed.ogg", value => SoundBoltClosed = value, () => SoundBoltClosed);
            serializer.DataReadWriteFunction("soundInsert", "/Audio/Weapons/Guns/MagIn/bullet_insert.ogg", value => SoundInsert = value, () => SoundInsert);
        }

        public override void Initialize()
        {
            base.Initialize();
            if (FillPrototype != null)
            {
                // TODO: Update revolvers and pump to do this
                UnspawnedCount += Capacity;
            }
        }

        protected abstract void SetBolt(bool value);

        protected abstract void TryEjectChamber();

        protected abstract void TryFeedChamber();

        protected abstract void Cycle(bool manual = false);

        public abstract bool TryInsertBullet(IEntity user, IEntity ammo);

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }
            
            // TODO: Any guns that use UnspawnedAmmo should try and spawn one into the chamber if possible.
            return !BoltOpen;
        }

        /*
        public bool TryInsertBullet(IEntity user, IEntity ammo)
        {
            if (!ammo.TryGetComponent(out AmmoComponent ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (!BoltOpen)
            {
                Owner.PopupMessage(user, Loc.GetString("Bolt isn't open"));
                return false;
            }

            if (_chamberContainer.ContainedEntity == null)
            {
                _chamberContainer.Insert(ammo);
                if (_soundInsert != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayAtCoords(_soundInsert, Owner.Transform.Coordinates, AudioParams.Default.WithVolume(-2));
                }
                Dirty();
                UpdateAppearance();
                return true;
            }

            if (_ammoContainer.ContainedEntities.Count < Capacity - 1)
            {
                _ammoContainer.Insert(ammo);
                _spawnedAmmo.Push(ammo);
                if (_soundInsert != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayAtCoords(_soundInsert, Owner.Transform.Coordinates, AudioParams.Default.WithVolume(-2));
                }
                Dirty();
                UpdateAppearance();
                return true;
            }

            Owner.PopupMessage(user, Loc.GetString("No room"));

            return false;
        }
        */

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            return TryInsertBullet(eventArgs.User, eventArgs.Using);
        }

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            if (BoltOpen)
            {
                SetBolt(false);
                // TODO: Predict when they're in plx
                Owner.PopupMessage(eventArgs.User, Loc.GetString("Bolt closed"));
                return true;
            }

            Cycle(true);

            return true;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BoltActionBarrelComponentState : ComponentState
    {
        public bool BoltOpen { get; }
        public bool? Chamber { get; }
        public FireRateSelector FireRateSelector { get; }
        public Stack<bool?> Bullets { get; }
        public string? SoundGunshot { get; }

        public BoltActionBarrelComponentState(
            bool boltOpen,
            bool? chamber,
            FireRateSelector fireRateSelector,
            Stack<bool?> bullets,
            string? soundGunshot) :
            base(ContentNetIDs.BOLTACTION_BARREL)
        {
            BoltOpen = boltOpen;
            Chamber = chamber;
            FireRateSelector = fireRateSelector;
            Bullets = bullets;
            SoundGunshot = soundGunshot;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BoltChangedComponentMessage : ComponentMessage
    {
        public bool BoltOpen { get; }

        public BoltChangedComponentMessage(bool boltOpen)
        {
            BoltOpen = boltOpen;
            Directed = true;
        }
    }
}
