#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;

namespace Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedMagazineComponent))]
    public class ServerRangedMagazineComponent : SharedRangedMagazineComponent
    {
        private Container _ammoContainer = default!;

        public IReadOnlyCollection<IEntity> SpawnedAmmo => _spawnedAmmo;
        private Stack<IEntity> _spawnedAmmo = new Stack<IEntity>();
        
        public override int ShotsLeft => _spawnedAmmo.Count + UnspawnedCount;

        public override void Initialize()
        {
            base.Initialize();
            
            _ammoContainer = ContainerManagerComponent.Ensure<Container>($"{Name}-magazine", Owner, out var existing);

            if (existing)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity);
                    UnspawnedCount--;
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
                
            return new RangedMagazineComponentState(ammo);
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

        protected override bool TryInsertAmmo(IEntity user, IEntity ammo)
        {
            // TODO: Move popups to client-side when possible
            if (!ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (ShotsLeft >= Capacity)
            {
                Owner.PopupMessage(user, Loc.GetString("Magazine is full"));
                return false;
            }

            _ammoContainer.Insert(ammo);
            _spawnedAmmo.Push(ammo);
            Dirty();
            return true;
        }

        protected override bool Use(IEntity user)
        {
            if (!user.TryGetComponent(out HandsComponent? handsComponent))
            {
                return false;
            }

            if (!TryPop(out var ammo))
            {
                return false;
            }

            var itemComponent = ammo.GetComponent<ItemComponent>();
            if (!handsComponent.CanPutInHand(itemComponent))
            {
                ammo.Transform.Coordinates = user.Transform.Coordinates;
                EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(user, ammo);
            }
            else
            {
                handsComponent.PutInHand(itemComponent);
            }

            return true;
        }
    }
}