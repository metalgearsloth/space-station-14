#nullable enable
using System;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Server.GameObjects.Components.Container;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeaponComponent))]
    public sealed class ServerRevolverBarrelComponent : SharedRevolverBarrelComponent
    {
        private IEntity?[] _ammoSlots = null!;

        private IContainer AmmoContainer { get; set; } = default!;

        protected override ushort Capacity => (ushort) _ammoSlots.Length;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataReadWriteFunction(
                "capacity",
                6,
                cap => _ammoSlots = new IEntity[cap],
                () => _ammoSlots.Length);
            serializer.DataField(ref FillPrototype, "fillPrototype", null);
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);

            IActorComponent? actorComponent = null;
            var user = Shooter();
            user?.TryGetComponent(out actorComponent);

            if (user == null || session != actorComponent?.playerSession)
            {
                // Cheater / lagger?
                return;
            }

            switch (message)
            {
                case RevolverSpinMessage msg:
                    CurrentSlot = msg.Slot;
                    EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(user, Owner, SoundSpin, true);
                    break;
            }
        }

        public override ComponentState GetComponentState()
        {
            var bullets = new bool?[_ammoSlots.Length];
            for (var i = 0; i < _ammoSlots.Length; i++)
            {
                bullets[i] = !_ammoSlots[i]?.GetComponent<SharedAmmoComponent>().Spent;
            }
            
            return new RevolverBarrelComponentState(CurrentSlot, Selector, bullets, SoundGunshot);
        }

        public override void Initialize()
        {
            base.Initialize();
            AmmoContainer = ContainerManagerComponent.Ensure<Container>("weapon-ammo", Owner, out var existing);

            if (existing)
            {
                DebugTools.Assert(AmmoContainer.ContainedEntities.Count <= _ammoSlots.Length);
                
                foreach (var entity in AmmoContainer.ContainedEntities)
                {
                    _ammoSlots[CurrentSlot] = entity;
                    Cycle();
                    UnspawnedCount--;
                }
            }

            if (FillPrototype != null)
            {
                UnspawnedCount += (ushort) _ammoSlots.Length;
            }
        }

        protected override bool TryTakeAmmo()
        {
            // Revolvers can keep cycling even if the ammo is invalid.
            if (!base.TryTakeAmmo())
            {
                return false;
            }

            var currentAmmo = _ammoSlots[CurrentSlot];

            if (currentAmmo == null)
            {
                if (UnspawnedCount > 0)
                {
                    var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                    _ammoSlots[CurrentSlot] = entity;
                    AmmoContainer.Insert(entity);
                    UnspawnedCount--;
                }
                else
                {
                    return false;
                }
            } 
            else
            {
                if (currentAmmo.GetComponent<AmmoComponent>().Spent)
                {
                    Cycle();
                    return false;
                }
            }

            Cycle();
            return true;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            // TODO: Copy existing shooting code rather than re-inventing the wheel
            // Feed in the ammo and do what you need to do with it.
            // Also TODO: Make this common to all projectile guns
            shotCount = Math.Min(shotCount, _ammoSlots.Length);
            var slot = CurrentSlot;

            for (var i = 0; i < shotCount; i++)
            {
                slot = (ushort) (slot == 0 ? _ammoSlots.Length - 1 : slot - 1);
                var entity = _ammoSlots[slot];

                if (entity == null)
                    continue;
                
                var ammo = entity.GetComponent<AmmoComponent>();
                var sound = ammo.Spent ? SoundEmpty : SoundGunshot;

                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, sound);
                EntitySystem.Get<RangedWeaponSystem>().Shoot(Shooter(), direction, ammo);
                ammo.Spent = true;
            }
        }

        protected override void EjectAllSlots()
        {
            var gunSystem = EntitySystem.Get<SharedRangedWeaponSystem>();
            // How many times we can play the sound
            const ushort soundCap = 3;
            ushort soundCount = 0;
            
            for (var i = 0; i < Capacity; i++)
            {
                var entity = _ammoSlots[i];
                if (entity == null)
                {
                    if (UnspawnedCount > 0)
                    {
                        entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                        UnspawnedCount--;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    AmmoContainer.Remove(entity);
                }
                
                // TODO: Set to user when prediction is in
                _ammoSlots[i] = null;

                gunSystem.EjectCasing(null, entity, soundCount < soundCap);
                soundCount++;
            }

            // May as well point back at the end?
            CurrentSlot = (ushort) (_ammoSlots.Length - 1);
            Dirty();
            return;
        }

        protected override bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (!base.TryInsertBullet(user, ammoComponent))
                return false;

            // Functions like a stack
            // These are inserted in reverse order but then when fired Cycle will go through in order
            // The reason we don't just use an actual stack is because spin can select a random slot to point at
            for (var i = _ammoSlots.Length - 1; i >= 0; i--)
            {
                var slot = _ammoSlots[i];
                if (slot == null)
                {
                    CurrentSlot = (byte) i;
                    _ammoSlots[i] = ammoComponent.Owner;
                    AmmoContainer.Insert(ammoComponent.Owner);
                    NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
                    EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(null, Owner, SoundInsert, true);
                    // Won't need dirty once prediction is in, place we can pass in the actual user above.
                    Dirty();
                    return true;
                }
            }
            
            return false;
        }
    }
}