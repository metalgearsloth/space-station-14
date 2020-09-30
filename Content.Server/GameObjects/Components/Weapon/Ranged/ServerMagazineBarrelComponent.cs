#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.Audio;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Maths;

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

            _chamberContainer = ContainerManagerComponent.Ensure<ContainerSlot>("magazine-chamber", Owner, out var existingChamber);
            _magazineContainer =
                ContainerManagerComponent.Ensure<ContainerSlot>("magazine-mag", Owner, out var existingMag);

            if (!existingMag && MagFillPrototype != null)
            {
                var mag = Owner.EntityManager.SpawnEntity(MagFillPrototype, Owner.Transform.MapPosition);
                _magazineContainer.Insert(mag);
            }
            
            Dirty();
        }

        public override ComponentState GetComponentState()
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

        protected override void SetBolt(bool value)
        {
            if (BoltOpen == value)
                return;

            if (value)
            {
                TryEjectChamber();
                if (SoundBoltOpen != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundBoltOpen, Owner, AudioHelpers.WithVariation(BoltToggleVariation), excludedSession: Shooter().PlayerSession());
                }
            }
            else
            {
                TryFeedChamber();
                if (SoundBoltClosed != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundBoltClosed, Owner, AudioHelpers.WithVariation(BoltToggleVariation), excludedSession: Shooter().PlayerSession());
                }
            }

            BoltOpen = value;
            Dirty();
        }

        protected override void Cycle(bool manual = false)
        {
            TryEjectChamber();
            TryFeedChamber();

            if (manual)
            {
                if (SoundRack != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundRack, Owner, AudioParams.Default.WithVolume(-2));
                }
            }
            
            Dirty();
        }

        protected override void TryEjectChamber()
        {
            var chamberEntity = _chamberContainer?.ContainedEntity;
            if (chamberEntity != null)
            {
                if (!_chamberContainer?.Remove(chamberEntity) == true)
                    return;

                var ammoComponent = chamberEntity.GetComponent<SharedAmmoComponent>();
                if (!ammoComponent.Caseless)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(Shooter(), chamberEntity);
                }
                else
                {
                    // TODO: Uhh this is megasketch and probably needs a bool override if its during shooting or even remove it from here
                    // TODO: Pretty sure all muzzles are being parented when they shouldn't be
                    chamberEntity.Delete();
                }
                
                return;
            }
        }

        protected override void TryFeedChamber()
        {
            if (_chamberContainer?.ContainedEntity != null)
                return;

            // Try and pull a round from the magazine to replace the chamber if possible
            var magazine = _magazineContainer?.ContainedEntity?.GetComponent<ServerRangedMagazineComponent>();
            IEntity? nextCartridge = null;
            magazine?.TryPop(out nextCartridge);

            if (nextCartridge == null)
                return;

            _chamberContainer?.Insert(nextCartridge);

            if (AutoEjectMag && magazine != null && magazine.ShotsLeft == 0)
            {
                if (SoundAutoEject != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundAutoEject, Owner, AudioHelpers.WithVariation(AutoEjectVariation), excludedSession: Shooter().PlayerSession());

                _magazineContainer?.Remove(magazine.Owner);
            }
            return;
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
                // TODO: Variation
                EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundMagEject, Owner, AudioHelpers.WithVariation(0.1f));

            if (user.TryGetComponent(out HandsComponent? handsComponent))
                handsComponent.PutInHandOrDrop(mag.GetComponent<ItemComponent>());

            Dirty();
        }

        protected override bool TryInsertMag(IEntity user, IEntity mag)
        {
            // TODO: Popups temporary until prediction
            
            // Insert magazine
            if (mag.TryGetComponent(out SharedRangedMagazineComponent? magazineComponent))
            {
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

                if (_magazineContainer?.ContainedEntity == null)
                {
                    if (SoundMagInsert != null)
                    {
                        EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundMagInsert, Owner, AudioHelpers.WithVariation(MagVariation), excludedSession: Shooter().PlayerSession());
                    }

                    Owner.PopupMessage(user, Loc.GetString("Magazine inserted"));
                    _magazineContainer?.Insert(user);
                    Dirty();
                    return true;
                }

                Owner.PopupMessage(user, Loc.GetString("Already holding a magazine"));
                return false;
            }

            return false;
        }

        protected override bool TryInsertAmmo(IEntity user, IEntity ammo)
        {
            // Insert 1 ammo
            if (ammo.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
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
                    .ContainedEntity == null)
                {
                    Owner.PopupMessage(user, Loc.GetString("Ammo inserted"));
                    _chamberContainer?.Insert(ammo);
                    Dirty();
                    return true;
                }

                Owner.PopupMessage(user, Loc.GetString("Chamber full"));
                return false;
            }

            return false;
        }

        protected override bool UseEntity(IEntity user)
        {
            // Behavior:
            // If bolt open just close it
            // If bolt closed then cycle
            //     If we cycle then get next round
            //         If no more round then open bolt

            if (BoltOpen)
            {
                if (SoundBoltClosed != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundBoltClosed, Owner, AudioHelpers.WithVariation(BoltToggleVariation));
                
                Owner.PopupMessage(user, Loc.GetString("Bolt closed"));
                BoltOpen = false;
                return true;
            }

            // Could play a rack-slide specific sound here if you're so inclined (if the chamber is empty but rounds are available)

            Cycle(true);
            return true;
        }

        protected override bool TryShoot(Angle angle)
        {
            if (!base.TryShoot(angle))
                return false;
            
            var chamberEntity = _chamberContainer?.ContainedEntity;
            Cycle();
            var shooter = Shooter();

            if (chamberEntity == null)
            {
                if (SoundEmpty != null)
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundEmpty, Owner, AudioHelpers.WithVariation(EmptyVariation), excludedSession: shooter.PlayerSession());
                
                return true;
            }

            var ammoComp = chamberEntity.GetComponent<AmmoComponent>();
            var sound = ammoComp.Spent ? SoundEmpty : SoundGunshot;
            
            if (sound != null)
                EntitySystem.Get<AudioSystem>().PlayFromEntity(sound, Owner, AudioHelpers.WithVariation(GunshotVariation), excludedSession: shooter.PlayerSession());

            if (!ammoComp.Spent)
            {
                EntitySystem.Get<RangedWeaponSystem>().ShootAmmo(shooter, this, angle, ammoComp);
                EntitySystem.Get<SharedRangedWeaponSystem>().MuzzleFlash(shooter, this, angle);
                ammoComp.Spent = true;
            }

            return true;
        }
    }
}