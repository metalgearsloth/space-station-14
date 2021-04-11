#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using YamlDotNet.Serialization;
using Component = Robust.Shared.GameObjects.Component;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public enum BallisticCaliber : byte
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
        public int ShotsLeft { get; }

        public RangedShotsLeftMessage(int shotsLeft)
        {
            ShotsLeft = shotsLeft;
        }
    }

    [Serializable, NetSerializable]
    public class ShootMessage : EntityEventArgs
    {
        public EntityUid Uid;
        public MapCoordinates FireCoordinates;
        public int Shots;
        public TimeSpan CurrentTime;

        public ShootMessage(EntityUid uid, MapCoordinates fireCoordinates, int shots, TimeSpan currentTime)
        {
            Uid = uid;
            FireCoordinates = fireCoordinates;
            Shots = shots;
            CurrentTime = currentTime;
        }
    }

    [Serializable, NetSerializable]
    public sealed class StopFiringMessage : EntityEventArgs
    {
        public EntityUid Uid { get; }

        public int ExpectedShots { get; }

        public StopFiringMessage(EntityUid uid, int expectedShots)
        {
            Uid = uid;
            ExpectedShots = expectedShots;
        }
    }

    [Serializable, NetSerializable]
    public class RangedFireMessage : EntityEventArgs
    {
        /// <summary>
        ///     Gun Uid
        /// </summary>
        public EntityUid Uid { get; }

        /// <summary>
        ///     Coordinates to shoot at.
        ///     If list empty then we'll stop shooting.
        /// </summary>
        public MapCoordinates FireCoordinates { get; }

        public RangedFireMessage(EntityUid uid, MapCoordinates fireCoordinates)
        {
            Uid = uid;
            FireCoordinates = fireCoordinates;
        }
    }

    public abstract class SharedRangedWeaponComponent : Component, IHandSelected, IInteractUsing, IUse, IGun, ISerializationHooks
    {
        /// <summary>
        ///     Current fire selector.
        /// </summary>
        [DataField("currentSelector")]
        public FireRateSelector Selector { get; set; } = FireRateSelector.Safety;

        /// <summary>
        ///     All available fire selectors
        /// </summary>
        [DataField("allSelectors")]
        public FireRateSelector AllSelectors { get; protected set; }

        /// <summary>
        ///     The earliest time the gun can fire next.
        /// </summary>
        public TimeSpan NextFire { get; set; }

        /// <summary>
        ///     The last time we fired. Useful for calculating recoil angles.
        /// </summary>
        public TimeSpan LastFire { get; set; }

        /// <summary>
        ///     Shots fired per second.
        /// </summary>
        [DataField("fireRate")]
        public float FireRate { get; set; } = 0.0f;

        /// <summary>
        ///     Keep a running track of how many shots we've fired for single-shot (etc.) weapons.
        /// </summary>
        public int ShotCounter { get; set; }

        // These 2 are mainly for handling desyncs so the server fires the same number of shots as the client.
        // Someone smarter probably has a better way of doing it but these seemed to work okay...
        public int ExpectedShots { get; set; }

        public int AccumulatedShots { get; set; }

        /// <summary>
        ///     Filepath to MuzzleFlash texture
        /// </summary>
        [DataField("muzzleFlash")]
        [ViewVariables(VVAccess.ReadWrite)]
        public string? MuzzleFlash { get; set; } = "Objects/Weapons/Guns/Projectiles/bullet_muzzle.png";

        public bool Firing { get; set; }

        /// <summary>
        ///     Multiplies the ammo spread to get the final spread of each pellet
        /// </summary>
        [DataField("ammoSpreadRatio")]
        public float AmmoSpreadRatio { get; set; } = 1.0f;

        // Recoil / spray control
        /// <summary>
        ///     Minimum angle that recoil can be.
        /// </summary>
        [DataField("minAngle")]
        public Angle MinAngle { get; set; }

        /// <summary>
        ///     Maximum angle that recoil can be.
        /// </summary>
        [DataField("maxAngle")]
        public Angle MaxAngle { get; set; } = 45.0f;

        public Angle CurrentAngle { get; set; } = Angle.Zero;

        /// <summary>
        ///     How slowly the angle's theta decays per second in radians
        /// </summary>
        [DataField("angleDecay")]
        public float AngleDecay { get; set; } = 20.0f;

        /// <summary>
        ///     How quickly the angle's theta builds for every shot fired in radians
        /// </summary>
        [DataField("angleIncrease")]
        public float AngleIncrease { get; set; } = 40.0f;

        /// <summary>
        ///     How much camera recoil there is.
        /// </summary>
        [DataField("recoilMultiplier")]
        protected float RecoilMultiplier { get; set; } = 1.1f;

        public MapCoordinates? FireCoordinates { get; set; }

        // Sounds
        [DataField("soundGunshot")]
        public string? SoundGunshot { get; private set; } = null;
        [DataField("soundRange")]
        public float SoundRange { get; }

        [DataField("soundEmpty")]
        public string? SoundEmpty { get; private set; } = "/Audio/Weapons/Guns/Empty/empty.ogg";

        void ISerializationHooks.AfterDeserialization()
        {
            DebugTools.Assert(AllSelectors.HasFlag(Selector));
            AngleDecay = AngleDecay * MathF.PI / 180f;
            AngleIncrease = AngleIncrease * (float) Math.PI / 180f;
            MaxAngle = Angle.FromDegrees(MaxAngle / 2f);
            MinAngle = Angle.FromDegrees(MinAngle / 2f);
        }

        void ISerializationHooks.BeforeSerialization()
        {
            AngleDecay = MathF.Round(AngleDecay / (MathF.PI / 180f), 2);
            AngleIncrease = MathF.Round(AngleIncrease / (MathF.PI / 180f), 2);
            MaxAngle = MaxAngle.Degrees * 2;
            MinAngle = MinAngle.Degrees * 2;
        }

        public IEntity? Shooter()
        {
            return !Owner.TryGetContainer(out var container) ? null : container.Owner;
        }

        public virtual bool CanFire()
        {
            if (FireRate <= 0.0f)
                return false;

            return Selector switch
            {
                FireRateSelector.Safety => false,
                FireRateSelector.Single => ShotCounter < 1,
                FireRateSelector.TripleBurst => ShotCounter < 3,
                FireRateSelector.Automatic => true,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        ///     Fire out the specified number of bullets if possible.
        ///     Client-side this will just play the specified number of sounds and a muzzle flash.
        ///     Server-side this will work out each bullet to spawn and fire them.
        /// </summary>
        public virtual bool TryShoot(Angle angle)
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

        void IHandSelected.HandSelected(HandSelectedEventArgs eventArgs)
        {
            ResetFire();
        }

        protected void ResetFire()
        {
            ShotCounter = 0;
            NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
        }

        public abstract Task<bool> InteractUsing(InteractUsingEventArgs eventArgs);

        public abstract bool UseEntity(UseEntityEventArgs eventArgs);
    }

    [Flags]
    public enum FireRateSelector : byte
    {
        // If you need more than 9 values then use ushort
        Safety = 0,
        Single = 1 << 0,
        TripleBurst = 1 << 1,
        Automatic = 1 << 2,
    }
}
