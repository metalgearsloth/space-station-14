using System;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedGunSystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;

        protected const float EffectDuration = 0.5f;

        /// <summary>
        ///     Show the muzzle flash for the weapon.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="predicted">Whether we also need to show the effect for the client. Eventually this shouldn't be needed (when we can predict hitscan / weapon recoil)</param>
        /// <param name="currentTime"></param>
        /// <param name="alphaRatio"></param>
        public abstract void MuzzleFlash(IEntity? user, SharedGunComponent weapon, Angle angle, TimeSpan? currentTime = null, bool predicted = true, float alphaRatio = 1.0f);

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
        public abstract void ShootHitscan(IEntity? user, SharedGunComponent weapon, HitscanPrototype hitscan, Angle angle, float damageRatio = 1.0f, float alphaRatio = 1.0f);

        /// <summary>
        ///     If you want to pull out the projectile from ammo and shoot it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="ammoComponent"></param>
        public abstract void ShootAmmo(IEntity? user, SharedGunComponent weapon, Angle angle, SharedAmmoComponent ammoComponent);

        /// <summary>
        ///     Shoot the projectile directly
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="projectileComponent"></param>
        /// <param name="velocity"></param>
        public abstract void ShootProjectile(IEntity? user, SharedGunComponent weapon, Angle angle, SharedProjectileComponent projectileComponent, float velocity);

        /// <summary>
        ///     Actually fires the gun, cycles rounds, etc.
        /// </summary>
        private bool TryShoot(SharedGunComponent weapon, Angle angle)
        {
            var magazine = weapon.Magazine;

            if (magazine == null)
            {
                if (weapon.SoundEmpty != null)
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundEmpty, weapon.Owner);

                return false;
            }

            if (!magazine.CanShoot())
            {
                return false;
            }

            // TODO: What some games do is bundle multiple shots into a single one with increased damage at low tickrates
            // which we could potentially look at doing, but this is more for stuff with stupidly high firerates.
            if (!magazine.TryGetProjectile(out var projectile))
            {
                return false;
            }

            switch (projectile)
            {
                default:
                    throw new NotImplementedException($"Projectile type {projectile.GetType()} not implemented!");
            }

            return true;
        }

        /// <summary>
        ///     Get the Sound filter.
        /// </summary>
        /// <param name="gun"></param>
        /// <returns></returns>
        protected abstract Filter GetFilter(SharedGunComponent gun);

        /// <summary>
        ///     General shooting code.
        /// </summary>
        protected bool TryFire(IEntity user, SharedGunComponent weapon, MapCoordinates coordinates, out int firedShots, TimeSpan? currentTime = null)
        {
            currentTime ??= _gameTiming.CurTime;
            firedShots = 0;
            var lastFire = weapon.NextFire;

            // If it's first shot we'll just set it to now.
            if (weapon.ShotCounter == 0 && weapon.NextFire <= currentTime)
            {
                weapon.LastFire = weapon.NextFire;
                weapon.NextFire = currentTime.Value;
            }

            if (currentTime < weapon.NextFire)
                return false;

            var fireAngle = (coordinates.Position - user.Transform.WorldPosition).ToAngle();

            // To handle guns with firerates higher than framerate / tickrate
            while (weapon.NextFire <= currentTime)
            {
                weapon.LastFire = weapon.NextFire;
                weapon.NextFire += TimeSpan.FromSeconds(1 / weapon.FireRate);
                var spread = GetWeaponSpread(weapon, lastFire, fireAngle);
                lastFire = weapon.NextFire;

                // Mainly check if we can get more bullets (e.g. if there's only 1 left in the clip).
                if (!TryShoot(weapon, spread))
                    break;

                firedShots++;
                weapon.ShotCounter++;
            }

            // Somewhat suss on this, needs more playtesting
            if (firedShots == 0)
                return false;

            weapon.UpdateAppearance();
            return true;
        }

        /// <summary>
        ///     Get the adjusted weapon angle with recoil
        /// </summary>
        /// <remarks>
        ///     The only reason this is virtual is because client-side randomness isnt deterministic so we can't show an accurate muzzle flash.
        ///     As such (for now) client-side guns will override.
        /// </remarks>
        /// <param name="currentTime"></param>
        /// <param name="lastFire"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        protected Angle GetWeaponSpread(SharedGunComponent weapon, TimeSpan lastFire, Angle angle, TimeSpan? currentTime = null)
        {
            currentTime ??= _gameTiming.CurTime;

            // TODO: Could also predict this client-side. Probably need to use System.Random and seeds but out of scope for this big pr.
            // If we're sure no desyncs occur then we could just use the Uid to get the seed probably.
            var newTheta = MathHelper.Clamp(
                weapon.CurrentAngle.Theta + weapon.AngleIncrease - weapon.AngleDecay * (currentTime.Value - lastFire).TotalSeconds,
                weapon.MinAngle.Theta,
                weapon.MaxAngle.Theta);

            weapon.CurrentAngle = new Angle(newTheta);

            var random = (_robustRandom.NextDouble() - 0.5) * 2;
            return Angle.FromDegrees(angle.Degrees + weapon.CurrentAngle.Degrees * random);
        }
    }
}
