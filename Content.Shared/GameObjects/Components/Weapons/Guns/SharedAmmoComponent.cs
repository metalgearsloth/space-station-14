using System;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Guns
{
    /// <summary>
    ///     Allows this entity to be loaded into a ranged weapon (if the caliber matches)
    ///     Generally used for bullets but can be used for other things like bananas
    /// </summary>
    public abstract class SharedAmmoComponent : Component, IProjectile
    {
        public override string Name => "Ammo";

        public override uint? NetID => ContentNetIDs.AMMO;

        [ViewVariables]
        [DataField("caliber")]
        public GunCaliber Caliber { get; private set; } = GunCaliber.Unspecified;

        public virtual bool Spent { get; set; }

        public bool AmmoIsProjectile => _ammoIsProjectile;

        // TODO: Particles or something less uggers
        /// <summary>
        /// Texture to use for a muzzle flash
        /// </summary>
        [ViewVariables]
        [DataField("muzzleFlash")]
        public string? MuzzleFlash { get; } = null; // TODO: No null

        /// <summary>
        ///     Used for anything without a case that fires itself, like if you loaded a banana into a banana launcher.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("isProjectile")]
        private bool _ammoIsProjectile = false;

        /// <summary>
        ///     Used for ammo that is deleted when the projectile is retrieved
        /// </summary>
        [ViewVariables]
        [DataField("caseless")]
        public bool Caseless { get; private set; }

        // Rather than managing bullet / case state seemed easier to just have 2 toggles
        // ammoIsProjectile being for a beanbag for example and caseless being for ClRifle rounds

        /// <summary>
        ///     For shotguns where they might shoot multiple entities
        /// </summary>
        [ViewVariables]
        [DataField("projectilesFired")]
        public byte ProjectilesFired { get; private set; } = 1;

        /// <summary>
        ///     Prototype ID of the entity to be spawned (projectile or hitscan).
        /// </summary>
        [ViewVariables]
        [DataField("projectile")]
        public string ProjectileId { get; private set; } = default!;

        public bool IsHitscan(IPrototypeManager? protoManager = null)
        {
            protoManager ??= IoCManager.Resolve<IPrototypeManager>();
            return protoManager.HasIndex<HitscanPrototype>(ProjectileId);
        }

        /// <summary>
        ///     How far apart each entity is if multiple are shot, like with a shotgun.
        /// </summary>
        [ViewVariables]
        [DataField("ammoSpread")]
        public float EvenSpreadAngle { get; private set; }

        /// <summary>
        ///     How fast the shot entities travel
        /// </summary>
        [ViewVariables]
        [DataField("ammoVelocity")]
        public float Velocity { get; private set; } = 20.0f;

        [ViewVariables]
        [DataField("soundCollectionEject")]
        public string? SoundCollectionEject { get; private set; } = "CasingEject";

        public override void Initialize()
        {
            base.Initialize();
            // TODO: Move to a test
            // Being both caseless and shooting yourself doesn't make sense
            DebugTools.Assert(!(_ammoIsProjectile && Caseless));
            if (ProjectilesFired < 1)
            {
                Logger.Error("Ammo can't have less than 1 projectile");
                throw new InvalidOperationException();
            }

            if (EvenSpreadAngle > 0 && ProjectilesFired == 1)
            {
                Logger.Error("Can't have an even spread if only 1 projectile is fired");
                throw new InvalidOperationException();
            }
        }

        public bool CanFire()
        {
            if (Spent && !_ammoIsProjectile)
            {
                return false;
            }

            return true;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AmmoComponentState : ComponentState
    {
        public bool Spent { get; }

        public AmmoComponentState(bool spent) : base(ContentNetIDs.AMMO)
        {
            Spent = spent;
        }
    }
}
