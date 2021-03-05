#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public sealed class ServerPumpBarrelComponent : SharedPumpBarrelComponent
    {
        private ContainerSlot _chamberContainer = default!;
        private Container _ammoContainer = default!;
        private Stack<IEntity> _spawnedAmmo = new();

        protected override int ShotsLeft => UnspawnedCount + _spawnedAmmo.Count;

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

            _ammoContainer = Owner.EnsureContainer<Container>($"{Name}-ammo-container", out var existing);

            if (existing)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity);
                    UnspawnedCount--;
                }
            }

            _chamberContainer = Owner.EnsureContainer<ContainerSlot>($"{Name}-chamber-container", out existing);

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

        public override ComponentState GetComponentState(ICommonSession session)
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
                if (SoundRack != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundRack, Owner, AudioHelpers.WithVariation(CycleVariation).WithVolume(CycleVolume));
            }
        }

        public override bool TryInsertBullet(IEntity user, IEntity ammo)
        {
            if (!ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
                return false;

            if (ammoComponent.Caliber != Caliber)
                return false;

            if (_ammoContainer.ContainedEntities.Count < Capacity)
            {
                _ammoContainer.Insert(ammo);
                _spawnedAmmo.Push(ammo);

                if (SoundInsert != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundInsert, Owner, AudioHelpers.WithVariation(InsertVariation).WithVolume(CycleVariation));

                Dirty();
                return true;
            }

            return false;
        }
    }
}
