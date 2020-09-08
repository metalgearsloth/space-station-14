#nullable enable
using System;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Projectiles;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedWeapon))]
    public sealed class ServerRevolver : SharedRevolverBarrelComponent, IServerRangedWeapon
    {

        public override bool Firing
        {
            get => _firing;
            set
            {
                _firing = value;
            }
        }
        
        private bool _firing;

        public override Angle? FireAngle { get; set; }
        
        // TODO: When client-side predicted containers are in use that and move this to shared.
        private IEntity?[] _ammoSlots = null!;

        private IContainer AmmoContainer { get; set; } = default!;

        protected override ushort Capacity => (ushort) _ammoSlots.Length;
        
        public TimeSpan BurstStop { get; } = TimeSpan.Zero;

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

        public override void Initialize()
        {
            base.Initialize();
            AmmoContainer = ContainerManagerComponent.Ensure<ContainerSlot>("weapon-ammo", Owner, out var existing);

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

        public override void MuzzleFlash()
        {
            // TODO
            return;
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }
            
            if (_ammoSlots[CurrentSlot] == null && UnspawnedCount > 0)
            {
                var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                _ammoSlots[CurrentSlot] = entity;
                AmmoContainer.Insert(entity);
                UnspawnedCount--;
            }

            if (_ammoSlots[CurrentSlot] != null)
            {
                Cycle();
                return true;
            }

            return false;
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
                var ammo = _ammoSlots[slot];
                DebugTools.AssertNotNull(ammo);
                var ammoComp = ammo!.GetComponent<AmmoComponent>();
                
                if (!ammoComp.Can)
                
                // TODO: Need to take bullets here
                var bullet = ammoComp.TakeBullet();

                for (var j = 0; j < ammoComp.ProjectilesFired; j++)
                {
                    if (j > 0)
                    {
                        bullet = Owner
                            .EntityManager
                            .SpawnEntity(ammoComp.ProjectileId, Owner.Transform.MapPosition);
                    }

                    bullet.Transform.LocalRotation = direction;
                    
                    bullet.GetComponent<ProjectileComponent>().IgnoreEntity(Shooter());
                    bullet.GetComponent<CollidableComponent>().LinearVelocity = direction.ToVec() * ammoComp.Velocity;
                }
            }
        }

        protected override ushort EjectAllSlots()
        {
            ushort dumped = 0;
            
            for (var i = 0; i < Capacity; i++)
            {
                var entity = _ammoSlots[i];
                if (entity == null)
                {
                    continue;
                }

                AmmoContainer.Remove(entity);
                // TODO: MANAGER EjectCasing(entity);
                NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
                _ammoSlots[i] = null;
                dumped++;
            }

            // May as well point back at the end?
            CurrentSlot = (ushort) (_ammoSlots.Length - 1);
            return dumped;
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
                    return true;
                }
            }
            
            return false;
        }
    }
}