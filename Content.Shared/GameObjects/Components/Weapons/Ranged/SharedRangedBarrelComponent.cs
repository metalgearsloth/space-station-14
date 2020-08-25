#nullable enable
using System;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Verbs;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Audio;
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
        /// Used for anything without a case that fires itself
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
        // How far apart each entity is if multiple are shot
        public float EvenSpreadAngle => _evenSpreadAngle;
        private float _evenSpreadAngle;
        /// <summary>
        /// How fast the shot entities travel
        /// </summary>
        public float Velocity => _velocity;
        private float _velocity;

        private string? _muzzleFlashSprite;

        public string? SoundCollectionEject => _soundCollectionEject;
        private string? _soundCollectionEject;

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
            
            // TODO: Up to here
            // Used for shotty to determine overall pellet spread
            serializer.DataField(ref _evenSpreadAngle, "ammoSpread", 0);
            serializer.DataField(ref _velocity, "ammoVelocity", 20.0f);
            serializer.DataField(ref _ammoIsProjectile, "isProjectile", false);
            serializer.DataReadWriteFunction(
                "caseless", 
                false, 
                caseless => Caseless = caseless,
                () => Caseless);
            // Being both caseless and shooting yourself doesn't make sense
            DebugTools.Assert(!(_ammoIsProjectile && Caseless));
            serializer.DataField(ref _muzzleFlashSprite, "muzzleFlash", "Objects/Weapons/Guns/Projectiles/bullet_muzzle.png");
            serializer.DataField(ref _soundCollectionEject, "soundCollectionEject", "CasingEject");

            if (_projectilesFired < 1)
            {
                Logger.Error("Ammo can't have less than 1 projectile");
            }

            if (_evenSpreadAngle > 0 && _projectilesFired == 1)
            {
                Logger.Error("Can't have an even spread if only 1 projectile is fired");
                throw new InvalidOperationException();
            }
        }

        public virtual bool TryTakeBullet(GridCoordinates spawnAtGrid, MapCoordinates spawnAtMap, out IEntity? entity)
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

            entity = spawnAtGrid.GridID != GridId.Invalid ? Owner.EntityManager.SpawnEntity(_projectileId, spawnAtGrid) : Owner.EntityManager.SpawnEntity(_projectileId, spawnAtMap);

            DebugTools.AssertNotNull(entity);
            return true;
        }

        // TODO: Implement client and server-side.
        public abstract void MuzzleFlash(GridCoordinates grid, Angle angle);
    }
    
    public abstract class SharedRevolverBarrelComponent : Component, IInteractUsing
    {
        public override string Name => "RevolverBarrel";
        private BallisticCaliber _caliber;
        private ushort _currentSlot = 0;
        public abstract ushort Capacity { get; }
        private IEntity?[] _ammoSlots;
        
        public abstract ushort ShotsLeft { get; }
        
        private string? _fillPrototype;
        private ushort _unspawnedCount;

        // TODO: Use ContainerSlot on the server
        protected abstract IContainer AmmoContainer { get; set; }

        // Sounds
        private string _soundEject;
        private string _soundInsert;
        private string _soundSpin;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _caliber, "caliber", BallisticCaliber.Unspecified);
            serializer.DataReadWriteFunction(
                "capacity",
                6,
                cap => _ammoSlots = new IEntity[cap],
                () => _ammoSlots.Length);
            serializer.DataField(ref _fillPrototype, "fillPrototype", null);

            // Sounds
            serializer.DataField(ref _soundEject, "soundEject", "/Audio/Weapons/Guns/MagOut/revolver_magout.ogg");
            serializer.DataField(ref _soundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/revolver_magin.ogg");
            serializer.DataField(ref _soundSpin, "soundSpin", "/Audio/Weapons/Guns/Misc/revolver_spin.ogg");
        }

        public bool TryInsertBullet(IEntity user, IEntity entity)
        {
            if (!entity.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            if (ammoComponent.Caliber != _caliber)
            {
                Owner.PopupMessage(user, Loc.GetString("Wrong caliber"));
                return false;
            }

            // Functions like a stack
            // These are inserted in reverse order but then when fired Cycle will go through in order
            // The reason we don't just use an actual stack is because spin can select a random slot to point at
            for (var i = _ammoSlots.Length - 1; i >= 0; i--)
            {
                var slot = _ammoSlots[i];
                if (slot == null)
                {
                    _currentSlot = (byte) i;
                    _ammoSlots[i] = entity;
                    AmmoContainer.Insert(entity);
                    return true;
                }
            }

            Owner.PopupMessage(user, Loc.GetString("Ammo full"));
            return false;
        }

        public void Cycle()
        {
            // Move up a slot
            _currentSlot = (ushort) ((_currentSlot + 1) % _ammoSlots.Length);
        }

        /* TODO: CLIENT-SIDE
        /// <summary>
        /// Russian Roulette
        /// </summary>
        public void Spin()
        {
            var random = IoCManager.Resolve<IRobustRandom>().Next(_ammoSlots.Length - 1);
            _currentSlot = random;
            if (_soundSpin != null)
            {
                EntitySystem.Get<AudioSystem>().PlayAtCoords(_soundSpin, Owner.Transform.GridPosition, AudioParams.Default.WithVolume(-2));
            }
        }
        */

        public IEntity PeekAmmo()
        {
            return _ammoSlots[_currentSlot];
        }

        /// <summary>
        /// Takes a projectile out if possible
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public IEntity TakeProjectile(MapCoordinates spawnAtMap)
        {
            var ammo = _ammoSlots[_currentSlot];
            IEntity bullet = null;
            if (ammo != null)
            {
                var ammoComponent = ammo.GetComponent<SharedAmmoComponent>();
                bullet = ammoComponent.TakeBullet(spawnAtGrid, spawnAtMap);
                if (ammoComponent.Caseless)
                {
                    _ammoSlots[_currentSlot] = null;
                    AmmoContainer.Remove(ammo);
                }
            }
            Cycle();
            return bullet;
        }
        
        // TODO: EJECTCASING should be on like a GunManager.

        /// <summary>
        /// Dumps all cartridges onto the ground.
        /// </summary>
        /// <returns>The number of cartridges ejected</returns>
        private ushort EjectAllSlots()
        {
            ushort dumped = 0;
            
            for (var i = 0; i < _ammoSlots.Length; i++)
            {
                var entity = _ammoSlots[i];
                if (entity == null)
                {
                    continue;
                }

                AmmoContainer.Remove(entity);
                // TODO: MANAGER EjectCasing(entity);
                _ammoSlots[i] = null;
                dumped++;
            }

            // May as well point back at the end?
            _currentSlot = (ushort) (_ammoSlots.Length - 1);
            return dumped;
        }

        /// <summary>
        /// Eject all casings
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            var dumped = EjectAllSlots();

            if (dumped > 0)
            {
                // TODO: IF client-side predict and play sound
                
            }

            return true;
        }

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (TryInsertBullet(eventArgs.User, eventArgs.Using))
            {
                // TODO: Sound (both sides) / appearance (client-side).
            }
        }

        // TODO: Do spin verb client-side
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