#nullable enable
using System;
using Content.Shared.GameObjects.Components.Power;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedRangedWeaponSystem : EntitySystem
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
        public abstract void MuzzleFlash(IEntity? user, SharedRangedWeaponComponent weapon, Angle angle, TimeSpan? currentTime = null, bool predicted = true, float alphaRatio = 1.0f);

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

        /// <summary>
        ///     Actually fires the gun, cycles rounds, etc.
        /// </summary>
        private bool TryShoot(SharedRangedWeaponComponent weapon, Angle angle)
        {
            if (weapon.Owner.TryGetComponent(out IBallisticGun? ballistic))
            {
                return TryShootBallistic(ballistic, angle);
            }

            if (weapon.Owner.TryGetComponent(out IBatteryGun? batteryGun))
            {
                return TryShootBattery(batteryGun, angle);
            }

            Logger.Error($"Tried to fire non-implemented gun type from {weapon.Owner.Uid}!");
            return false;
        }

        protected abstract bool TryShootBallistic(IBallisticGun weapon, Angle angle);

        protected abstract bool TryShootBattery(IBatteryGun weapon, Angle angle);

        /// <summary>
        ///     General shooting code.
        /// </summary>
        protected bool TryFire(IEntity user, SharedRangedWeaponComponent weapon, MapCoordinates coordinates, out int firedShots, TimeSpan? currentTime = null)
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

            if (!weapon.CanFire())
                return false;

            if (currentTime < weapon.NextFire)
                return true;

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
        protected Angle GetWeaponSpread(SharedRangedWeaponComponent weapon, TimeSpan lastFire, Angle angle, TimeSpan? currentTime = null)
        {
            currentTime ??= _gameTiming.CurTime;

            // TODO: Could also predict this client-side. Probably need to use System.Random and seeds but out of scope for this big pr.
            // If we're sure no desyncs occur then we could just use the Uid to get the seed probably.
            var newTheta = MathHelper.Clamp(
                weapon._currentAngle.Theta + weapon._angleIncrease - weapon._angleDecay * (currentTime.Value - lastFire).TotalSeconds,
                weapon._minAngle.Theta,
                weapon._maxAngle.Theta);

            weapon._currentAngle = new Angle(newTheta);

            var random = (_robustRandom.NextDouble() - 0.5) * 2;
            return Angle.FromDegrees(angle.Degrees + weapon._currentAngle.Degrees * random);
        }
    }

    /// <summary>
    ///     A gun that pulls its ammo from projectiles.
    /// </summary>
    public interface IBallisticGun : IGun
    {
        /// <summary>
        ///     Not every ballistic weapon has a magazine, and it also may not be in the gun.
        /// </summary>
        IEntity? Magazine { get; }

        bool BoltOpen { get; }

        /// <summary>
        ///     If we have an entity chambered.
        /// </summary>
        IEntity? Chambered { get; }

        /// <summary>
        ///     Whether the weapon should cycle automatically weapon fired.
        /// </summary>
        bool AutoCycle { get; }

        void TrySetBolt(bool value);

        /// <summary>
        ///     Cycle the chamber.
        /// </summary>
        void Cycle();
    }

    /// <summary>
    ///     A gun that pulls its ammo from a battery rather than discrete projectiles.
    /// </summary>
    public interface IBatteryGun : IGun
    {
        IEntity? Battery { get; }

        SharedPowerCell? PowerCell { get; }
    }

    // TODO: Dis
    public abstract class SharedPowerCellComponent
    {

    }

    public interface IGun
    {
        string? SoundGunshot { get; }

        float SoundRange { get; }

        string? SoundEmpty { get; }

        float EmptyVariation { get; }

        float EmptyVolume { get; }

        float GunshotVariation { get; }

        float GunshotVolume { get;}

        IEntity Owner { get; }

        IEntity Shooter { get; }
    }
}
