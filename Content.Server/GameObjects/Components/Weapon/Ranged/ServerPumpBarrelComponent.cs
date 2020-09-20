using System;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public sealed class ServerPumpBarrelComponent : SharedPumpBarrelComponent
    {
        private ContainerSlot _chamberContainer;
        private Container _ammoContainer;
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();
        
        private Queue<IEntity> _toFireAmmo = new Queue<IEntity>();

        private float _ammoSpreadRatio;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("ammoSpreadRatio", 1.0f, value => _ammoSpreadRatio = value, () => _ammoSpreadRatio);
        }

        public override void Initialize()
        {
            base.Initialize();

            _ammoContainer =
                ContainerManagerComponent.Ensure<Container>($"{Name}-ammo-container", Owner, out var existing);

            if (existing)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity);
                    UnspawnedCount--;
                }
            }

            _chamberContainer =
                ContainerManagerComponent.Ensure<ContainerSlot>($"{Name}-chamber-container", Owner, out existing);
            
            if (existing)
            {
                UnspawnedCount--;
            }

            if (FillPrototype == null)
            {
                UnspawnedCount = 0;
            }

            if (false)
            {
                // TODO: Log
            }
            
            Dirty();
        }

        public override ComponentState GetComponentState()
        {
            var chamber = !_chamberContainer.ContainedEntity?.GetComponent<SharedAmmoComponent>().Spent;
            var ammo = new Stack<bool>();
            foreach (var entity in _spawnedAmmo)
            {
                ammo.Push(!entity.GetComponent<SharedAmmoComponent>().Spent);
            }

            for (var i = 0; i < UnspawnedCount; i++)
            {
                ammo.Push(true);
            }
            
            return new PumpBarrelComponentState(chamber, Selector, Capacity, ammo);
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            var chamberEntity = _chamberContainer.ContainedEntity;
            if (chamberEntity != null)
            {
                _toFireAmmo.Enqueue(chamberEntity);
                if (!ManualCycle)
                {
                    Cycle();
                }
                return true;
            }

            return false;
        }

        protected override void Cycle(bool manual = false)
        {
            var chamberedEntity = _chamberContainer.ContainedEntity;
            if (chamberedEntity != null)
            {
                _chamberContainer.Remove(chamberedEntity);
                var ammoComponent = chamberedEntity.GetComponent<SharedAmmoComponent>();
                if (!ammoComponent.Caseless)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(Shooter(), chamberedEntity);
                }
            }

            if (_spawnedAmmo.TryPop(out var next))
            {
                _ammoContainer.Remove(next);
                _chamberContainer.Insert(next);
            }

            if (UnspawnedCount > 0)
            {
                UnspawnedCount--;
                var ammoEntity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.Coordinates);
                _chamberContainer.Insert(ammoEntity);
            }

            if (manual)
            {
                if (SoundCycle != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayAtCoords(SoundCycle, Owner.Transform.Coordinates, AudioParams.Default.WithVolume(-2));
                }
            }
            
            // TODO: When interaction predictions are in remove this.
            Dirty();
        }

        public override bool TryInsertBullet(InteractUsingEventArgs eventArgs)
        {
            // TODO: Also check this out on the revolver for prediction.
            if (!eventArgs.Using.TryGetComponent(out SharedAmmoComponent ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                return false;
            }

            if (_ammoContainer.ContainedEntities.Count < Capacity - 1)
            {
                _ammoContainer.Insert(eventArgs.Using);
                _spawnedAmmo.Push(eventArgs.Using);

                if (SoundInsert != null)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(eventArgs.User, Owner, SoundInsert);
                }
                
                // TODO: when interaction predictions are in remove this.
                Dirty();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            DebugTools.Assert(shotCount == _toFireAmmo.Count);

            while (_toFireAmmo.Count > 0)
            {
                var ammo = _toFireAmmo.Dequeue();

                if (ammo == null)
                    continue;

                // TODO: This should be removed elsewhere
                if (_ammoContainer.Contains(ammo))
                    _ammoContainer.Remove(ammo);
                
                var ammoComp = ammo.GetComponent<AmmoComponent>();

                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundGunshot);
                EntitySystem.Get<RangedWeaponSystem>().Shoot(Shooter(), direction, ammoComp, _ammoSpreadRatio);
                ammoComp.Spent = true;
            }
        }
    }
}