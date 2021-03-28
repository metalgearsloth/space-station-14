#nullable enable
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
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public sealed class ServerMagazineBarrelComponent : SharedMagazineBarrelComponent
    {
        private ContainerSlot _chamberContainer = default!;
        private ContainerSlot? _magazineContainer;

        public override void Initialize()
        {
            base.Initialize();

            _chamberContainer = Owner.EnsureContainer<ContainerSlot>("magazine-chamber", out var existingChamber);

            if ()
            _magazineContainer = Owner.EnsureContainer<ContainerSlot>("magazine-mag", out var existingMag);

            if (!existingMag && MagFillPrototype != null)
            {
                var mag = Owner.EntityManager.SpawnEntity(MagFillPrototype, Owner.Transform.MapPosition);
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
                    ammo.Push(!entity.GetComponent<SharedAmmoComponent>().Spent);
                    count++;
                }

                for (var i = 0; i < shotsLeft - count; i++)
                {
                    ammo.Push(true);
                }
            }

            return new MagazineBarrelComponentState(BoltOpen, chamber, Selector, ammo);
        }

        protected override bool TrySetBolt(bool value)
        {
            if (BoltOpen == value)
                return false;

            var shooter = Shooter();

            if (value)
            {
                TryEjectChamber();
                if (SoundBoltOpen != null)
                {
                    Owner.PopupMessage(shooter, Loc.GetString("Bolt opened"));
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundBoltOpen, Owner, AudioHelpers.WithVariation(BoltToggleVariation).WithVolume(BoltToggleVolume));
                }
            }
            else
            {
                TryFeedChamber();
                if (SoundBoltClosed != null)
                {
                    Owner.PopupMessage(shooter, Loc.GetString("Bolt closed"));
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundBoltClosed, Owner, AudioHelpers.WithVariation(BoltToggleVariation).WithVolume(BoltToggleVolume));
                }
            }

            BoltOpen = value;
            return true;
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
                EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundMagEject, Owner, AudioHelpers.WithVariation(MagVariation).WithVolume(MagVolume));

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
                EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundMagInsert, Owner, AudioHelpers.WithVariation(MagVariation).WithVolume(MagVolume));

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

        protected override bool UseEntity(IEntity user)
        {
            var mag = _magazineContainer.ContainedEntity;
            if (mag?.GetComponent<SharedRangedMagazineComponent>().ShotsLeft > 0)
            {
                Cycle(true);
                Dirty();
                return true;
            }

            if (BoltOpen && _magazineContainer.ContainedEntity != null)
            {
                RemoveMagazine(user);
                return true;
            }

            if (TrySetBolt(!BoltOpen))
                Dirty();

            return true;
        }
    }
}
