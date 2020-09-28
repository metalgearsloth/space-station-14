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
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
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
    public class StartFiringMessage : EntitySystemMessage
    {
        public EntityUid Uid { get; }
        
        public MapCoordinates FireCoordinates { get; }

        public StartFiringMessage(EntityUid uid, MapCoordinates fireCoordinates)
        {
            Uid = uid;
            FireCoordinates = fireCoordinates;
        }
    }

    [Serializable, NetSerializable]
    public sealed class StopFiringMessage : EntitySystemMessage
    {
        public EntityUid Uid { get; }
        
        /// <summary>
        ///     We'll send the amount of shots we expected so the server can try to reconcile it.
        /// </summary>
        public ushort Shots { get; }

        public StopFiringMessage(EntityUid uid, ushort shots)
        {
            Uid = uid;
            Shots = shots;
        }
    }

    [Serializable, NetSerializable]
    public class RangedCoordinatesMessage : EntitySystemMessage
    {
        public EntityUid Uid { get; }
        
        public MapCoordinates? Coordinates { get; }

        public RangedCoordinatesMessage(EntityUid uid, MapCoordinates? coordinates)
        {
            Uid = uid;
            Coordinates = coordinates;
        }
    }

    public abstract class SharedRangedWeaponComponent : Component, IHandSelected, IInteractUsing, IUse
    {
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
        public ushort ShotCounter;
        
        // Shooting
        // So I guess we'll try syncing start and stop fire, as well as fire angles
        public bool Firing { get; set; }
        
        /// <summary>
        ///     Filepath to MuzzleFlash texture
        /// </summary>
        public string? MuzzleFlash { get; set; }
        
        /// <summary>
        ///     The angle the shooter selected to fire at.
        /// </summary>
        public MapCoordinates? FireCoordinates { get; set; }
        
        public ushort ExpectedShots { get; set; }
        
        public ushort AccumulatedShots { get; set; }
        
        // Sounds
        public string? SoundGunshot { get; protected set; }
        public string? SoundEmpty { get; protected set; }
        
        // Audio profile
        protected const float GunshotVariation = 0.1f;
        protected const float EmptyVariation = 0.1f;
        protected const float CycleVariation = 0.1f;
        protected const float BoltToggleVariation = 0.1f;
        protected const float InsertVariation = 0.1f;
        
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
                "Objects/Weapons/Guns/Projectiles/bullet_muzzle.png",
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
                "/Audio/Weapons/Guns/Empty/empty.ogg",
                sound => SoundEmpty = sound,
                () => SoundEmpty
            );
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

        /// <summary>
        ///     Called by the ranged weapon system if no bullets were fired by the gun
        /// </summary>
        protected virtual void NoShotsFired() {}

        protected virtual bool CanFire(IEntity entity)
        {
            if (FireRate <= 0.0f || FireCoordinates == null)
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
        ///     Try to shoot the gun for this tick.
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="entity"></param>
        /// <param name="position"></param>
        /// <returns>false if firing is impossible, true if firing is possible but delayed or we did fire</returns>
        public bool TryFire(TimeSpan currentTime, IEntity entity, MapCoordinates position)
        {
            if (ShotCounter == 0 && NextFire <= currentTime)
            {
                NextFire = currentTime;
            }
            
            if (!CanFire(entity))
            {
                return false;
            }
            
            if (currentTime < NextFire)
            {
                return true;
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
                NoShotsFired();
                // EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundEmpty);
                return false;
            }

            AccumulatedShots += firedShots;
            // SO server-side we essentially need to backtrack by n firedShots to work out what to shoot for each one
            // Client side we'll just play the effects and shit unless we get client-side entity prediction in.
            Shoot(firedShots, position);

            return true;
        }

        /// <summary>
        ///     Fire out the specified number of bullets.
        ///     Client-side this will just play the specified number of sounds and a muzzle flash.
        ///     Server-side this will work out each bullet to spawn and fire them.
        /// </summary>
        /// <param name="shotCount"></param>
        /// <param name="coordinates"></param>
        protected abstract void Shoot(int shotCount, MapCoordinates coordinates);

        void IHandSelected.HandSelected(HandSelectedEventArgs eventArgs)
        {
            ResetFire();
        }

        protected void ResetFire()
        {
            ShotCounter = 0;
            NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
            FireCoordinates = null;
        }

        public abstract Task<bool> InteractUsing(InteractUsingEventArgs eventArgs);

        public abstract bool UseEntity(UseEntityEventArgs eventArgs);
    }

    [Flags]
    public enum FireRateSelector
    {
        Safety = 0,
        Single = 1 << 0,
        Automatic = 1 << 1,
    }
}