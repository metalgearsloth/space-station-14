#nullable enable
using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Projectiles;
using Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal sealed class RangedWeaponSystem : SharedRangedWeaponSystem
    {
        // Handles fire positions etc. from clients
        // It'd be cleaner to have this under corresponding Client / Server components buuuttt the issue with that is
        // you wouldn't be able to inherit from "SharedBlankWeapon" and would instead need to make
        // discrete server and client versions of each weapon that don't inherit from shared.
        
        // e.g. SharedRangedWeapon -> ServerRevolver and SharedRangedWeapon -> ClientRevolver
        // (Handles syncing via component)
        // vs.
        // SharedRangedWeapon -> SharedRevolver -> ServerRevolver
        // (needs to sync via system or component spaghetti)

        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<RangedCoordinatesMessage>(HandleRangedAngleMessage);
            SubscribeNetworkEvent<StartFiringMessage>(HandleStartFiringMessage);
            SubscribeNetworkEvent<StopFiringMessage>(HandleStopFiringMessage);
        }

        private void HandleRangedAngleMessage(RangedCoordinatesMessage message, EntitySessionEventArgs args)
        {
            var entity = _entityManager.GetEntity(message.Uid);
            var weapon = entity.GetComponent<SharedRangedWeaponComponent>();
            var shooter = weapon.Shooter();

            if (shooter != args.SenderSession.AttachedEntity)
            {
                // Cheater / lagger
                return;
            }

            weapon.FireCoordinates = message.Coordinates;
        }
        
        private void HandleStartFiringMessage(StartFiringMessage message, EntitySessionEventArgs args)
        {
            if (message.FireCoordinates == null)
            {
                return;
            }

            var entity = _entityManager.GetEntity(message.Uid);
            var weapon = entity.GetComponent<SharedRangedWeaponComponent>();
            var shooter = weapon.Shooter();

            if (shooter != args.SenderSession.AttachedEntity)
            {
                // Cheater / lagger
                return;
            }

            weapon.Firing = true;
            weapon.FireCoordinates = message.FireCoordinates;
            weapon.ShotCounter = 0;
        }
        
        private void HandleStopFiringMessage(StopFiringMessage message, EntitySessionEventArgs args)
        {
            var entity = _entityManager.GetEntity(message.Uid);
            var weapon = entity.GetComponent<SharedRangedWeaponComponent>();
            var shooter = weapon.Shooter();

            if (shooter != args.SenderSession.AttachedEntity)
            {
                // Cheater / lagger
                return;
            }
            
            weapon.Firing = false;
            weapon.ExpectedShots += message.Shots;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            
            foreach (var comp in ComponentManager.EntityQuery<SharedRangedWeaponComponent>())
            {
                Update(comp, currentTime);
            }
        }

        private void Update(SharedRangedWeaponComponent weaponComponent, TimeSpan currentTime)
        {
            if (weaponComponent.FireCoordinates == null || (!weaponComponent.Firing && weaponComponent.ExpectedShots == 0))
                return;

            var shooter = weaponComponent.Shooter();
            if (shooter == null)
                return;
            
            if (!weaponComponent.TryFire(currentTime, shooter, weaponComponent.FireCoordinates!.Value) || (!weaponComponent.Firing && weaponComponent.ExpectedShots <= weaponComponent.AccumulatedShots))
            {
                weaponComponent.ExpectedShots -= weaponComponent.ExpectedShots;
                weaponComponent.AccumulatedShots = 0;

                if (weaponComponent.ExpectedShots > 0)
                {
                    Logger.Warning("Desync shots fired");
                    weaponComponent.ExpectedShots = 0;
                }
            }
        }

        public override void ShootHitscan(IEntity? user, HitscanPrototype hitscan, Angle angle, float damageRatio = 1, float alphaRatio = 1)
        {
            throw new NotImplementedException();
            // TODO: Travel and thingo effect
            // Maybe shooting should also do muzzle flashes
        }

        public override void ShootAmmo(IEntity? user, Angle angle, SharedAmmoComponent ammoComponent, float spreadRatio = 1.0f)
        {
            // BIG FAT TODO: NEED TO GET RECOIL HERE
            if (!ammoComponent.CanFire())
                return;

            List<Angle>? sprayAngleChange = null;
            var count = ammoComponent.ProjectilesFired;
            var evenSpreadAngle = ammoComponent.EvenSpreadAngle;

            if (ammoComponent.AmmoIsProjectile)
            {
                ShootProjectile(user,  angle, ammoComponent.Owner.GetComponent<SharedProjectileComponent>(), ammoComponent.Velocity);
                return;
            }

            if (count > 1)
            {
                evenSpreadAngle *= spreadRatio;
                sprayAngleChange = Linspace(-evenSpreadAngle / 2, evenSpreadAngle / 2, count);
            }

            for (var i = 0; i < count; i++)
            {
                var projectile =
                        EntityManager.SpawnEntity(ammoComponent.ProjectileId, ammoComponent.Owner.Transform.MapPosition);
                
                Angle projectileAngle;

                if (sprayAngleChange != null)
                {
                    projectileAngle = angle + sprayAngleChange[i];
                }
                else
                {
                    projectileAngle = angle;
                }
                
                var hitscan = IoCManager.Resolve<IPrototypeManager>().Index<HitscanPrototype>(ammoComponent.ProjectileId);

                if (hitscan != null)
                {
                    ShootHitscan(user, hitscan, angle);
                }
                else
                {
                    // TODO: Go User -> Projectile -> Angle
                    ShootProjectile(user, projectileAngle, projectile.GetComponent<SharedProjectileComponent>(), ammoComponent.Velocity);
                }
            }
        }

        public override void ShootProjectile(IEntity? user, Angle angle, SharedProjectileComponent projectileComponent, float velocity)
        {
            var collidableComponent = projectileComponent.Owner.GetComponent<ICollidableComponent>();
            collidableComponent.Status = BodyStatus.InAir;

            if (user != null)
                projectileComponent.IgnoreEntity(user);

            collidableComponent
                .EnsureController<BulletController>()
                .LinearVelocity = angle.ToVec() * velocity;

            projectileComponent.Owner.Transform.LocalRotation = angle.Theta;
        }

        private List<Angle> Linspace(double start, double end, int intervals)
        {
            DebugTools.Assert(intervals > 1);

            var linspace = new List<Angle>(intervals);

            for (var i = 0; i <= intervals - 1; i++)
            {
                linspace.Add(Angle.FromDegrees(start + (end - start) * i / (intervals - 1)));
            }
            return linspace;
        }

        public override void MuzzleFlash(IEntity? user, IEntity weapon, string texture, Angle angle)
        {
            IActorComponent? actorComponent = null;
            user?.TryGetComponent(out actorComponent);
            
            var offset = angle.ToVec().Normalized / 2;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = _gameTiming.CurTime,
                DeathTime = _gameTiming.CurTime + TimeSpan.FromSeconds(0.2),
                AttachedEntityUid = weapon.Uid,
                AttachedOffset = offset,
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), Vector4.One),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };
            
            Get<EffectSystem>().CreateParticle(message, actorComponent?.playerSession);
        }

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null)
        {
            ejectDirections ??= new[] {Direction.East, Direction.North, Direction.South, Direction.West};

            const float ejectOffset = 0.2f;
            
            var ammo = casing.GetComponent<SharedAmmoComponent>();
            var offsetPos = (_robustRandom.NextFloat() * ejectOffset, _robustRandom.NextFloat() * ejectOffset);
            casing.Transform.Coordinates = casing.Transform.Coordinates.Offset(offsetPos);
            casing.Transform.LocalRotation = _robustRandom.Pick(ejectDirections).ToAngle();

            if (ammo.SoundCollectionEject == null || !playSound)
            {
                return;
            }

            var soundCollection = _prototypeManager.Index<SoundCollectionPrototype>(ammo.SoundCollectionEject);
            var randomFile = _robustRandom.Pick(soundCollection.PickFiles);
            // Don't use excluded til cartridges predicted

            Get<AudioSystem>().PlayFromEntity(randomFile, casing, AudioHelpers.WithVariation(0.2f, _robustRandom).WithVolume(-1));
        }
    }
}