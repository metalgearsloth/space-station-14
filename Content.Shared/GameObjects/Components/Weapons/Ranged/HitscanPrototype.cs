#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared.Damage;
using Content.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged
{
    [Prototype("hitscan")]
    public sealed class HitscanPrototype : IPrototype
    {
        [DataField("id")]
        public string ID { get; private set; } = default!;

        // Muzzle -> Travel -> Impact for hitscans forms the full laser.
        // Muzzle is declared elsewhere

        /// <summary>
        ///     Overrides the weapon's muzzle-flash if it uses hitscan.
        /// </summary>
        [DataField("muzzleEffect")]
        public string? MuzzleEffect { get; private set; } = "";
        [DataField("travelEffect")]
        public string? TravelEffect { get; private set; } = "Objects/Weapons/Guns/Projectiles/laser.png";
        [DataField("impactEffect")]
        public string? ImpactEffect { get; private set; }

        [DataField("collisionMask")]
        public CollisionGroup CollisionMask { get; private set; } = CollisionGroup.None;

        [DataField("damage")]
        public float Damage { get; private set; } = 10.0f;

        [DataField("damageType")]
        public DamageType DamageType { get; private set; } = DamageType.Heat;

        [DataField("maxLength")]
        public float MaxLength { get; private set; } = 20.0f;

        // Sounds
        [DataField("soundHitWall")]
        public string? SoundHitWall { get; private set; } = "/Audio/Weapons/Guns/Hits/laser_sear_wall.ogg";
    }
}
