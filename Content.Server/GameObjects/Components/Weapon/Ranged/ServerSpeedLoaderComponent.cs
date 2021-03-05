#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedSpeedLoaderComponent))]
    public sealed class ServerSpeedLoaderComponent : SharedSpeedLoaderComponent
    {
        // TODO: check out the other weapons and just use default!
        private Container _ammoContainer = default!;
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();

        public override int ShotsLeft => UnspawnedCount + _spawnedAmmo.Count;

        public override void Initialize()
        {
            base.Initialize();
            _ammoContainer = Owner.EnsureContainer<Container>($"{Name}-container", out var existing);

            if (existing)
            {
                foreach (var ammo in _ammoContainer.ContainedEntities)
                {
                    UnspawnedCount--;
                    _spawnedAmmo.Push(ammo);
                }
            }

            Dirty();
        }

        public override ComponentState GetComponentState(ICommonSession session)
        {
            var ammo = new Stack<bool>();

            foreach (var entity in _spawnedAmmo)
            {
                ammo.Push(!entity.GetComponent<SharedAmmoComponent>().Spent);
            }

            for (var i = 0; i < UnspawnedCount; i++)
            {
                ammo.Push(true);
            }

            return new SpeedLoaderComponentState(Capacity, ammo);
        }

        public bool TryPop([NotNullWhen(true)] out IEntity? entity)
        {
            if (_spawnedAmmo.TryPop(out entity))
            {
                Dirty();
                return true;
            }

            if (UnspawnedCount > 0)
            {
                entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.Coordinates);
                UnspawnedCount--;
                Dirty();
                return true;
            }

            return false;
        }

        public override bool TryInsertAmmo(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (!base.TryInsertAmmo(user, ammoComponent))
                return false;

            if (ShotsLeft >= Capacity)
            {
                Owner.PopupMessage(user, Loc.GetString("Already full"));
                return false;
            }

            _ammoContainer.Insert(ammoComponent.Owner);
            _spawnedAmmo.Push(ammoComponent.Owner);
            return true;
        }

        protected override bool UseEntity(IEntity user)
        {
            if (!user.TryGetComponent(out HandsComponent? handsComponent))
                return false;

            if (!TryPop(out var ammo))
                return false;

            var itemComponent = ammo.GetComponent<ItemComponent>();
            if (!handsComponent.CanPutInHand(itemComponent))
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(user, ammo);
            }
            else
            {
                handsComponent.PutInHand(itemComponent);
            }

            Dirty();
            return true;
        }

        protected override async Task<bool> AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (eventArgs.Target == null)
                return false;

            // This area is dirty but not sure of an easier way to do it besides add an interface or somethin
            bool changed = false;

            if (eventArgs.Target.TryGetComponent(out SharedRevolverBarrelComponent? revolverBarrel))
            {
                if (Caliber != revolverBarrel.Caliber)
                    return false;

                for (var i = 0; i < Capacity; i++)
                {
                    if (!TryPop(out var ammo))
                        break;

                    var ammoComponent = ammo.GetComponent<SharedAmmoComponent>();
                    if (revolverBarrel.TryInsertBullet(eventArgs.User, ammoComponent))
                    {
                        changed = true;
                        continue;
                    }

                    // Take the ammo back
                    TryInsertAmmo(eventArgs.User, ammoComponent);
                    break;
                }
            }
            else if (eventArgs.Target.TryGetComponent(out SharedBoltActionBarrelComponent? boltActionBarrel))
            {
                if (Caliber != boltActionBarrel.Caliber)
                    return true;

                for (var i = 0; i < Capacity; i++)
                {
                    if (!TryPop(out var ammo))
                        break;

                    var ammoComponent = ammo.GetComponent<SharedAmmoComponent>();
                    if (boltActionBarrel.TryInsertBullet(eventArgs.User, ammoComponent))
                    {
                        changed = true;
                        continue;
                    }

                    // Take the ammo back
                    TryInsertAmmo(eventArgs.User, ammoComponent);
                    break;
                }

            }

            if (changed)
            {
                Dirty();
            }

            return true;
        }
    }
}
