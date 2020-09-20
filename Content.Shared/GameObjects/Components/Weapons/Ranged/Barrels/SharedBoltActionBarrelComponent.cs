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

        public ushort Capacity { get; set; }

        /* TODO: Server
        private ContainerSlot _chamberContainer;
        private Stack<IEntity> _spawnedAmmo;
        private Container _ammoContainer;
        */

        [ViewVariables]
        protected BallisticCaliber Caliber;

        [ViewVariables]
        protected string FillPrototype;
        [ViewVariables]
        protected int UnspawnedCount;

        public bool BoltOpen { get; protected set; }
        protected bool AutoCycle;

        protected float AmmoSpreadRatio;

        // Sounds
        protected string SoundCycle;
        protected string SoundBoltOpen;
        protected string SoundBoltClosed;
        protected string SoundInsert;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref Caliber, "caliber", BallisticCaliber.Unspecified);
            serializer.DataReadWriteFunction("capacity", Capacity, value => Capacity = value, () => Capacity);
            serializer.DataField(ref FillPrototype, "fillPrototype", null);
            serializer.DataField(ref AutoCycle, "autoCycle", false);
            serializer.DataReadWriteFunction("ammoSpreadRatio", 1.0f, value => AmmoSpreadRatio = value, () => AmmoSpreadRatio);

            serializer.DataField(ref SoundCycle, "soundCycle", "/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg");
            serializer.DataField(ref SoundBoltOpen, "soundBoltOpen", "/Audio/Weapons/Guns/Bolt/rifle_bolt_open.ogg");
            serializer.DataField(ref SoundBoltClosed, "soundBoltClosed", "/Audio/Weapons/Guns/Bolt/rifle_bolt_closed.ogg");
            serializer.DataField(ref SoundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/bullet_insert.ogg");
        }

        /*
        public override ComponentState GetComponentState()
        {
            (int, int)? count = (ShotsLeft, Capacity);
            var chamberedExists = _chamberContainer.ContainedEntity != null;
            // (Is one chambered?, is the bullet spend)
            var chamber = (chamberedExists, false);
            if (chamberedExists && _chamberContainer.ContainedEntity.TryGetComponent<AmmoComponent>(out var ammo))
            {
                chamber.Item2 = ammo.Spent;
            }

            return new BoltActionBarrelComponentState(
                chamber,
                FireRateSelector,
                count,
                SoundGunshot);
        }
        */
        
        protected override bool CanFire(IEntity entity)
        {
            if (!base.CanFire(entity))
            {
                return false;
            }

            return !BoltOpen;
        }

        protected abstract void SetBolt(bool value);

        protected abstract void TryEjectChamber();

        protected abstract void TryFeedChamber();

        /*
        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            if (_autoCycle)
            {
                Cycle();
            }
            else
            {
                Dirty();
            }
        }
        */

        protected abstract void Cycle(bool manual = false);

        protected abstract bool TryInsertBullet(IEntity user, IEntity ammo);
            
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

        /*
        private bool TryEjectChamber()
        {
            var chamberedEntity = _chamberContainer.ContainedEntity;
            if (chamberedEntity != null)
            {
                if (!_chamberContainer.Remove(chamberedEntity))
                {
                    return false;
                }
                if (!chamberedEntity.GetComponent<AmmoComponent>().Caseless)
                {
                    EjectCasing(chamberedEntity);
                }
                return true;
            }
            return false;
        }
        */

        /*
        private bool TryFeedChamber()
        {
            if (_chamberContainer.ContainedEntity != null)
            {
                return false;
            }
            if (_spawnedAmmo.TryPop(out var next))
            {
                _ammoContainer.Remove(next);
                _chamberContainer.Insert(next);
                return true;
            }
            else if (UnspawnedCount > 0)
            {
                UnspawnedCount--;
                var ammoEntity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.Coordinates);
                _chamberContainer.Insert(ammoEntity);
                return true;
            }
            return false;
        }
        */

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
    public class BoltActionBarrelComponentState : ComponentState
    {
        public bool? Chamber { get; }
        public FireRateSelector FireRateSelector { get; }
        public Stack<bool?> Bullets { get; }
        public string SoundGunshot { get; }

        public BoltActionBarrelComponentState(
            bool? chamber,
            FireRateSelector fireRateSelector,
            Stack<bool?> magazine,
            string soundGunshot) :
            base(ContentNetIDs.BOLTACTION_BARREL)
        {
            Chamber = chamber;
            FireRateSelector = fireRateSelector;
            Bullets = Bullets;
            SoundGunshot = soundGunshot;
        }
    }
}
