#nullable enable
using System;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Verbs;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using Component = Robust.Shared.GameObjects.Component;
using IContainer = Robust.Shared.Interfaces.GameObjects.Components.IContainer;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    public enum BallisticCaliber
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
    }

    [Serializable, NetSerializable]
    public class RangedFireMessage : EntitySystemMessage
    {
        
    }

    public interface IRangedWeapon
    {
        FireRateSelector Selector { get; }
        TimeSpan NextFire { get; set; }
        float FireRate { get; }
        bool CanFire(IEntity entity);
        void MuzzleFlash();
    }

    /// <summary>
    ///     Allows this entity to be loaded into a ranged weapon (if the caliber matches)
    ///     Generally used for bullets but can be used for other things like bananas
    /// </summary>
    public abstract class SharedAmmoComponent : Component
    {
        public override string Name => "Ammo";
        
        [ViewVariables]
        public BallisticCaliber Caliber { get; private set; }
        
        [ViewVariables]
        public bool Spent
        {
            get
            {
                if (_ammoIsProjectile)
                {
                    return false;
                }

                return _spent;
            }
        }
        private bool _spent;

        /// <summary>
        ///     Used for anything without a case that fires itself, like if you loaded a banana into a banana launcher.
        /// </summary>
        private bool _ammoIsProjectile;

        /// <summary>
        ///     Used for ammo that is deleted when the projectile is retrieved
        /// </summary>
        [ViewVariables]
        public bool Caseless { get; private set; }
        
        // Rather than managing bullet / case state seemed easier to just have 2 toggles
        // ammoIsProjectile being for a beanbag for example and caseless being for ClRifle rounds

        /// <summary>
        ///     For shotguns where they might shoot multiple entities
        /// </summary>
        [ViewVariables]
        public byte ProjectilesFired { get; private set; }

        /// <summary>
        ///     Prototype ID of the entity to be spawned.
        /// </summary>
        [ViewVariables]
        public string? ProjectileId { get; private set; }
        
        /// <summary>
        ///     How far apart each entity is if multiple are shot, like with a shotgun.
        /// </summary>
        [ViewVariables]
        public float EvenSpreadAngle { get; private set; }
        
        /// <summary>
        ///     How fast the shot entities travel
        /// </summary>
        [ViewVariables]
        public float Velocity { get; private set; }

        [ViewVariables]
        public string? MuzzleFlashSprite { get; set; }

        public string? SoundCollectionEject { get; private set; }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            // For shotty or whatever as well
            serializer.DataReadWriteFunction(
                "projectile", 
                null, 
                projectile => ProjectileId = projectile,
                () => ProjectileId);
            
            serializer.DataReadWriteFunction(
                "caliber", 
                BallisticCaliber.Unspecified, 
                caliber => Caliber = caliber,
                () => Caliber);

            serializer.DataReadWriteFunction(
                "projectilesFired", 
                1, 
                numFired => ProjectilesFired = (byte) numFired,
                () => ProjectilesFired);

            serializer.DataReadWriteFunction(
                "ammoSpread", 
                0, 
                spread => EvenSpreadAngle = spread,
                () => EvenSpreadAngle);
            
            serializer.DataReadWriteFunction(
                "ammoVelocity", 
                20.0f, 
                velocity => Velocity = velocity,
                () => Velocity);
            
            serializer.DataField(ref _ammoIsProjectile, "isProjectile", false);
            
            serializer.DataReadWriteFunction(
                "caseless", 
                false, 
                caseless => Caseless = caseless,
                () => Caseless);
            
            // Being both caseless and shooting yourself doesn't make sense
            DebugTools.Assert(!(_ammoIsProjectile && Caseless));

            serializer.DataReadWriteFunction(
                "muzzleFlash", 
                "Objects/Weapons/Guns/Projectiles/bullet_muzzle.png", 
                muzzle => MuzzleFlashSprite = muzzle,
                () => MuzzleFlashSprite);
            
            serializer.DataReadWriteFunction(
                "soundCollectionEject", 
                "CasingEject", 
                soundEject => SoundCollectionEject = soundEject,
                () => SoundCollectionEject);

            if (ProjectilesFired < 1)
            {
                Logger.Error("Ammo can't have less than 1 projectile");
            }

            if (EvenSpreadAngle > 0 && ProjectilesFired == 1)
            {
                Logger.Error("Can't have an even spread if only 1 projectile is fired");
                throw new InvalidOperationException();
            }
        }

        public virtual bool TryTakeBullet(out IEntity? entity)
        {
            if (_ammoIsProjectile)
            {
                entity = Owner;
                return true;
            }

            if (_spent)
            {
                entity = null;
                return false;
            }

            _spent = true;
            /* TODO: Client-side
            if (Owner.TryGetComponent(out AppearanceComponent appearanceComponent))
            {
                appearanceComponent.SetData(AmmoVisuals.Spent, true);
            }
            */
            
            entity = Owner.EntityManager.SpawnEntity(ProjectileId, Owner.Transform.MapPosition);

            DebugTools.AssertNotNull(entity);
            return true;
        }

        // TODO: Implement client and server-side.
        public abstract void MuzzleFlash(GridCoordinates grid, Angle angle);
    }
    
    // TODO: Need lightweight messages for syncing
    
    public abstract class SharedRevolverBarrelComponent : Component, IInteractUsing, IUse
    {
        public override string Name => "RevolverBarrel";

        protected BallisticCaliber Caliber;
        
        /// <summary>
        ///     What slot will be used for the next bullet.
        /// </summary>
        protected ushort CurrentSlot = 0;

        protected IEntity?[] AmmoSlots = null!;
        
        // TODO: Use ContainerSlot on the server
        protected abstract IContainer AmmoContainer { get; set; }

        protected string? FillPrototype;
        
        /// <summary>
        ///     To avoid spawning entities in until necessary we'll just keep a counter for the unspawned default ammo.
        /// </summary>
        protected ushort UnspawnedCount;

        // Sounds
        protected string? SoundEject;
        protected string? SoundInsert;
        protected string? SoundSpin;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref Caliber, "caliber", BallisticCaliber.Unspecified);
            
            serializer.DataReadWriteFunction(
                "capacity",
                6,
                cap => AmmoSlots = new IEntity[cap],
                () => AmmoSlots.Length);
            serializer.DataField(ref FillPrototype, "fillPrototype", null);

            // Sounds
            serializer.DataField(ref SoundEject, "soundEject", "/Audio/Weapons/Guns/MagOut/revolver_magout.ogg");
            serializer.DataField(ref SoundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/revolver_magin.ogg");
            serializer.DataField(ref SoundSpin, "soundSpin", "/Audio/Weapons/Guns/Misc/revolver_spin.ogg");
        }

        private void Cycle()
        {
            // Move up a slot
            CurrentSlot = (ushort) ((CurrentSlot + 1) % AmmoSlots.Length);
        }

        /// <summary>
        ///     Takes a projectile out if possible
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public IEntity? TakeProjectile()
        {
            var ammo = AmmoSlots[CurrentSlot];
            IEntity? bullet = null;
            if (ammo != null)
            {
                var ammoComponent = ammo.GetComponent<SharedAmmoComponent>();

                if (ammoComponent.TryTakeBullet(out bullet) && ammoComponent.Caseless)
                {
                    AmmoSlots[CurrentSlot] = null;
                    AmmoContainer.Remove(ammo);
                }
            }
            
            Cycle();
            return bullet;
        }
        
        // TODO: EJECTCASING should be on like a GunManager.

        /// <summary>
        ///     Dumps all cartridges onto the ground.
        /// </summary>
        /// <returns>The number of cartridges ejected</returns>
        private ushort EjectAllSlots()
        {
            ushort dumped = 0;
            
            for (var i = 0; i < AmmoSlots.Length; i++)
            {
                var entity = AmmoSlots[i];
                if (entity == null)
                {
                    continue;
                }

                AmmoContainer.Remove(entity);
                // TODO: MANAGER EjectCasing(entity);
                AmmoSlots[i] = null;
                dumped++;
            }

            // May as well point back at the end?
            CurrentSlot = (ushort) (AmmoSlots.Length - 1);
            return dumped;
        }
        
        private bool TryInsertBullet(IEntity user, IEntity entity)
        {
            if (!entity.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != Caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            // Functions like a stack
            // These are inserted in reverse order but then when fired Cycle will go through in order
            // The reason we don't just use an actual stack is because spin can select a random slot to point at
            for (var i = AmmoSlots.Length - 1; i >= 0; i--)
            {
                var slot = AmmoSlots[i];
                if (slot == null)
                {
                    CurrentSlot = (byte) i;
                    AmmoSlots[i] = entity;
                    AmmoContainer.Insert(entity);
                    return true;
                }
            }

            Owner.PopupMessage(user, Loc.GetString("Ammo full"));
            return false;
        }

        /// <summary>
        /// Eject all casings
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public bool UseEntity(UseEntityEventArgs eventArgs)
        {
            var dumped = EjectAllSlots();

            if (dumped > 0)
            {
                // TODO: IF client-side predict and play sound
            }

            return true;
        }

        public async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (TryInsertBullet(eventArgs.User, eventArgs.Using))
            {
                // TODO: Sound (both sides) / appearance (client-side).
            }

            return true;
        }
    }
    
    public abstract class SharedRangedBarrelComponent : Component
    {
        public abstract FireRateSelector FireRateSelector { get; }
        public abstract FireRateSelector AllRateSelectors { get; }
        public abstract float FireRate { get; }
        public abstract int ShotsLeft { get; }
        public abstract int Capacity { get; }
    }

    [Flags]
    public enum FireRateSelector
    {
        Safety = 0,
        Single = 1 << 0,
        Automatic = 1 << 1,
    }
}