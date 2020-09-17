#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using Component = Robust.Shared.GameObjects.Component;

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
    ///     After the client is done shooting we'll sync how many shots are left just in case.
    /// </summary>
    [Serializable, NetSerializable]
    public class RangedShotsLeftMessage : ComponentMessage
    {
        public ushort ShotsLeft { get; }

        public RangedShotsLeftMessage(ushort shotsLeft)
        {
            ShotsLeft = shotsLeft;
        }
    }
    
    [Serializable, NetSerializable]
    public class StartFiringMessage : ComponentMessage
    {
        public Angle FireAngle { get; }

        public StartFiringMessage(Angle fireAngle)
        {
            FireAngle = fireAngle;
        }
    }

    [Serializable, NetSerializable]
    public sealed class StopFiringMessage : ComponentMessage
    {
        /// <summary>
        ///     We'll send the amount of shots we expected so the server can try to reconcile it.
        /// </summary>
        public ushort Shots { get; }

        public StopFiringMessage(ushort shots)
        {
            Shots = shots;
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

    public abstract class SharedRangedWeapon : Component, IHandSelected, IInteractUsing, IUse
    {
        public override string Name => "RangedWeapon";

        /// <summary>
        ///     Current fire selector.
        /// </summary>
        public FireRateSelector Selector { get; protected set; }
        
        /// <summary>
        ///     The earliest time the gun can fire next.
        /// </summary>
        public TimeSpan NextFire { get; protected set; }
        
        /// <summary>
        ///     Shots fired per second.
        /// </summary>
        public float FireRate { get; protected set; }
        
        /// <summary>
        ///     Keep a running track of how many shots we've fired for single-shot (etc.) weapons.
        /// </summary>
        protected ushort ShotCounter;
        
        // Shooting
        // So I guess we'll try syncing start and stop fire, as well as fire angles
        public abstract bool Firing { get; set; }
        
        /// <summary>
        ///     Filepath to MuzzleFlash texture
        /// </summary>
        public string? MuzzleFlash { get; set; }

        /// <summary>
        ///     The angle the shooter selected to fire at.
        /// </summary>
        public abstract Angle? FireAngle { get; set; }

        // TODO: A few of these are server-only so need to move in the refactor
        public ushort ExpectedShots { get; set; }
        
        public ushort AccumulatedShots { get; set; }
        
        // Sounds
        protected string? SoundGunshot { get; set; }
        protected string? SoundEmpty { get; set; }
        
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
            
            serializer.DataReadWriteFunction(
                "muzzleFlash",
                null,
                value => MuzzleFlash = value,
                () => MuzzleFlash);
            
            // Sounds
            serializer.DataReadWriteFunction(
                "soundGunshot",
                null,
                sound => SoundGunshot = sound,
                () => SoundGunshot
                );
            
            serializer.DataReadWriteFunction(
                "soundEmpty",
                null,
                sound => SoundEmpty = sound,
                () => SoundEmpty
            );
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
                case StartFiringMessage msg:
                    if (msg.FireAngle == null)
                    {
                        return;
                    }
                    Firing = true;
                    FireAngle = msg.FireAngle;
                    ShotCounter = 0;
                    break;
                case StopFiringMessage msg:
                    Firing = false;
                    ExpectedShots += msg.Shots;
                    break;
            }
        }

        /// <summary>
        ///     Lord help me this is bad.
        /// </summary>
        /// <returns></returns>
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
            if (FireRate <= 0.0f || FireAngle == null)
            {
                return false;
            }
            
            switch (Selector)
            {
                case FireRateSelector.Safety:
                    return false;
                case FireRateSelector.Single:
                    return ShotCounter < 1;
                case FireRateSelector.Automatic:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        ///     Whether we can take more ammo for shooting. Doesn't necessarily need to be fireable.
        /// </summary>
        /// <remarks>
        ///     Doesn't need to be fireable so something like a revolver can keep cycling through bullets even though they're not usable.
        /// </remarks>
        /// <returns></returns>
        protected virtual bool TryTakeAmmo()
        {
            switch (Selector)
            {
                case FireRateSelector.Safety:
                    return false;
                case FireRateSelector.Single:
                    return ShotCounter < 1;
                case FireRateSelector.Automatic:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        /// <summary>
        ///     
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="entity"></param>
        /// <param name="direction"></param>
        /// <returns>false if firing is impossible, true if firing is possible but delayed or we did fire</returns>
        public bool TryFire(TimeSpan currentTime, IEntity entity, Angle direction)
        {
            if (ShotCounter == 0 && NextFire <= currentTime)
            {
                NextFire = currentTime;
            }
            
            if (currentTime < NextFire)
            {
                return true;
            }
            
            if (!CanFire(entity))
            {
                return false;
            }

            ushort firedShots = 0;

            // To handle guns with firerates higher than framerate / tickrate
            while (NextFire <= currentTime)
            {
                NextFire += TimeSpan.FromSeconds(1 / FireRate);
                
                // Mainly check if we can get more bullets (e.g. if there's only 1 left in the clip).
                if (!TryTakeAmmo())
                {
                    break;
                }
                
                firedShots++;
                ShotCounter++;
            }

            // No ammo :(
            if (firedShots == 0)
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundEmpty);
                return false;
            }

            if (MuzzleFlash != null && FireAngle != null)
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().MuzzleFlash(Shooter(), Owner, MuzzleFlash, FireAngle.Value);
            }
            
            AccumulatedShots += firedShots;
            // SO server-side we essentially need to backtrack by n firedShots to work out what to shoot for each one
            // Client side we'll just play the effects and shit unless we get client-side entity prediction in.
            Shoot(firedShots, direction);

            return true;
        }

        /// <summary>
        ///     Fire out the specified number of bullets.
        ///     Client-side this will just play the specified number of sounds and a muzzle flash.
        ///     Server-side this will work out each bullet to spawn and fire them.
        /// </summary>
        /// <param name="shotCount"></param>
        /// <param name="direction"></param>
        protected abstract void Shoot(int shotCount, Angle direction);

        void IHandSelected.HandSelected(HandSelectedEventArgs eventArgs)
        {
            ResetFire();
        }

        protected void ResetFire()
        {
            ShotCounter = 0;
            NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
            FireAngle = null;
        }

        public abstract Task<bool> InteractUsing(InteractUsingEventArgs eventArgs);

        public abstract bool UseEntity(UseEntityEventArgs eventArgs);
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

        public bool Spent
        {
            get => _spent;
            set
            {
                if (_spent == value)
                {
                    return;
                }

                _spent = value;

                if (_spent)
                {
                    if (Caseless)
                    {
                        Owner.Delete();
                        return;
                    }
                }
            }
        }
        private bool _spent;

        public bool AmmoIsProjectile => _ammoIsProjectile;
        
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
                string.Empty, 
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

    public abstract class SharedRevolverBarrelComponent : SharedRangedWeapon
    {
        public override string Name => "RevolverBarrel";
        public override uint? NetID => ContentNetIDs.REVOLVER_BARREL;

        protected BallisticCaliber Caliber;
        
        /// <summary>
        ///     What slot will be used for the next bullet.
        /// </summary>
        protected ushort CurrentSlot = 0;

        protected abstract ushort Capacity { get; }

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
            serializer.DataField(ref FillPrototype, "fillPrototype", null);

            // Sounds
            serializer.DataField(ref SoundEject, "soundEject", "/Audio/Weapons/Guns/MagOut/revolver_magout.ogg");
            serializer.DataField(ref SoundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/revolver_magin.ogg");
            serializer.DataField(ref SoundSpin, "soundSpin", "/Audio/Weapons/Guns/Misc/revolver_spin.ogg");
        }

        protected void Cycle()
        {
            // Move up a slot
            CurrentSlot = (ushort) ((CurrentSlot + 1) % Capacity);
        }

        // TODO: EJECTCASING should be on like a GunManager.

        /// <summary>
        ///     Dumps all cartridges onto the ground.
        /// </summary>
        /// <returns>The number of cartridges ejected</returns>
        protected abstract ushort EjectAllSlots();

        protected virtual bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (ammoComponent.Caliber != Caliber)
                return false;

            return true;
        }
        
        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (!eventArgs.Target.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            return TryInsertBullet(eventArgs.User, ammoComponent);
        }

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            EjectAllSlots();
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