using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Projectiles;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal sealed class RangedWeaponSystem : EntitySystem
    {
        // Handles fire positions etc. from clients
        // It'd be cleaner to have this under corresponding Client / Server components buuuttt the issue with that is
        // you wouldn't be able to inherit from "SharedBlankWeapon" and would instead need to make
        // discrete server and client versions of each weapon that don't inherit from shared.
        
        // e.g. SharedRangedWeapon -> ServerRevolver and SharedRangedWeapon -> ClientRevolver
        // (Handles syncing via component)
        // vs.
        // SharedRangedWeapon -> SharedRevolver -> ServerRevolver
        // (needs to sync via system or component spaghetti)

        [Dependency] private readonly IRobustRandom _robustRandom = default!;

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            
            foreach (var comp in ComponentManager.EntityQuery<SharedRangedWeapon>())
            {
                Update(comp, currentTime);
            }
        }
        
        private void Update(SharedRangedWeapon weapon, TimeSpan currentTime)
        {
            if (weapon.FiringStart == null && weapon.FiringEnd == null)
            {
                return;
            }
            
            if ((weapon.FiringStart != null && weapon.FiringStart > currentTime) && (weapon.FiringEnd != null && currentTime > weapon.FiringEnd))
            {
                return;
            }
            
            // TODO: Shitcode
            var shooter = weapon.Shooter();
            
            if (shooter == null)
            {
                return;
            }

            weapon.TryFire(currentTime, shooter, weapon.FireAngle!.Value);
        }

        public void Shoot(IEntity user, Angle angle, AmmoComponent ammoComponent, float spreadRatio = 1.0f)
        {
            if (!ammoComponent.CanFire())
                return;
            
            List<Angle> sprayAngleChange = null;
            var count = ammoComponent.ProjectilesFired;
            var evenSpreadAngle = ammoComponent.EvenSpreadAngle;

            if (ammoComponent.AmmoIsProjectile)
            {
                Fire(user, ammoComponent.Owner, angle, ammoComponent.Velocity);
            }

            if (count > 1)
            {
                // TODO: Queue angles server-side alongside timestamp from client and get latest angle
                // Ideally you'd also roll client position back maybe..., idk
                evenSpreadAngle *= spreadRatio;
                sprayAngleChange = Linspace(-evenSpreadAngle / 2, evenSpreadAngle / 2, count);
            }

            for (var i = 0; i < count; i++)
            {
                var projectile =
                        EntityManager.SpawnEntity(ammoComponent.ProjectileId, ammoComponent.Owner.Transform.MapPosition);
                
                Angle projectileAngle;

                if (sprayAngleChange != null)
                {
                    projectileAngle = angle + sprayAngleChange[i];
                }
                else
                {
                    projectileAngle = angle;
                }

                Fire(user, projectile, projectileAngle, ammoComponent.Velocity);
            }
            
            if (ammoComponent.Caseless)
                ammoComponent.Owner.Delete();
        }

        private void Fire(IEntity shooter, IEntity bullet, Angle angle, float velocity)
        {
            var collidableComponent = bullet.GetComponent<ICollidableComponent>();
            collidableComponent.Status = BodyStatus.InAir;

            var projectileComponent = bullet.GetComponent<ProjectileComponent>();
            projectileComponent.IgnoreEntity(shooter);

            bullet
                .GetComponent<ICollidableComponent>()
                .EnsureController<BulletController>()
                .LinearVelocity = angle.ToVec() * velocity;

            bullet.Transform.LocalRotation = angle.Theta;
        }
    }
}