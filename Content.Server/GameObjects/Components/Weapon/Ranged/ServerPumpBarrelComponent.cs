#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.Audio;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Server.Player;
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
        private ContainerSlot _chamberContainer = default!;
        private Container _ammoContainer = default!;
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();
        
        private Queue<IEntity> _toFireAmmo = new Queue<IEntity>();

        private float _ammoSpreadRatio;

        protected override int ShotsLeft => UnspawnedCount + _spawnedAmmo.Count;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction("ammoSpreadRatio", 1.0f, value => _ammoSpreadRatio = value, () => _ammoSpreadRatio);
        }

        public override void Initialize()
        {
            base.Initialize();
            if (FillPrototype == null)
            {
                UnspawnedCount = 0;
            }
            else
            {
                UnspawnedCount += Capacity + 1;
            }

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
            
            if (!existing && FillPrototype != null)
            {
                var ammo = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.Coordinates);
                _chamberContainer.Insert(ammo);
                UnspawnedCount--;
            } 
            else if (existing)
            {
                UnspawnedCount--;
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
                _chamberContainer.Remove(chamberEntity);
                
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
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundCycle, Owner, AudioHelpers.WithVariation(CycleVariation));
                }
            }
            
            // TODO: When interaction predictions are in remove this.
            Dirty();
        }
        
        public override bool TryInsertBullet(IEntity user, IEntity ammo)
        {
            // TODO: Also check this out on the revolver for prediction.
            if (!ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                return false;
            }

            if (_ammoContainer.ContainedEntities.Count < Capacity)
            {
                _ammoContainer.Insert(ammo);
                _spawnedAmmo.Push(ammo);

                if (SoundInsert != null)
                {
                    // TODO: Copy stuff from existing with variation, same with alllll the others.
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundInsert, Owner);
                }
                
                Dirty();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, List<Angle> spreads)
        {
            DebugTools.Assert(shotCount == _toFireAmmo.Count);
            var shooter = Shooter();

            for (var i = 0; i < shotCount; i++)
            {
                var ammo = _toFireAmmo.Dequeue();

                if (ammo == null)
                    continue;

                var ammoComp = ammo.GetComponent<AmmoComponent>();
                var sound = ammoComp.Spent ? SoundEmpty : SoundGunshot;
                
                if (sound != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(sound, Owner, AudioHelpers.WithVariation(GunshotVariation), excludedSession: shooter.PlayerSession());

                if (!ammoComp.Spent)
                {
                    EntitySystem.Get<RangedWeaponSystem>().ShootAmmo(shooter, this, spreads[i], ammoComp);
                    ammoComp.Spent = true;
                }
            }
        }
    }
}