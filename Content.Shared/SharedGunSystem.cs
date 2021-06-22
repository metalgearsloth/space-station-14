using System;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedGunSystem : EntitySystem
    {
        [Dependency] protected readonly IGameTiming GameTiming = default!;
        [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
        [Dependency] protected readonly IRobustRandom RobustRandom = default!;

        protected bool Enabled { get; set; } = true;

        protected const float EffectDuration = 0.5f;

        protected abstract void Cycle(SharedChamberedGunComponent component, IEntity? user = null, bool manual = false);

        protected abstract void EjectMagazine(SharedGunComponent component);

        protected abstract void ToggleBolt(SharedChamberedGunComponent component);

        protected abstract void PlayGunSound(IEntity? user, IEntity entity, string? sound, float variation = 0.0f, float volume = 0.0f);

        /// <summary>
        ///     Show the muzzle flash for the weapon.
        /// </summary>
        public abstract void MuzzleFlash(IEntity? user, SharedGunComponent weapon, Angle angle, TimeSpan currentTime, bool predicted = false);

        public abstract void EjectCasing(IEntity? user, IEntity casing, bool playSound = true);

        /// <summary>
        ///     Shoot a hitscan weapon (e.g. laser).
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="hitscan"></param>
        /// <param name="angle"></param>
        /// <param name="damageRatio"></param>
        /// <param name="alphaRatio"></param>
        public abstract void ShootHitscan(IEntity? user, IGun weapon, HitscanPrototype hitscan, Angle angle, float damageRatio = 1.0f, float alphaRatio = 1.0f);

        /// <summary>
        ///     If you want to pull out the projectile from ammo and shoot it.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="ammoComponent"></param>
        public abstract void ShootAmmo(IEntity? user, IGun weapon, Angle angle, SharedAmmoComponent ammoComponent);

        /// <summary>
        ///     Shoot the projectile directly
        /// </summary>
        /// <param name="user"></param>
        /// <param name="weapon"></param>
        /// <param name="angle"></param>
        /// <param name="projectileComponent"></param>
        /// <param name="velocity"></param>
        public abstract void ShootProjectile(IEntity? user, IGun weapon, Angle angle, SharedProjectileComponent projectileComponent, float velocity);

        /// <summary>
        ///     Actually fires the gun, cycles rounds, etc.
        /// </summary>
        private bool TryShoot(IEntity user, SharedGunComponent weapon, Angle angle)
        {
            IProjectile? projectile;

            switch (weapon)
            {
                case SharedChamberedGunComponent chamberedGun:
                    if (!chamberedGun.CanFire())
                    {
                        return false;
                    }

                    // TODO: Client needs all of the fuckery as we don't have a lot of stuff predicted
                    // (e.g. using a stack and stuff).

                    // TODO: Remove the ShootAmmo and have this get the projectile
                    // TODO: This is duped with Cycle
                    if (chamberedGun.TryPopChamber(out var ammo))
                    {
                        EjectCasing(null, ammo.Owner);
                    }

                    Cycle(chamberedGun, user);

                    projectile = ammo;
                    break;
                default:
                    var magazine = weapon.Magazine;

                    if (magazine == null)
                    {
                        return false;
                    }

                    if (!magazine.CanShoot())
                    {
                        return false;
                    }

                    if (!magazine.TryGetProjectile(out projectile))
                    {
                        return false;
                    }

                    magazine.Dirty();
                    break;
            }

            // TODO: Need to figure out this inheritance bullshit.
            switch (projectile)
            {
                case SharedAmmoComponent ammo:
                    ShootAmmo(user, weapon, angle, ammo);
                    break;
                case SharedProjectileComponent proj:
                    // TODO: Support firing ammo or whatever it spawns instead.
                    ShootProjectile(user, weapon, angle, proj, 20f);
                    break;
                case HitscanPrototype hitscan:
                    ShootHitscan(user, weapon, hitscan, angle);
                    break;
                // Client-side support
                case null:
                    break;
                default:
                    throw new NotImplementedException($"Projectile type {projectile?.GetType()} not implemented!");
            }

            PlayGunSound(user, weapon.Owner, weapon.SoundGunshot, 0.01f);

            return true;
        }

        /// <summary>
        ///     Get the Sound filter.
        /// </summary>
        /// <param name="gun"></param>
        /// <returns></returns>
        protected abstract Filter GetFilter(IEntity user, SharedGunComponent gun);

        protected abstract Filter GetFilter(SharedAmmoProviderComponent ammoProvider);

        /// <summary>
        ///     General shooting code.
        /// </summary>
        protected bool TryFire(IEntity user, SharedGunComponent weapon, MapCoordinates coordinates, out int firedShots, TimeSpan? currentTime = null)
        {
            // TODO: Change MapCoordinates to Angle to optimise client
            currentTime ??= GameTiming.CurTime;
            firedShots = 0;
            var lastFire = weapon.NextFire;

            if (currentTime < weapon.NextFire)
            {
                return true;
            }

            if (!weapon.CanFire())
            {
                if (weapon.FireRate > 0f)
                    weapon.NextFire += TimeSpan.FromSeconds(1 / weapon.FireRate);

                // TODO: Empty variation and volume
                PlayGunSound(user, weapon.Owner, weapon.SoundEmpty);

                return false;
            }

            var fireAngle = (coordinates.Position - user.Transform.WorldPosition).ToAngle();
            var muzzleAngle = Angle.Zero;

            // To handle guns with firerates higher than framerate / tickrate
            while (weapon.NextFire <= currentTime)
            {
                weapon.NextFire += TimeSpan.FromSeconds(1 / weapon.FireRate);
                var spread = GetWeaponSpread(weapon, lastFire, fireAngle);
                muzzleAngle += spread;
                lastFire = weapon.NextFire;

                // Mainly check if we can get more bullets (e.g. if there's only 1 left in the clip).
                if (!TryShoot(user, weapon, spread))
                    break;

                firedShots++;
                weapon.ShotCounter++;
            }

            // Somewhat suss on this, needs more playtesting
            if (firedShots == 0)
                return false;

            // TODO: Eventually muzzle should be handled by the projectile itself (so we can mix hitscan and projectile freely)
            // but because no entity creation prediction we'll just store it on the gun for now.
            if (weapon.CanMuzzleFlash)
            {
                switch (weapon)
                {
                    case SharedChamberedGunComponent _:
                        MuzzleFlash(user, weapon, fireAngle, currentTime.Value, true);
                        break;
                    default:
                        MuzzleFlash(user, weapon, fireAngle, currentTime.Value);
                        break;
                }
            }

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
            currentTime ??= GameTiming.CurTime;

            // TODO: Could also predict this client-side. Probably need to use System.Random and seeds but out of scope for this big pr.
            // If we're sure no desyncs occur then we could just use the Uid to get the seed probably.
            var newTheta = MathHelper.Clamp(
                weapon.CurrentAngle.Theta + weapon.AngleIncrease - weapon.AngleDecay * (currentTime.Value - lastFire).TotalSeconds,
                weapon.MinAngle.Theta,
                weapon.MaxAngle.Theta);

            weapon.CurrentAngle = new Angle(newTheta);

            var random = (RobustRandom.NextDouble() - 0.5) * 2;
            return Angle.FromDegrees(angle.Degrees + weapon.CurrentAngle.Degrees * random);
        }

        [Serializable, NetSerializable]
        protected sealed class ShootMessage : EntityEventArgs
        {
            public EntityUid Gun { get; }

            public MapCoordinates Coordinates { get; }

            public int Shots { get; set; }

            public TimeSpan Time { get; set; }

            public ShootMessage(EntityUid gun, MapCoordinates coordinates, int shots, TimeSpan time)
            {
                Gun = gun;
                Coordinates = coordinates;
                Shots = shots;
                Time = time;
            }
        }
    }

    // TODO: Move to Yaml
    public enum GunCaliber : byte
    {
        Unspecified = 0,
        A357, // Placeholder?
        ClRifle,
        SRifle,
        Pistol,
        A35, // Placeholder?
        LRifle,
        Magnum,
        AntiMaterial,
        Shotgun,
        Cap,
        Rocket,
        Dart, // Placeholder
        Grenade,
        Energy,
        CreamPie,
    }

    [Flags]
    public enum GunMagazine : uint
    {

        Unspecified = 0,
        LPistol = 1 << 0, // Placeholder?
        Pistol = 1 << 1,
        HCPistol = 1 << 2,
        Smg = 1 << 3,
        SmgTopMounted = 1 << 4,
        Rifle = 1 << 5,
        IH = 1 << 6, // Placeholder?
        Box = 1 << 7,
        Pan = 1 << 8,
        Dart = 1 << 9, // Placeholder
        CalicoTopMounted = 1 << 10,
    }
}
