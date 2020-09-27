#nullable enable
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedRangedWeaponSystem : EntitySystem
    {
        public abstract void MuzzleFlash(IEntity? user, IEntity weapon, string texture, Angle angle);

        public abstract void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null);

        /// <summary>
        ///     Shoot a hitscan weapon (e.g. laser).
        /// </summary>
        /// <param name="user"></param>
        /// <param name="hitscan"></param>
        /// <param name="angle"></param>
        /// <param name="damageRatio"></param>
        /// <param name="alphaRatio"></param>
        public abstract void ShootHitscan(IEntity? user, HitscanPrototype hitscan, Angle angle, float damageRatio = 1.0f, float alphaRatio = 1.0f);

        /// <summary>
        ///     If you want to pull out the projectile from ammo and shoot it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="angle"></param>
        /// <param name="ammoComponent"></param>
        /// <param name="spreadRatio"></param>
        public abstract void ShootAmmo(IEntity? user, Angle angle, SharedAmmoComponent ammoComponent, float spreadRatio = 1.0f);

        /// <summary>
        ///     Shoot the projectile directly
        /// </summary>
        /// <param name="user"></param>
        /// <param name="angle"></param>
        /// <param name="projectileComponent"></param>
        public abstract void ShootProjectile(IEntity? user, Angle angle, SharedProjectileComponent projectileComponent, float velocity);
    }
}