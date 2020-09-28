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
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    public sealed class ServerMagazineBarrelComponent : SharedMagazineBarrelComponent
    {
        private ContainerSlot? _chamberContainer;
        private ContainerSlot? _magazineContainer;
        
        private Queue<IEntity> _toFireAmmo = new Queue<IEntity>();

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

        protected override void SetBolt(bool value)
        {
            if (BoltOpen == value)
            {
                return;
            }

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
            var chamberedEntity = _chamberContainer?.ContainedEntity;
            
            if (chamberedEntity != null)
            {
                _chamberContainer?.Remove(chamberedEntity);
                var ammoComponent = chamberedEntity.GetComponent<SharedAmmoComponent>();
                if (!ammoComponent.Caseless)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(Shooter(), chamberedEntity);
                }
            }

            var mag = _magazineContainer?.ContainedEntity;
            // TODO: Change to Shared when predicted
            var magComp = mag?.GetComponent<ServerRangedMagazineComponent>();
            
            if (magComp != null && magComp.TryPop(out var next))
            {
                _chamberContainer?.Insert(next);
            }

            if (manual)
            {
                if (SoundCycle != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundCycle, Owner, AudioParams.Default.WithVolume(-2));
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
                {
                    return;
                }
                
                var ammoComponent = chamberEntity.GetComponent<SharedAmmoComponent>();
                if (!ammoComponent.Caseless)
                {
                    EntitySystem.Get<SharedRangedWeaponSystem>().EjectCasing(Shooter(), chamberEntity);
                }
                else
                {
                    chamberEntity.Delete();
                }
                
                return;
            }
        }

        protected override void TryFeedChamber()
        {
            if (_chamberContainer?.ContainedEntity != null)
            {
                return;
            }

            // Try and pull a round from the magazine to replace the chamber if possible
            var magazine = _magazineContainer?.ContainedEntity;
            var nextCartridge = magazine?.GetComponent<SharedRangedMagazineComponent>();

            if (nextCartridge == null)
            {
                return;
            }

            _chamberContainer?.Insert(nextCartridge.Owner);

            if (AutoEjectMag && magazine != null && magazine.GetComponent<SharedRangedMagazineComponent>().ShotsLeft == 0)
            {
                if (SoundAutoEject != null)
                {
                    EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundAutoEject, Owner, AudioHelpers.WithVariation(AutoEjectVariation), excludedSession: Shooter().PlayerSession());
                }

                _magazineContainer?.Remove(magazine);
            }
            return;
        }

        protected override void RemoveMagazine(IEntity user)
        {
            var mag = _magazineContainer?.ContainedEntity;

            if (mag == null)
            {
                return;
            }

            if (MagNeedsOpenBolt && !BoltOpen)
            {
                Owner.PopupMessage(user, Loc.GetString("Bolt needs to be open"));
                return;
            }

            _magazineContainer?.Remove(mag);
            
            if (SoundMagEject != null)
            {
                // TODO: Variation
                EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundMagEject, Owner, AudioParams.Default.WithVolume(-2));
            }

            if (user.TryGetComponent(out HandsComponent? handsComponent))
            {
                handsComponent.PutInHandOrDrop(mag.GetComponent<ItemComponent>());
            }

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

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            var chamberEntity = _chamberContainer?.ContainedEntity;
            if (chamberEntity != null)
            {
                _toFireAmmo.Enqueue(chamberEntity);
                Cycle();
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

                if (ammoComp.Spent)
                {
                    if (SoundEmpty != null)
                    {
                        EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundEmpty, Owner, AudioHelpers.WithVariation(EmptyVariation), excludedSession: shooter.PlayerSession());
                    }
                }
                else
                {
                    if (SoundGunshot != null)
                    {
                        EntitySystem.Get<AudioSystem>().PlayFromEntity(SoundGunshot, Owner, AudioHelpers.WithVariation(GunshotVariation), excludedSession: shooter.PlayerSession());
                    }
                    
                    EntitySystem.Get<RangedWeaponSystem>().ShootAmmo(shooter, this, spreads[i], ammoComp);
                    ammoComp.Spent = true;
                }
            }
        }
    }
}