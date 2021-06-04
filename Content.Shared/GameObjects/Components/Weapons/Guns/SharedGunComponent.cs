using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Guns
{
    public interface IGun
    {
        IEntity Owner { get; }
        SharedAmmoProviderComponent? Magazine { get; }
    }

    public abstract class SharedChamberedGunComponent : SharedGunComponent, IGun
    {
        // Gun + a chamber + bolt
        // Look it's kinda weird but I couldn't see a cleaner way to do it so be my guest if you want to change it.

        public override string Name => "ChamberedGun";

        // Sounds
        [ViewVariables]
        [DataField("soundBoltOpen")]
        public string? SoundBoltOpen { get; }

        [ViewVariables]
        [DataField("soundBoltClosed")]
        public string? SoundBoltClosed { get; }

        [ViewVariables]
        [DataField("soundCycle")]
        public string? SoundCycle { get; } = "/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg";

        // You could also potentially make these 2 below optional if you want a bolt but no chamber or vice versa
        public ContainerSlot Chamber { get; private set; } = default!;

        [ViewVariables]
        [DataField("boltClosed")]
        public bool BoltClosed { get; set; } = true;

        [ViewVariables]
        [DataField("caliber")]
        public GunCaliber Caliber { get; } = GunCaliber.Unspecified;

        public override void Initialize()
        {
            base.Initialize();
            // DebugTools.Assert(); // TODO: Assert the caliber of us matches any magazines we gots.
            Chamber = Owner.EnsureContainer<ContainerSlot>(nameof(SharedChamberedGunComponent) + "-chamber");

            var ammoProvider = MagazineSlot?.ContainedEntity;

            if (ammoProvider != null && Chamber.ContainedEntity == null)
            {
                // Chambers only work with ballistic mags I guess.
                DebugTools.Assert(ammoProvider.HasComponent<SharedBallisticsAmmoProvider>());

                if (ammoProvider.GetComponent<SharedBallisticsAmmoProvider>().TryGetAmmo(out var ammo))
                {
                    if (!TryInsertChamber(ammo))
                    {
                        throw new InvalidOperationException("Unable to insert magazine ammo into chamber for {Owner}");
                    }
                }
            }
        }

        public override void UpdateAppearance()
        {
            base.UpdateAppearance();
            if (!Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent)) return;
            appearanceComponent.SetData(GunVisuals.BoltClosed, BoltClosed);
        }

        public override bool CanFire()
        {
            if (!base.CanFire()) return false;
            if (!BoltClosed) return false;
            return true;
        }

        public bool TryInsertChamber(SharedAmmoComponent ammo)
        {
            if (ammo.Caliber != Caliber ||
                !Chamber.Insert(ammo.Owner)) return false;

            return true;
        }

        /// <summary>
        /// Tries to pop the currently chambered entity.
        /// </summary>
        /// <param name="ammo"></param>
        /// <returns></returns>
        public abstract bool TryPopChamber([NotNullWhen(true)] out SharedAmmoComponent? ammo);

        public abstract void TryFeedChamber();
    }

    public abstract class SharedGunComponent : Component, IGun
    {
        public override string Name => "Gun";
        // Bool for whether we can chamber load if bolt is open

        // Sounds (TODO: Copy existing)
        [ViewVariables]
        [DataField("soundGunshot")]
        public string? SoundGunshot { get; } = null;

        [ViewVariables]
        [DataField("soundEmpty")]
        public string? SoundEmpty { get; } = "/Audio/Weapons/Guns/Empty/empty.ogg";

        [ViewVariables]
        [DataField("soundMagInsert")]
        public string? SoundMagInsert { get; }

        [ViewVariables]
        [DataField("soundMagEject")]
        public string? SoundMagEject { get; }

        // If our bolt is open then we can directly insert ammo into it.
        // This is useful for stuff that is single-shot and has no need for any kind of magazine.

        // TODO: Maybe have a separate magazine prototype for where the magazine is internal that way we don't need duplicate entities
        // for guns that have internal?
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
        public SharedAmmoProviderComponent? Magazine => MagazineSlot?.ContainedEntity?.GetComponent<SharedAmmoProviderComponent>();

        public ContainerSlot MagazineSlot = default!;

        [ViewVariables]
        [DataField("magFillPrototype", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        private string? _magazinePrototype;

        [ViewVariables]
        [DataField("magazineTypes")]
        public GunMagazine MagazineTypes { get; } = GunMagazine.Unspecified;

        [ViewVariables]
        [DataField("currentSelector")]
        public GunFireSelector CurrentSelector { get; private set; } = GunFireSelector.Safety;

        [ViewVariables]
        [DataField("allSelectors")]
        public GunFireSelector AllSelectors { get; } = GunFireSelector.Safety;

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
            DebugTools.Assert(_magazinePrototype == null || IoCManager.Resolve<IPrototypeManager>().HasIndex<EntityPrototype>(_magazinePrototype));

            // Pre-spawn magazine in
            MagazineSlot = Owner.EnsureContainer<ContainerSlot>("magazine", out var existingMag);

            if (!existingMag && _magazinePrototype != null)
            {
                var mag = Owner.EntityManager.SpawnEntity(_magazinePrototype, Owner.Transform.Coordinates);
                MagazineSlot.Insert(mag);
                UpdateAppearance();
                Dirty();
            }

            if (InternalMagazine && MagazineSlot.ContainedEntity == null)
            {
                throw new InvalidOperationException();
            }

            var mago = Magazine;

            if (mago != null && (MagazineTypes & mago.MagazineType) == 0)
            {
                // This can still work but it just means we can't put the mag back in
                Logger.ErrorS("gun", $"{Owner} has a magazine with a different type than its allowed types!");
            }
        }

        public void ChangeSelector(GunFireSelector selector)
        {
            if (CurrentSelector == selector) return;

            if ((AllSelectors & selector) == 0)
            {
                throw new InvalidOperationException($"Tried to change fire selector for {Owner} but {selector} isn't valid for it!");
            }

            CurrentSelector = selector;
        }

        public bool TryInsertMagazine(SharedAmmoProviderComponent magazine)
        {
            if (MagazineSlot == null) return false;
            if (MagazineSlot.ContainedEntity != null || !MagazineSlot.Insert(magazine.Owner)) return false;
            if (SoundMagInsert != null)
                SoundSystem.Play(Filter.Pvs(Owner), SoundMagInsert, Owner); // TODO: Variations + volumes

            UpdateAppearance();
            return true;
        }

        public bool TryRemoveMagazine([NotNullWhen(true)] out SharedAmmoProviderComponent? magazine)
        {
            magazine = MagazineSlot?.ContainedEntity?.GetComponent<SharedAmmoProviderComponent>();

            if (magazine == null)
            {
                return false;
            }

            if (MagazineSlot?.Remove(magazine.Owner) != true)
            {
                magazine = null;
                return false;
            }

            UpdateAppearance();
            return true;
        }

        public virtual bool CanFire()
        {
            if (CurrentSelector == GunFireSelector.Safety) return false;
            return true;
        }

        public virtual void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out SharedAppearanceComponent? appearance)) return;

            var mag = Magazine;

            if (mag != null)
            {
                mag.UpdateAppearance(appearance);
                appearance.SetData(GunVisuals.MagLoaded, true);
                appearance.SetData(GunVisuals.AmmoCount, mag.AmmoCount);
                appearance.SetData(GunVisuals.AmmoMax, mag.AmmoMax);
            }
            else
            {
                appearance.SetData(GunVisuals.MagLoaded, false);
                appearance.SetData(GunVisuals.AmmoCount, 0);
                appearance.SetData(GunVisuals.AmmoMax, 0);
            }

            // TODO: All the other appearance updates for bolts and shiznit.
        }
    }

    // I think all guns have magazines we just need to determine if internal or not
    // Magazine needs a bool for whether it autoejects on empty

    // Uhh bool for whether we can manually cycle
    // Bool for whether it autocycles

    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    public abstract class SharedBatteryAmmoProviderComponent : SharedAmmoProviderComponent
    {
        public override string Name => "BatteryAmmoProvider";
    }

    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    public abstract class SharedBallisticMagazineComponent : SharedBallisticsAmmoProvider
    {
        public override string Name => "BallisticAmmoProvider";

        // Sounds
        [ViewVariables]
        [DataField("soundRack")]
        public string? SoundRack { get; } = null;

        [ViewVariables]
        [DataField("rackVariation")]
        public float RackVariation { get; } = 0.01f;

        [ViewVariables]
        [DataField("rackVolume")]
        public float RackVolume { get; } = 0.0f;

        public override void UpdateAppearance(SharedAppearanceComponent appearance)
        {
            // TODO: Suss this shit out
            base.UpdateAppearance(appearance);
            appearance?.SetData(GunVisuals.MagLoaded, true);
            appearance?.SetData(GunVisuals.AmmoCount, AmmoCount);
            appearance?.SetData(GunVisuals.AmmoMax, AmmoCapacity);
        }
    }

    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    public abstract class SharedRevolverAmmoProviderComponent : SharedBallisticsAmmoProvider, ISerializationHooks
    {
        public override string Name => "RevolverAmmoProvider";

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
            _revolver = new SharedAmmoComponent?[AmmoCapacity];
        }

        private void Cycle()
        {
            // TODO: Copy and shit.
        }
    }

    public abstract class SharedReagentAmmoProviderComponent : SharedAmmoProviderComponent
    {
        public override string Name => "ReagentAmmoProvider";
    }

    /*
     * Okay so: SharedAmmoProvider -> SharedBatteryProvider
     * SharedAmmoProvider -> SharedBallisticsProvider -> SharedMagazineProvider
     */

    public abstract class SharedBallisticsAmmoProvider : SharedAmmoProviderComponent
    {
        /// <inheritdoc />
        [ViewVariables]
        [DataField("capacity")]
        public int AmmoCapacity { get; }

        /// <inheritdoc />
        public int UnspawnedCount { get; protected set; }

        /// <inheritdoc />
        [ViewVariables]
        [DataField("fillPrototype")]
        public string? FillPrototype { get; }

        public override void Initialize()
        {
            base.Initialize();
            if (FillPrototype != null)
            {
                UnspawnedCount = AmmoCapacity;
            }
        }

        public abstract bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo);
    }

    public abstract class SharedAmmoProviderComponent : Component
    {
        public override string Name => "AmmoProvider";

        // TODO: Most of the below seems more suited to a magazine weapon
        // Try working on the powercell one for a bit and see what flows.

        /// <summary>
        /// Does the magazine only transfer to other magazines / guns.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("speedLoader")] public bool SpeedLoader { get; } = false;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("caliber")]
        public GunCaliber Caliber { get; } = GunCaliber.Unspecified;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("magazineType")]
        public GunMagazine MagazineType { get; } = GunMagazine.Unspecified;

        public IEntity? Shooter()
        {
            return null;
        }

        public abstract int AmmoCount { get; }
        public abstract int AmmoMax { get; }

        public virtual bool CanShoot()
        {
            return true;
        }

        public abstract bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile);

        public virtual void UpdateAppearance(SharedAppearanceComponent appearance)
        {
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

    /// <summary>
    /// Because we have multiple very different types of "projectiles" that can be fired we need an interface for them.
    /// This is because we could fire an entity or just use a hitscan prototype etc.
    /// </summary>
    public interface IProjectile
    {
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
