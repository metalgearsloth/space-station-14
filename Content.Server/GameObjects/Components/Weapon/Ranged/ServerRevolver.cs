#nullable enable
using System;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
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
                if (_firing == value)
                {
                    return;
                }

                _firing = value;
                NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
            }
        }
        
        private bool _firing;

        public override Angle? FireAngle { get; set; }

        public override void Initialize()
        {
            base.Initialize();
            AmmoContainer = ContainerManagerComponent.Ensure<ContainerSlot>("weapon-ammo", Owner, out var existing);

            if (existing)
            {
                DebugTools.Assert(AmmoContainer.ContainedEntities.Count <= AmmoSlots.Length);
                
                foreach (var entity in AmmoContainer.ContainedEntities)
                {
                    AmmoSlots[CurrentSlot] = entity;
                    Cycle();
                }
            }
        }

        public override void MuzzleFlash()
        {
            // TODO
            return;
        }

        protected override bool TryTakeBullet()
        {
            if (base.TryTakeBullet())
            {
                return false;
            }
            
            if (AmmoSlots[CurrentSlot] == null && UnspawnedCount > 0)
            {
                var entity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                AmmoSlots[CurrentSlot] = entity;
                AmmoContainer.Insert(entity);
            }

            if (AmmoSlots[CurrentSlot]?.GetComponent<AmmoComponent>().CanFire() == true)
            {
                Cycle();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            shotCount = Math.Min(shotCount, AmmoSlots.Length);
            var slot = CurrentSlot;
            
            for (var i = 0; i < shotCount; i++)
            {
                slot--;
                var ammo = AmmoSlots[slot];
                DebugTools.AssertNotNull(ammo);
                var ammoComp = ammo.GetComponent<SharedAmmoComponent>();
                AmmoContainer.Remove(ammo);

                for (var j = 0; j < ammoComp.ProjectilesFired; j++)
                {
                    var bullet = Owner
                        .EntityManager
                        .SpawnEntity(ammo.GetComponent<SharedAmmoComponent>().ProjectileId, Owner.Transform.MapPosition);
                    bullet.Transform.LocalRotation = direction;
                }
            }
        }
    }
}