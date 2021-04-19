using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Guns
{
    public abstract class SharedGunComponent : Component
    {
        public override string Name => "Gun";
        // Bool for whether we can chamber load if bolt is open

        // Sounds (TODO: Copy existing)
        [ViewVariables]
        [DataField("soundGunshot")]
        public string? SoundGunshot { get; } = null;

        [ViewVariables]
        [DataField("soundEmpty")]
        public string? SoundEmpty { get; } = null;

        // If our bolt is open then we can directly insert ammo into it.
        // This is useful for stuff that is single-shot and has no need for any kind of magazine.

        /// <summary>
        /// Can the magazine be removed?
        /// </summary>
        [ViewVariables]
        [DataField("internalMag")]
        public bool InternalMagazine { get; }

        /// <inheritdoc />
        [ViewVariables]
        [DataField("autoEjectOnEmpty")]
        public bool AutoEjectOnEmpty { get; }

        /// <summary>
        /// All guns have a magazine, some may have it internal.
        /// </summary>
        public IAmmoProvider? Magazine => _magazineSlot?.ContainedEntity?.GetComponent<IAmmoProvider>();

        private ContainerSlot? _magazineSlot = null;

        [ViewVariables]
        [DataField("magazinePrototype")]
        private EntityPrototype? _magazinePrototype;

        // TODO: MagazineType

        /// <summary>
        /// How many times we can shoot per second
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("fireRate")]
        public float FireRate { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("ammoSpreadRatio")]
        public float AmmoSpreadRatio { get; set; }

        public Angle CurrentAngle { get; set; }

        /// <summary>
        /// How much the CurrentAngle increases per shot fired.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("angleIncrease")]
        public float AngleIncrease { get; set; }

        /// <summary>
        /// How fast per second the CurrentAngle decays.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("angleDecay")]
        public float AngleDecay { get; set; }

        /// <summary>
        /// The minimum variance allowed to the shooting angle.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minAngle")]
        public Angle MinAngle { get; set; }

        /// <summary>
        /// The maximum variance allowed to the shooting angle.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxAngle")]
        public Angle MaxAngle { get; set; }

        /// <summary>
        /// Last time we pulled a projectile.
        /// </summary>
        public TimeSpan LastFire { get; set; }

        /// <summary>
        /// Earliest time we can pull another projectile
        /// </summary>
        public TimeSpan NextFire { get; set; }

        /// <summary>
        /// How many times we've fired in the current burst. Useful for tracking single-shot / burst-fire.
        /// </summary>
        [ViewVariables]
        public int ShotCounter { get; set; }

        public override void Initialize()
        {
            base.Initialize();

            // Pre-spawn magazine in
            _magazineSlot = Owner.EnsureContainer<ContainerSlot>("magazine", out var existingMag);

            if (!existingMag && _magazinePrototype != null)
            {
                var mag = Owner.EntityManager.SpawnEntity(_magazinePrototype.ID, Owner.Transform.Coordinates);
                _magazineSlot.Insert(mag);
                UpdateAppearance();
                Dirty();
            }

            if (InternalMagazine && _magazineSlot.ContainedEntity == null)
            {
                throw new InvalidOperationException();
            }
        }

        public void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out SharedAppearanceComponent? appearance)) return;
            Magazine?.UpdateAppearance(appearance);

            // TODO: All the other appearance updates for bolts and shiznit.
        }
    }

    // I think all guns have magazines we just need to determine if internal or not
    // Magazine needs a bool for whether it autoejects on empty

    // Uhh bool for whether we can manually cycle
    // Bool for whether it autocycles

    public abstract class SharedBatteryMagazineComponent : SharedAmmoProviderComponent
    {

    }

    public abstract class SharedBallisticMagazineComponent : SharedBallisticsAmmoProvider
    {
        /// <summary>
        /// If we have a chamber then we pull from that to shoot.
        /// If not then we pull directly from the magazine.
        /// </summary>
        [ViewVariables]
        [DataField("hasChamber")]
        public bool HasChamber { get; }

        public ContainerSlot? Chamber { get; private set; }

        private Container? _ammoContainer = null;

        /// <inheritdoc />
        public int ProjectileCount => UnspawnedCount + _ammoContainer?.ContainedEntities.Count ?? 0;

        public override void Initialize()
        {
            base.Initialize();
            _ammoContainer = Owner.EnsureContainer<Container>("ammo", out var existing);

            if (existing)
            {
                UnspawnedCount -= _ammoContainer.ContainedEntities.Count;
            }

            if (!BoltClosed && !BoltToggleable)
            {
                throw new InvalidOperationException("Can't have a forced-open bolt with a non-toggleable one on entity {Owner}");
            }

            if (HasChamber)
            {
                Chamber = Owner.EnsureContainer<ContainerSlot>("chamber");
            }
            else
            {
                if (Owner.TryGetComponent(out ContainerManagerComponent? manager) && manager.HasContainer("chamber"))
                {
                    throw new InvalidOperationException($"Found existing chamber on {Owner} but this is not allowed");
                }
            }

            if (Owner.TryGetContainer(out var container) && container.Owner.TryGetComponent(out SharedGunComponent? gun))
            {
                // If we're attached to a gun then pre-fill its chamber
                if (HasChamber && TryGetAmmo(out var ammo))
                {
                    Chamber!.Insert(ammo.Owner);
                    gun.UpdateAppearance();
                    gun.Dirty();
                }
            }

            DebugTools.Assert(ProjectileCount <= ProjectileCapacity);
        }
    }

    public abstract class SharedRevolverMagazineComponent : SharedBallisticsAmmoProvider, ISerializationHooks
    {
        private SharedAmmoComponent?[] _revolver = default!;

        // Don't initialize to their capacity given more guns will never need their max capacity.
        private Stack<SharedAmmoComponent> _spawnedAmmo = new();

        [ViewVariables]
        [DataField("speedLoadable")]
        public bool SpeedLoadable { get; } = false;

        private int _currentCylinder;

        public override void Initialize()
        {
            base.Initialize();
            _revolver = new SharedAmmoComponent?[ProjectileCapacity];
        }

        private void Cycle()
        {
            // TODO: Copy and shit.
        }
    }

    public abstract class SharedReagentMagazineComponent : SharedAmmoProviderComponent
    {

    }

    /*
     * Okay so: SharedAmmoProvider -> SharedBatteryProvider
     * SharedAmmoProvider -> SharedBallisticsProvider -> SharedMagazineProvider
     */

    public abstract class SharedBallisticsAmmoProvider : SharedAmmoProviderComponent
    {
        [ViewVariables]
        [DataField("boltClosed")]
        public bool BoltClosed { get; } = true;

        [ViewVariables]
        [DataField("boltToggleable")]
        public bool BoltToggleable { get; } = false;

        /// <inheritdoc />
        [ViewVariables]
        [DataField("capacity")]
        public int ProjectileCapacity { get; }

        /// <inheritdoc />
        public int UnspawnedCount { get; protected set; }

        /// <inheritdoc />
        [ViewVariables]
        [DataField("prototype")]
        public string? FillPrototype { get; }

        public override void Initialize()
        {
            base.Initialize();
            if (FillPrototype != null)
            {
                UnspawnedCount = ProjectileCapacity;
            }
        }

        public virtual bool TrySetBolt(IMagazineGun weapon, bool value)
        {
            if (weapon.BoltOpen == value)
                return false;

            if (value)
            {
                TryEjectChamber(weapon);
            }
            else
            {
                TryFeedChamber(weapon);
            }

            weapon.BoltOpen = value;
            return true;
        }

        #region Magazine
        /// <summary>
        ///     Cycle the chamber.
        /// </summary>
        public void Cycle(IMagazineGun weapon, bool manual = false)
        {
            TryEjectChamber(weapon);
            TryFeedChamber(weapon);

            if (manual)
            {
                if (weapon.SoundRack != null)
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundRack, AudioHelpers.WithVariation(IMagazineGun.RackVariation).WithVolume(IMagazineGun.RackVolume));
            }
        }

        protected void TryEjectChamber(IMagazineGun weapon)
        {
            var chamberEntity = weapon.Chambered;
            if (chamberEntity != null)
            {
                if (weapon.TryRemoveChambered())
                    return;

                if (!chamberEntity.Caseless)
                    EjectCasing(weapon.Shooter(), chamberEntity.Owner);

                return;
            }
        }

        protected void TryFeedChamber(IMagazineGun weapon)
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
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundAutoEject, AudioHelpers.WithVariation(IMagazineGun.AutoEjectVariation).WithVolume(IMagazineGun.AutoEjectVolume));

                weapon.TryRemoveMagazine();
            }
        }
        #endregion

        public abstract bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo);
    }

    public abstract class SharedAmmoComponent : Component
    {

    }

    public abstract class SharedAmmoProviderComponent : Component, IAmmoProvider
    {
        // TODO: Most of the below seems more suited to a magazine weapon
        // Try working on the powercell one for a bit and see what flows.

        // TODO: Caliber

        // TODO: MagazineType

        public virtual bool CanShoot()
        {
            return true;
        }

        public abstract bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile);

        public virtual void UpdateAppearance(SharedAppearanceComponent? appearance = null) {}
    }

    // Speedloader should be a flag or whatever
    // Then, check if chamber is full (insert if not)
    // Then, insert into magazine where possible

    // Gun attachments should essentially do the same thing but we toggle whether we're firing the attachment or something
    // As for alternate fire mods uhh

    // Loading methods:
    // Single cartridge
    // SpeedLoader
    // Magazine
    // PowerCell

    public interface IAmmoProvider
    {
        /// <summary>
        /// Check upfront whether we can fire e.g. is bolt-closed where relevant.
        /// </summary>
        /// <returns></returns>
        bool CanShoot();

        /// <summary>
        /// Pulls the actual IProjectile for firing if available.
        /// </summary>
        /// <param name="projectile"></param>
        /// <returns></returns>
        bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile);

        /// <summary>
        /// Update our appearance visualizers.
        /// </summary>
        void UpdateAppearance(SharedAppearanceComponent? appearance = null);
    }

    /// <summary>
    /// Because we have multiple very different types of "projectiles" that can be fired we need an interface for them.
    /// This is because we could fire an entity or just use a hitscan prototype etc.
    /// </summary>
    public interface IProjectile
    {
        void Fire(MapCoordinates coordinates, Angle angle);
    }

    /// <summary>
    /// Fires discrete entities
    /// </summary>
    public abstract class SharedActualAmmoComponent : IProjectile
    {
        public void Fire(MapCoordinates coordinates, Angle angle)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Fires a raycast.
    /// </summary>
    [Prototype("hitscan")]
    public sealed class HitscanAmmoPrototype : IPrototype, IProjectile
    {
        public void Fire(MapCoordinates coordinates, Angle angle)
        {
            throw new NotImplementedException();
        }

        public string ID
        {
            get { throw new NotImplementedException(); }
        }
    }

    public enum GunProjectileType : byte
    {
        // Stuff like a flamer would probably need its own mode
        Projectile = 0,
        Hitscan = 1,
    }

    public interface IGunAttachment
    {
        // Suppressor overrides gun sound on attach and restores it on remove
        // Flashlight adds an action or whatever
        void OnAttach();
        void OnRemove();
    }

    public abstract class SharedGunAttachmentsComponent : Component
    {

    }

    public abstract class SharedGunAmmoCounterComponent : Component
    {

    }

    [Flags]
    public enum GunFireSelector : byte
    {
        Safety = 0,
        Single = 1 << 0,
        Burst = 1 << 2,
        Automatic = 1 << 3,
    }

    [Flags]
    public enum GunAttachmentSlots : byte
    {
        None = 0,
        Muzzle = 1 << 0,
        Rail = 1 << 1,
        Underslung = 1 << 2,
        Stock = 1 << 3,
    }
}
