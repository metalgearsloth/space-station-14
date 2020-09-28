#nullable enable
using System;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedRangedWeaponSystem : EntitySystem
    {
        protected const float EffectDuration = 0.5f;
        
        /// <summary>
        ///     Show the muzzle flash parented to the weapon.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="texture"></param>
        /// <param name="angle"></param>
        /// <param name="predicted">As the impact and travel effects aren't predicted yet we can't predict hitscan stuff very well</param>
        /// <param name="currentTime"></param>
        /// <param name="alphaRatio"></param>
        public abstract void MuzzleFlash(IEntity? user, SharedRangedWeaponComponent weapon, string? texture, Angle angle, bool predicted = true, TimeSpan? currentTime = null, float alphaRatio = 1.0f);

        public abstract void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null);

        /// <summary>
        ///     Shoot a hitscan weapon (e.g. laser).
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="hitscan"></param>
        /// <param name="angle"></param>
        /// <param name="damageRatio"></param>
        /// <param name="alphaRatio"></param>
        public abstract void ShootHitscan(IEntity? user, SharedRangedWeaponComponent weapon, HitscanPrototype hitscan, Angle angle, float damageRatio = 1.0f, float alphaRatio = 1.0f);

        /// <summary>
        ///     If you want to pull out the projectile from ammo and shoot it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="ammoComponent"></param>
        public abstract void ShootAmmo(IEntity? user, SharedRangedWeaponComponent weapon, Angle angle, SharedAmmoComponent ammoComponent);

        /// <summary>
        ///     Shoot the projectile directly
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="projectileComponent"></param>
        /// <param name="velocity"></param>
        public abstract void ShootProjectile(IEntity? user, SharedRangedWeaponComponent weapon, Angle angle, SharedProjectileComponent projectileComponent, float velocity);
    }
}