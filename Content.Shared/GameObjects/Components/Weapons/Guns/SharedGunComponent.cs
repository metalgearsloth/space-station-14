using System;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
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

        /// <summary>
        /// If we have a chamber then we pull from that to shoot.
        /// If not then we pull directly from the magazine.
        /// </summary>
        [ViewVariables]
        [DataField("hasChamber")]
        public bool HasChamber { get; }

        public ContainerSlot? Chamber { get; private set; }

        // If our bolt is open then we can directly insert ammo into it.
        // This is useful for stuff that is single-shot and has no need for any kind of magazine.

        [ViewVariables]
        [DataField("boltClosed")]
        public bool BoltClosed { get; } = true;

        [ViewVariables]
        [DataField("boltToggleable")]
        public bool BoltToggleable { get; } = false;

        /// <summary>
        /// Can the magazine be removed?
        /// </summary>
        [ViewVariables]
        [DataField("internalMag")]
        public bool InternalMagazine { get; }

        /// <summary>
        /// All guns have a magazine, some may have it internal.
        /// </summary>
        public IAmmoProvider? Magazine => _magazineSlot?.ContainedEntity?.GetComponent<IAmmoProvider>();

        private ContainerSlot? _magazineSlot = null;

        [ViewVariables]
        [DataField("magazinePrototype")]
        private EntityPrototype? _magazinePrototype;

        // TODO: MagazineType

        // I think all guns have magazines we just need to determine if internal or not
        // Magazine needs a bool for whether it autoejects on empty

        // Uhh bool for whether we can manually cycle
        // Bool for whether it autocycles

        [ViewVariables]
        [DataField("speedLoadable")]
        public bool SpeedLoadable { get; } = false;

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

            if (!BoltClosed && !BoltToggleable)
            {
                throw new InvalidOperationException();
            }

            if (HasChamber)
            {
                Chamber = Owner.EnsureContainer<ContainerSlot>("chamber");
            }
            else
            {
                if (Owner.TryGetComponent(out ContainerManagerComponent? manager) && manager.HasContainer("chamber"))
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out SharedAppearanceComponent? appearance)) return;
            Magazine?.UpdateAppearance(appearance);

            // TODO: All the other appearance updates for bolts and shiznit.
        }
    }

    public abstract class SharedRevolverMagazineComponent : SharedAmmoProviderComponent
    {

    }

    public abstract class SharedBatteryMagazineComponent : SharedAmmoProviderComponent
    {

    }

    public abstract class SharedBallisticMagazineComponent : SharedAmmoProviderComponent
    {

    }

    public abstract class SharedReagentMagazineComponent : SharedAmmoProviderComponent
    {

    }

    public abstract class SharedAmmoProviderComponent : Component, IAmmoProvider
    {
        // TODO: Most of the below seems more suited to a magazine weapon
        // Try working on the powercell one for a bit and see what flows.

        // TODO: Caliber

        // TODO: MagazineType

        private Container? _ammoContainer = null;

        /// <inheritdoc />
        [ViewVariables]
        [DataField("autoEjectOnEmpty")]
        public bool AutoEjectOnEmpty { get; }

        /// <inheritdoc />
        public ushort ProjectileCount => UnspawnedCount + (ushort) _ammoContainer?.ContainedEntities?.Count ?? 0;

        /// <inheritdoc />
        [ViewVariables]
        [DataField("capacity")]
        public ushort ProjectileCapacity { get; }

        /// <inheritdoc />
        public ushort UnspawnedCount { get; private set; }

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

            _ammoContainer = Owner.EnsureContainer<Container>("ammo", out var existing);

            if (existing)
            {
                UnspawnedCount -= (ushort) _ammoContainer.ContainedEntities.Count;
            }

            if (Owner.TryGetContainer(out var container) && container.Owner.TryGetComponent(out SharedGunComponent? gun))
            {
                // If we're attached to a gun then pre-fill its chamber
                if (gun.HasChamber && TryGetAmmo(out var ammo))
                {
                    gun.Chamber!.Insert(ammo);
                    gun.UpdateAppearance();
                    gun.Dirty();
                }
            }

            DebugTools.Assert(ProjectileCount <= ProjectileCapacity);
        }

        public bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            ammo = null;
            return false;
        }

        public virtual void UpdateAppearance(SharedAppearanceComponent? appearance = null)
        {
            return;
        }
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
        bool AutoEjectOnEmpty { get; }

        /// <summary>
        /// How many projectiles are we currently holding
        /// </summary>
        ushort ProjectileCount { get; }

        /// <summary>
        /// How many projectiles can we hold?
        /// </summary>
        ushort ProjectileCapacity { get; }

        /// <summary>
        /// We defer spawning projectiles as late as possible hence we need a tracker for it
        /// </summary>
        ushort UnspawnedCount { get; }

        /// <summary>
        /// What do we spawn with out UnspawnedCount?
        /// </summary>
        string? FillPrototype { get; }

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
