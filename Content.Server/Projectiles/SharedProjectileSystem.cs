using Content.Server.Administration.Logs;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Random;
using GunSystem = Content.Server.Weapon.Ranged.Systems.GunSystem;

namespace Content.Server.Projectiles
{
    [UsedImplicitly]
    public sealed class ProjectileSystem : SharedProjectileSystem
    {
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly CameraRecoilSystem _cameraRecoil = default!;
        [Dependency] private readonly GunSystem _guns = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnCollide);
        }

        private void OnCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
        {
            // This is so entities that shouldn't get a collision are ignored.
            if (args.OurFixture.ID != ProjectileFixture || !args.OtherFixture.Hard || component.DamagedEntity)
                return;

            // Handle ricochets, Can't do it in a separate system in case projectile deletes this
            if (TryRicochet(ref args))
                return;

            var otherEntity = args.OtherFixture.Body.Owner;

            var modifiedDamage = _damageableSystem.TryChangeDamage(otherEntity, component.Damage);
            component.DamagedEntity = true;

            if (modifiedDamage is not null && component.Shooter != null)
            {
                _adminLogger.Add(LogType.BulletHit,
                    HasComp<ActorComponent>(otherEntity) ? LogImpact.Extreme : LogImpact.High,
                    $"Projectile {ToPrettyString(component.Owner):projectile} shot by {ToPrettyString(component.Shooter.Value):user} hit {ToPrettyString(otherEntity):target} and dealt {modifiedDamage.Total:damage} damage");
            }

            _guns.PlayImpactSound(otherEntity, modifiedDamage, component.SoundHit, component.ForceSound);

            // Damaging it can delete it
            if (HasComp<CameraRecoilComponent>(otherEntity))
            {
                var direction = args.OurFixture.Body.LinearVelocity.Normalized;
                _cameraRecoil.KickCamera(otherEntity, direction);
            }

            if (component.DeleteOnCollide)
                QueueDel(uid);
        }

        private bool TryRicochet(ref StartCollideEvent args)
        {
            if (!TryComp<RicochetComponent>(args.OurFixture.Body.Owner, out var ricochet)) return false;

            // Stop the collision at all.
            if (ricochet.LastRicochet == args.OtherFixture.Body.Owner) return true;

            if (!_random.Prob(ricochet.Prob)) return false;

            // TODO: Sound
            ricochet.LastRicochet = args.OtherFixture.Body.Owner;
            Dirty(ricochet);
            Logger.DebugS("projectile", $"Ricochet!");

            var localNormal = args.Contact.Manifold.LocalNormal;

            var oldVelocity = args.OurFixture.Body.LinearVelocity;
            var velocity = localNormal.ToAngle().RotateVec(oldVelocity);

            Get<SharedPhysicsSystem>().SetLinearVelocity(args.OurFixture.Body, velocity);
            Transform(args.OurFixture.Body.Owner).LocalRotation = velocity.ToAngle();

            return true;
        }
    }
}
