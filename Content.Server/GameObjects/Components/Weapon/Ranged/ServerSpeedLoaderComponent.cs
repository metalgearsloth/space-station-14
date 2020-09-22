#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.Interfaces.GameObjects.Components;

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
            _ammoContainer = ContainerManagerComponent.Ensure<Container>($"{Name}-container", Owner, out var existing);

            if (existing)
            {
                foreach (var ammo in _ammoContainer.ContainedEntities)
                {
                    UnspawnedCount--;
                    _spawnedAmmo.Push(ammo);
                }
            }
        }

        public override ComponentState GetComponentState()
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

        protected void AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (eventArgs.Target == null)
            {
                return;
            }

            // This area is dirty but not sure of an easier way to do it besides add an interface or somethin
            bool changed = false;

            if (eventArgs.Target.TryGetComponent(out SharedRevolverBarrelComponent? revolverBarrel))
            {
                if (Caliber != revolverBarrel.Caliber)
                    return;
                
                for (var i = 0; i < Capacity; i++)
                {
                    if (!TryPop(out var ammo))
                        break;

                    // TODO: Update this I guess to make it consistent
                    if (revolverBarrel.TryInsertBullet(eventArgs.User, ammo))
                    {
                        changed = true;
                        continue;
                    }

                    // Take the ammo back
                    TryInsertAmmo(eventArgs.User, ammo);
                    break;
                }
            } 
            else if (eventArgs.Target.TryGetComponent(out SharedBoltActionBarrelComponent? boltActionBarrel))
            {
                if (Caliber != boltActionBarrel.Caliber)
                    return;
                
                for (var i = 0; i < Capacity; i++)
                {
                    if (!TryPop(out var ammo))
                    {
                        break;
                    }

                    if (boltActionBarrel.TryInsertBullet(eventArgs.User, ammo))
                    {
                        changed = true;
                        continue;
                    }

                    // Take the ammo back
                    TryInsertAmmo(eventArgs.User, ammo);
                    break;
                }

            }

            if (changed)
            {
                Dirty();
            }
        }
    }
}