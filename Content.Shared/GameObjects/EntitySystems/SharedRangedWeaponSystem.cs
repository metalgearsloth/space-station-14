#nullable enable
using System;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Power;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
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

        /// <summary>
        ///     Get the Sound filter.
        /// </summary>
        /// <param name="gun"></param>
        /// <returns></returns>
        protected abstract Filter GetFilter(IGun gun);

        /// <summary>
        ///     Try to shoot a ballistic weapon.
        ///     Can fire a projectile or hitscan.
        /// </summary>
        protected virtual bool TryShootBallistic(IBallisticGun weapon, Angle angle)
        {
            if (weapon.Chambered == null) return false;
            if (weapon.AutoCycle)
                Cycle(weapon);

            return true;
        }

        #region Ballistic
        protected void Cycle(IBallisticGun weapon, bool manual = false)
        {
            TryEjectChamber(weapon);
            TryFeedChamber(weapon);

            if (manual)
            {
                if (weapon.SoundRack != null)
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundRack, AudioHelpers.WithVariation(IBallisticGun.RackVariation).WithVolume(IBallisticGun.RackVolume));
            }
        }

        protected void TryEjectChamber(IBallisticGun weapon)
        {
            var chamberEntity = weapon.Chambered;
            if (chamberEntity != null)
            {
                if (weapon.TryRemoveChambered())
                    return;

                if (!chamberEntity.Caseless)
                    Get<SharedRangedWeaponSystem>().EjectCasing(weapon.Shooter(), chamberEntity.Owner);

                return;
            }
        }

        protected void TryFeedChamber(IBallisticGun weapon)
        {
            if (weapon.Chambered != null) return;

            // Try and pull a round from the magazine to replace the chamber if possible
            var magazine = weapon.Magazine;
            SharedAmmoComponent? nextCartridge = null;
            magazine?.TryPop(out nextCartridge);

            if (nextCartridge == null)
                return;

            weapon.TryInsertChamber(nextCartridge);

            if (weapon.AutoEjectMag && magazine != null && magazine.ShotsLeft == 0)
            {
                if (weapon.SoundAutoEject != null)
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundAutoEject, AudioHelpers.WithVariation(IBallisticGun.AutoEjectVariation).WithVolume(IBallisticGun.AutoEjectVolume));

                weapon.TryRemoveMagazine();
            }
        }
        #endregion
        #region Battery

        #endregion

        /// <summary>
        ///     Try to shoot a battery weapon.
        ///     Can fire a projectile or hitscan.
        /// </summary>
        protected virtual bool TryShootBattery(IBatteryGun weapon, Angle angle)
        {
            return true;
        }

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
                weapon.MaxAngle.Theta);

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
        // Sounds
        /// <summary>
        ///     Played when the mag is auto ejected.
        /// </summary>
        string? SoundAutoEject { get; }

        /// <summary>
        ///     Played if the ballistic gun is manually cycled.
        /// </summary>
        string? SoundRack { get; }

        // TODO
        const float AutoEjectVariation = 0.0f;
        const float AutoEjectVolume = 0.0f;
        const float RackVariation = 0.0f;
        const float RackVolume = 0.0f;

        /// <summary>
        ///     Whether the magazine is detachable or its internal (e.g. smg vs bolt-action rifle)
        /// </summary>
        bool InbuiltMagazine { get; }

        /// <summary>
        ///     Does the magazine automatically eject on the gun being empty.
        /// </summary>
        bool AutoEjectMag { get; }

        /// <summary>
        ///     Not every ballistic weapon has a magazine, and it also may not be in the gun.
        /// </summary>
        SharedRangedMagazineComponent? Magazine { get; }

        bool BoltOpen { get; }

        /// <summary>
        ///     If we have an entity chambered.
        /// </summary>
        SharedAmmoComponent? Chambered { get; }

        /// <summary>
        ///     Tries to remove the entity from the chamber slot.
        /// </summary>
        /// <returns></returns>
        bool TryRemoveChambered();

        bool TryInsertChamber(SharedAmmoComponent ammo);

        bool TryRemoveMagazine();

        bool TryInsertMagazine(SharedRangedMagazineComponent magazine);

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
        bool PowerCellRemovable { get; }

        SharedBatteryComponent? Battery { get; }

        float LowerChargeLimit { get; }

        /// <summary>
        ///     How much energy it costs to fire a full shot.
        ///     We can also fire partial shots if LowerChargeLimit is met.
        /// </summary>
        float BaseFireCost { get; }
    }

    /// <summary>
    ///     Universal gun traits that htey all must have.
    /// </summary>
    public interface IGun
    {
        // Sounds
        string? SoundGunshot { get; }

        float SoundRange { get; }

        string? SoundEmpty { get; }

        const float GunshotVariation = 0.1f;
        const float EmptyVariation = 0.1f;
        const float CycleVariation = 0.1f;
        const float BoltToggleVariation = 0.1f;
        const float InsertVariation = 0.1f;

        const float GunshotVolume = 0.0f;
        const float EmptyVolume = 0.0f;
        const float CycleVolume = 0.0f;
        const float BoltToggleVolume = 0.0f;
        const float InsertVolume = 0.0f;

        FireRateSelector Selector { get; set; }

        IEntity Owner { get; }

        IEntity Shooter();
    }
}
