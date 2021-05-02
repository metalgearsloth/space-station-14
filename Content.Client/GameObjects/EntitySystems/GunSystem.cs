using System;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class GunSystem : SharedGunSystem
    {
        public override void MuzzleFlash(IEntity? user, IEntity weapon, SharedAmmoComponent ammo, Angle angle, TimeSpan? currentTime = null,
            bool predicted = false, float alphaRatio = 1)
        {
            throw new NotImplementedException();
        }

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null)
        {
            throw new NotImplementedException();
        }

        public override void ShootHitscan(IEntity? user, SharedGunComponent weapon, HitscanPrototype hitscan, Angle angle,
            float damageRatio = 1, float alphaRatio = 1)
        {
            throw new NotImplementedException();
        }

        public override void ShootAmmo(IEntity? user, SharedGunComponent weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            throw new NotImplementedException();
        }

        public override void ShootProjectile(IEntity? user, SharedGunComponent weapon, Angle angle,
            SharedProjectileComponent projectileComponent, float velocity)
        {
            throw new NotImplementedException();
        }

        protected override Filter GetFilter(SharedGunComponent gun)
        {
            throw new NotImplementedException();
        }

        protected override Filter GetFilter(SharedAmmoProviderComponent ammoProvider)
        {
            throw new NotImplementedException();
        }
    }
}
