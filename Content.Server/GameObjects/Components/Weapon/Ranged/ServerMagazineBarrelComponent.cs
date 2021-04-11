#nullable enable
using System;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public sealed class ServerMagazineBarrelComponent : SharedMagazineBarrelComponent
    {
        private ContainerSlot _chamberContainer = default!;
        private ContainerSlot _magazineContainer = default!;

        public override void Initialize()
        {
            base.Initialize();

            _chamberContainer = Owner.EnsureContainer<ContainerSlot>("magazine-chamber", out var existingChamber);
            _magazineContainer = Owner.EnsureContainer<ContainerSlot>("magazine-mag", out var existingMag);

            if (!existingMag && MagFillPrototype != null)
            {
                var mag = Owner.EntityManager.SpawnEntity(MagFillPrototype.ID, Owner.Transform.MapPosition);
                _magazineContainer.Insert(mag);
                Dirty();
            }
        }

        public override ComponentState GetComponentState(ICommonSession session)
        {
            var chamber = !_chamberContainer.ContainedEntity?.GetComponent<SharedAmmoComponent>().Spent;
            var ammo = new Stack<bool>();
            var mag = _magazineContainer.ContainedEntity?.GetComponent<ServerRangedMagazineComponent>();

            if (mag == null)
            {
                ammo = null;
            }
            else
            {
                var shotsLeft = mag.ShotsLeft;
                var count = 0;

                foreach (var entity in mag.SpawnedAmmo)
                {
                    ammo.Push(!entity.Spent);
                    count++;
                }

                for (var i = 0; i < shotsLeft - count; i++)
                {
                    ammo.Push(true);
                }
            }

            return new MagazineBarrelComponentState(BoltOpen, chamber, Selector, ammo);
        }

        protected override void RemoveMagazine(IEntity user)
        {
            var mag = _magazineContainer?.ContainedEntity;
            if (mag == null)
                return;

            if (MagNeedsOpenBolt && !BoltOpen)
            {
                Owner.PopupMessage(user, Loc.GetString("Bolt needs to be open"));
                return;
            }

            _magazineContainer?.Remove(mag);

            if (SoundMagEject != null)
               SoundSystem.Play(Filter.Pvs(Owner), SoundMagEject, Owner, AudioHelpers.WithVariation(MagVariation).WithVolume(MagVolume));

            if (user.TryGetComponent(out HandsComponent? handsComponent))
                handsComponent.PutInHandOrDrop(mag.GetComponent<ItemComponent>());

            Dirty();
        }

        protected override bool TryInsertMag(IEntity user, IEntity mag)
        {
            // TODO: Popups temporary until prediction

            // Insert magazine
            if (!mag.TryGetComponent(out SharedRangedMagazineComponent? magazineComponent))
                return false;

            if ((MagazineTypes & magazineComponent.MagazineType) == 0)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong magazine type"));
                return false;
            }

            if (magazineComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (MagNeedsOpenBolt && !BoltOpen)
            {
                Owner.PopupMessage(user, Loc.GetString("Need to open bolt first"));
                return false;
            }

            if (_magazineContainer?.ContainedEntity != null)
            {
                Owner.PopupMessage(user, Loc.GetString("Already holding a magazine"));
                return false;
            }

            if (SoundMagInsert != null)
                SoundSystem.Play(Filter.Pvs(Owner), SoundMagInsert, Owner, AudioHelpers.WithVariation(MagVariation).WithVolume(MagVolume));

            Owner.PopupMessage(user, Loc.GetString("Magazine inserted"));
            _magazineContainer?.Insert(mag);
            Dirty();
            return true;
        }

        protected override bool TryInsertAmmo(IEntity user, IEntity ammo)
        {
            // Insert 1 ammo
            if (!ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
                return false;

            if (!BoltOpen)
            {
                Owner.PopupMessage(user, Loc.GetString("Cannot insert ammo while bolt is closed"));
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            if (_chamberContainer?
                .ContainedEntity != null)
            {
                Owner.PopupMessage(user, Loc.GetString("Chamber full"));
                return false;
            }

            Owner.PopupMessage(user, Loc.GetString("Ammo inserted"));
            _chamberContainer?.Insert(ammo);
            Dirty();
            return true;
        }

        public override bool TryRemoveChambered()
        {
            throw new NotImplementedException();
        }

        public override bool TryInsertChamber(SharedAmmoComponent ammo)
        {
            throw new NotImplementedException();
        }

        public override bool TryRemoveMagazine()
        {
            throw new NotImplementedException();
        }

        public override bool TryInsertMagazine(SharedRangedMagazineComponent magazine)
        {
            throw new NotImplementedException();
        }

        protected override bool UseEntity(IEntity user)
        {
            var mag = _magazineContainer.ContainedEntity;
            if (mag?.GetComponent<SharedRangedMagazineComponent>().ShotsLeft > 0)
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().Cycle(this, true);
                Dirty();
                return true;
            }

            if (BoltOpen && _magazineContainer.ContainedEntity != null)
            {
                RemoveMagazine(user);
                return true;
            }

            if (EntitySystem.Get<SharedRangedWeaponSystem>().TrySetBolt(this, !BoltOpen))
                Dirty();

            return true;
        }
    }
}
