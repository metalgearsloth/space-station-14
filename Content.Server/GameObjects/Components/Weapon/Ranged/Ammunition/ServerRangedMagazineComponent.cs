#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedMagazineComponent))]
    public class ServerRangedMagazineComponent : SharedRangedMagazineComponent
    {
        private Container _ammoContainer = default!;

        public IReadOnlyCollection<SharedAmmoComponent> SpawnedAmmo => _spawnedAmmo;
        private Stack<SharedAmmoComponent> _spawnedAmmo = new();

        public override int ShotsLeft => _spawnedAmmo.Count + UnspawnedCount;

        public override void Initialize()
        {
            base.Initialize();

            _ammoContainer = Owner.EnsureContainer<Container>($"{Name}-magazine", out var existing);

            if (FillPrototype != null)
            {
                UnspawnedCount += Capacity;
            }
            else
            {
                UnspawnedCount = 0;
            }

            if (existing)
            {
                foreach (var entity in _ammoContainer.ContainedEntities)
                {
                    _spawnedAmmo.Push(entity.GetComponent<SharedAmmoComponent>());
                    UnspawnedCount--;
                }
            }

            Dirty();
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);

            // If it's not on the ground / in our inventory then block it
            if (ContainerHelpers.TryGetContainer(Owner, out var container) && container.Owner != session?.AttachedEntity)
                return;

            switch (message)
            {
                case DumpRangedMagazineComponentMessage msg:
                    Dump(session?.AttachedEntity, msg.Amount);
                    break;
            }
        }

        public override ComponentState GetComponentState(ICommonSession session)
        {
            var ammo = new Stack<bool>();

            foreach (var ammoComp in _spawnedAmmo)
            {
                ammo.Push(!ammoComp.Spent);
            }

            for (var i = 0; i < UnspawnedCount; i++)
            {
                ammo.Push(true);
            }

            return new RangedMagazineComponentState(ammo);
        }

        public void Dump(IEntity? user, int amount)
        {
            var count = Math.Min(amount, ShotsLeft);
            const byte maxSounds = 3;
            var soundsPlayed = 0;

            for (var i = 0; i < count; i++)
            {
                if (!TryPop(out var ammo))
                    break;

                EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(user, ammo.Owner, soundsPlayed < maxSounds);
                soundsPlayed++;
            }
        }

        public override bool TryPop([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            if (_spawnedAmmo.TryPop(out ammo))
            {
                Dirty();
                return true;
            }

            if (UnspawnedCount > 0)
            {
                ammo = Owner.EntityManager.SpawnEntity(FillPrototype!.ID, Owner.Transform.Coordinates).GetComponent<SharedAmmoComponent>();
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
            _spawnedAmmo.Push(ammoComponent);
            Dirty();
            return true;
        }

        protected override bool Use(IEntity user)
        {
            if (!user.TryGetComponent(out HandsComponent? handsComponent))
                return false;

            if (!TryPop(out var ammo))
                return false;

            var itemComponent = ammo.Owner.GetComponent<ItemComponent>();
            if (!handsComponent.CanPutInHand(itemComponent))
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(user, ammo.Owner);
            }
            else
            {
                handsComponent.PutInHand(itemComponent);
            }

            return true;
        }
    }
}
