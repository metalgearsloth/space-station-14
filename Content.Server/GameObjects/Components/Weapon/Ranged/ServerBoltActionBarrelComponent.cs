#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public class ServerBoltActionBarrelComponent : SharedBoltActionBarrelComponent
    {
        private ContainerSlot? _chamberContainer;
        private Container? _ammoContainer;
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();
        
        private Queue<IEntity> _toFireAmmo = new Queue<IEntity>();

        public override void Initialize()
        {
            base.Initialize();
            _chamberContainer = ContainerManagerComponent.Ensure<ContainerSlot>("bolt-chamber", Owner, out var existing);
            _ammoContainer = ContainerManagerComponent.Ensure<Container>("bolt-ammo", Owner, out existing);

            if (existing)
            {
                UnspawnedCount--;
            }
            else if (UnspawnedCount > 0)
            {
                // TODO: Do this on pump and revolver
                var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                _chamberContainer?.Insert(entity);
                UnspawnedCount--;
            }
            

            if (existing && _ammoContainer != null)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity);
                    UnspawnedCount--;
                }
            }
            
            Dirty();
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);
            if (session?.AttachedEntity != Shooter())
                return;

            switch (message)
            {
                case BoltChangedComponentMessage msg:
                    SetBolt(msg.BoltOpen);
                    break;
            }
        }

        public override ComponentState GetComponentState()
        {
            var chamber = !_chamberContainer?.ContainedEntity?.GetComponent<SharedAmmoComponent>().Spent;
            var ammo = new Stack<bool?>();

            foreach (var entity in _spawnedAmmo)
            {
                ammo.Push(entity.GetComponent<SharedAmmoComponent>().Spent);
            }

            for (var i = 0; i < UnspawnedCount; i++)
            {
                ammo.Push(true);
            }

            return new BoltActionBarrelComponentState(BoltOpen, chamber, Selector, ammo, SoundGunshot);
        }

        protected override void SetBolt(bool value)
        {
            if (BoltOpen == value)
            {
                return;
            }

            var gunSystem = EntitySystem.Get<SharedRangedWeaponSystem>();

            if (value)
            {
                TryEjectChamber();
                if (SoundBoltOpen != null)
                {
                    gunSystem.PlaySound(Shooter(), Owner, SoundBoltOpen);
                }
            }
            else
            {
                TryFeedChamber();
                if (SoundBoltClosed != null)
                {
                    gunSystem.PlaySound(Shooter(), Owner, SoundBoltClosed);
                }
            }

            BoltOpen = value;
            Dirty();
        }

        protected override void Cycle(bool manual = false)
        {
            // TODO: Do we need this eject chamber?
            TryEjectChamber();
            TryFeedChamber();
            var shooter = Shooter();

            if (_chamberContainer?.ContainedEntity == null && manual)
            {
                SetBolt(true);
                if (shooter != null)
                {
                    Owner.PopupMessage(shooter, Loc.GetString("Bolt opened"));
                }
                return;
            }
            else
            {
                // TODO: Need this because interaction prediction
                //AudioParams.Default.WithVolume(-2)
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(null, Owner, SoundCycle, true);
            }

            Dirty();
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }
            
            if (_chamberContainer?.ContainedEntity != null)
            {
                var chamber = _chamberContainer.ContainedEntity;
                _toFireAmmo.Enqueue(chamber);
                TryEjectChamber();

                if (AutoCycle)
                {
                    Cycle();
                }
                return true;
            }

            if (AutoCycle && _spawnedAmmo.Count > 0)
            {
                Cycle();
            }

            return false;
        }

        protected override void TryEjectChamber()
        {
            var chamber = _chamberContainer?.ContainedEntity;
            if (chamber == null)
                return;

            _chamberContainer?.Remove(chamber);
            EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(Shooter(), chamber);
        }

        protected override void TryFeedChamber()
        {
            if (_spawnedAmmo.TryPop(out var ammo))
            {
                _chamberContainer?.Insert(ammo);
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundCycle, true);
                return;
            }

            if (UnspawnedCount > 0)
            {
                var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                _chamberContainer?.Insert(entity);
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundCycle, true);
                UnspawnedCount--;
            }
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            DebugTools.Assert(shotCount == _toFireAmmo.Count);

            while (_toFireAmmo.Count > 0)
            {
                var entity = _toFireAmmo.Dequeue();
                var ammo = entity.GetComponent<AmmoComponent>();
                var sound = ammo.Spent ? SoundEmpty : SoundGunshot;
                
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, sound);
                EntitySystem.Get<RangedWeaponSystem>().Shoot(Shooter(), direction, ammo, AmmoSpreadRatio);
                ammo.Spent = true;
            }
        }

        public override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (ammoComponent.Caliber != Caliber)
            {
                return false;
            }

            if (_ammoContainer?.ContainedEntities.Count < Capacity - 1)
            {
                _ammoContainer?.Insert(ammoComponent.Owner);
                _spawnedAmmo.Push(ammoComponent.Owner);

                if (SoundInsert != null)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(user, Owner, SoundInsert);
                }
                
                // TODO: when interaction predictions are in remove this.
                Dirty();
                return true;
            }

            return false;
        }
    }
}