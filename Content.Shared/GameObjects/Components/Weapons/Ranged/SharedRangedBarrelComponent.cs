#nullable enable
using System;
using System.Collections.Generic;
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
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Players;
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
    public class RangedFiringMessage : ComponentMessage
    {
        public bool Firing { get; }

        public RangedFiringMessage(bool firing)
        {
            Firing = firing;
        }
    }

    [Serializable, NetSerializable]
    public class RangedAngleMessage : ComponentMessage
    {
        public Angle? Angle { get; }

        public RangedAngleMessage(Angle? angle)
        {
            Angle = angle;
        }
    }

    public interface IServerRangedWeapon {}

    public abstract class SharedRangedWeapon : Component, IEquipped
    {
        public override string Name => "RangedWeapon";

        public FireRateSelector Selector { get; protected set; }
        
        public TimeSpan NextFire { get; protected set; }
        
        public float FireRate { get; protected set; }
        
        private ushort _shotCounter = 0;
        
        // Shooting
        // So I guess we'll try syncing start and stop fire, as well as fire angles
        public abstract bool Firing { get; set; }

        public abstract Angle? FireAngle { get; set; }
        
        // Sounds
        protected string? SoundGunshot { get; }
        protected string? SoundEmpty { get; }
        
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            
            serializer.DataReadWriteFunction(
                "fireRate", 
                0.0f, 
                rate => FireRate = rate,
                () => FireRate);
            
            serializer.DataReadWriteFunction(
                "currentSelector", 
                FireRateSelector.Safety, 
                value => Selector = value, 
                () => Selector);
            
            serializer.DataReadWriteFunction(
                "allSelectors", 
                new List<FireRateSelector>(),
                selectors => selectors.ForEach(value => Selector |= value),
                () =>
                {
                    var result = new List<FireRateSelector>();
                    
                    foreach (FireRateSelector selector in Enum.GetValues(typeof(FireRateSelector)))
                    {
                        if ((selector & Selector) != 0)
                            result.Add(selector);
                    }

                    return result;
                });
        }

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);
            if (session?.AttachedEntity != Shooter())
            {
                Logger.Warning("Cheat cheat");
                return;
            }
            
            switch (message)
            {
                case RangedAngleMessage msg:
                    FireAngle = msg.Angle;
                    break;
                case RangedFiringMessage msg:
                    Firing = msg.Firing;
                    if (!Firing)
                    {
                        FireAngle = null;
                    }
                    break;
            }
        }

        public IEntity? Shooter()
        {
            if (!ContainerHelpers.TryGetContainer(Owner, out var container))
            {
                return null;
            }

            return container.Owner;
        }

        protected virtual bool CanFire(IEntity entity)
        {
            if (FireRate <= 0.0f)
            {
                return false;
            }
            
            switch (Selector)
            {
                case FireRateSelector.Safety:
                    return false;
                case FireRateSelector.Single:
                    return _shotCounter < 1;

                case FireRateSelector.Automatic:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public abstract void MuzzleFlash();

        /// <summary>
        ///     Whether we can take another bullet for our running total.
        ///     Won't actually take the bullet out; that's done under Shoot.
        /// </summary>
        /// <returns></returns>
        protected virtual bool TryTakeBullet()
        {
            switch (Selector)
            {
                case FireRateSelector.Safety:
                    return false;
                case FireRateSelector.Single:
                    return _shotCounter < 1;
                case FireRateSelector.Automatic:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public bool TryFire(TimeSpan currentTime, IEntity entity, Angle direction)
        {
            if (currentTime < NextFire)
            {
                return false;
            }
            
            // If it's our first shot then we'll fire at least 1 bullet now.
            if (_shotCounter == 0 && NextFire <= currentTime)
            {
                NextFire = currentTime;
            }
            
            // We'll send them a popup explaining why they can't as well.
            if (!CanFire(entity))
            {
                return false;
            }
            
            MuzzleFlash();

            ushort firedShots = 0;

            // To handle guns with firerates higher than framerate / tickrate
            while (NextFire <= currentTime)
            {
                NextFire += TimeSpan.FromSeconds(1 / FireRate);
                
                // Mainly check if we can get more bullets (e.g. if there's only 1 left in the clip).
                if (!TryTakeBullet())
                {
                    PlaySound(SoundEmpty);
                    break;
                }
                
                PlaySound(SoundGunshot);
                firedShots++;
                _shotCounter++;
            }
            
            // SO server-side we essentially need to backtrack by n firedShots to work out what to shoot for each one
            // Client side we'll just play the effects and shit unless we get client-side entity prediction in.
            Shoot(firedShots, direction);
            
            NextFire = currentTime + TimeSpan.FromSeconds(1 / FireRate);
            return true;
        }
        
        // TODO: We need a "StartFiring" message so the NextFire gets reset to now. Also need to verify it.

        /// <summary>
        ///     Fire out the specified number of bullets.
        ///     Client-side this will just play the specified number of sounds and a muzzle flash.
        ///     Server-side this will work out each bullet to spawn and fire them.
        /// </summary>
        /// <param name="shotCount"></param>
        /// <param name="direction"></param>
        protected abstract void Shoot(int shotCount, Angle direction);

        public void Equipped(EquippedEventArgs eventArgs)
        {
            ResetFire();
        }

        protected void ResetFire()
        {
            _shotCounter = 0;
            NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
            FireAngle = null;
        }
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

        public bool Spent { get; set; }

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
        public string ProjectileId { get; private set; } = default!;
        
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

        public bool CanFire()
        {
            if (Spent && !_ammoIsProjectile)
            {
                return false;
            }

            return true;
        }
    }

    public abstract class SharedRevolverBarrelComponent : SharedRangedWeapon, IInteractUsing, IUse
    {
        public override string Name => "RevolverWeapon";
        public override uint? NetID => ContentNetIDs.MAGAZINE_BARREL;

        protected BallisticCaliber Caliber;
        
        /// <summary>
        ///     What slot will be used for the next bullet.
        /// </summary>
        protected ushort CurrentSlot = 0;

        protected IEntity?[] AmmoSlots = null!;

        protected IContainer AmmoContainer { get; set; } = default!;

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

        protected void Cycle()
        {
            // Move up a slot
            CurrentSlot = (ushort) ((CurrentSlot + 1) % AmmoSlots.Length);
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
                    NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
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

    [Flags]
    public enum FireRateSelector
    {
        Safety = 0,
        Single = 1 << 0,
        Automatic = 1 << 1,
    }
}