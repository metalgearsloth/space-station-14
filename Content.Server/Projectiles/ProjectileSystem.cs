using Content.Server.Administration.Logs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Physics.Events;
using Content.Shared.Effects;
using Content.Shared.Physics;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    protected override void OnPreventCollide(EntityUid uid, ProjectileComponent component, ref PreventCollideEvent args)
    {
        base.OnPreventCollide(uid, component, ref args);

        if (args.Cancelled || args.OurFixture.ID != ProjectileFixture)
            return;

        if (TryComp<ProjectileTargetComponent>(uid, out var target) &&
            args.OtherEntity != target.Target &&
            (target.OriginalCollisionMask & args.OtherFixture.CollisionLayer) == 0x0 &&
            (target.OriginalCollisionLayer & args.OtherFixture.CollisionMask) == 0x0)
        {
            args.Cancelled = true;
        }
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixture.ID != ProjectileFixture || !args.OtherFixture.Hard || component.DamagedEntity)
            return;

        var ourLayer = (CollisionGroup) args.OurFixture.CollisionMask;
        var otheWeh = (CollisionGroup) args.OtherFixture.CollisionLayer;

        var otherEntity = args.OtherEntity;
        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(otherEntity, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(component, otherEntity);
            return;
        }

        var otherName = ToPrettyString(otherEntity);
        var direction = args.OurBody.LinearVelocity.Normalized();
        var modifiedDamage = _damageableSystem.TryChangeDamage(otherEntity, component.Damage, component.IgnoreResistances, origin: component.Shooter);
        var deleted = Deleted(otherEntity);

        if (modifiedDamage is not null && EntityManager.EntityExists(component.Shooter))
        {
            if (modifiedDamage.Total > FixedPoint2.Zero && !deleted)
            {
                RaiseNetworkEvent(new ColorFlashEffectEvent(Color.Red, new List<EntityUid> { otherEntity }), Filter.Pvs(otherEntity, entityManager: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                HasComp<ActorComponent>(otherEntity) ? LogImpact.Extreme : LogImpact.High,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter):user} hit {otherName:target} and dealt {modifiedDamage.Total:damage} damage");
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(otherEntity, modifiedDamage, component.SoundHit, component.ForceSound);
            _sharedCameraRecoil.KickCamera(otherEntity, direction);
        }

        var ev = new ProjectileCollideEvent(uid, false);
        RaiseLocalEvent(args.OtherEntity, ref ev);

        if (!ev.Cancelled)
        {
            component.DamagedEntity = true;

            if (component.DeleteOnCollide)
            {
                var otherMask = (CollisionGroup) args.OtherFixture.CollisionMask;
                var otherLayer = (CollisionGroup) args.OtherFixture.CollisionLayer;
                QueueDel(uid);
            }

            if (component.ImpactEffect != null && TryComp<TransformComponent>(uid, out var xform))
            {
                RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, xform.Coordinates), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
            }
        }
    }
}
