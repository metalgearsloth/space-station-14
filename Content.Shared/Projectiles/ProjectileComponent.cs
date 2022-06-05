using Content.Shared.Damage;
using Content.Shared.Sound;
using Robust.Shared.GameStates;

namespace Content.Shared.Projectiles
{
    [RegisterComponent, NetworkedComponent, Friend(typeof(SharedProjectileSystem))]
    public sealed class ProjectileComponent : Component
    {
        /// <summary>
        /// Entity to be ignored for collision
        /// </summary>
        public EntityUid? Shooter;

        [DataField("damage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = default!;

        [DataField("deleteOnCollide")]
        public bool DeleteOnCollide { get; } = true;

        // Get that juicy FPS hit sound
        [DataField("soundHit")] public SoundSpecifier? SoundHit;

        [DataField("soundForce")]
        public bool ForceSound = false;

        public bool DamagedEntity;
    }
}
