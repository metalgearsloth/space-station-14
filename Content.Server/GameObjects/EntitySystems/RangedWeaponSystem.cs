#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Projectiles;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
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

        [Dependency] private readonly IPhysicsManager _physicsManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        private EffectSystem _effectSystem = default!;
        private SharedBroadPhaseSystem _broadphase = default!;

        private List<(ShootMessage Message, IEntity Shooter)> _shootMessages = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<ShootMessage>(HandleStartMessage);

            _effectSystem = Get<EffectSystem>();
        }

        public override void Shutdown()
        {
            base.Shutdown();
            UnsubscribeNetworkEvent<ShootMessage>();
        }

        private void HandleStartMessage(ShootMessage message, EntitySessionEventArgs args)
        {
            var entity = EntityManager.GetEntity(message.Uid);
            var weapon = entity.GetComponent<SharedRangedWeaponComponent>();
            // TODO: Need to TryGet

            if (entity.Deleted)
            {
                // TODO: Clear list
                return;
            }

            var shooter = weapon.Shooter();
            if (shooter == null || shooter != args.SenderSession.AttachedEntity)
            {
                // Cheater / lagger
                return;
            }

            _shootMessages.Add((message, shooter));
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTime = _gameTiming.CurTime;

            foreach (var (message, shooter) in _shootMessages.ToArray())
            {
                if (!Pew(message, shooter, currentTime)) continue;
                DebugTools.Assert(_shootMessages.Contains((message, shooter)));
                _shootMessages.Remove((message, shooter));
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>true if the message was handled; false if it needs to be re-run next tick</returns>
        private bool Pew(ShootMessage message, IEntity shooter, TimeSpan currentTime)
        {
            var gun = EntityManager.GetEntity(message.Uid).GetComponent<SharedRangedWeaponComponent>();

            if (shooter != gun.Shooter())
            {
                return true;
            }

            if (!TryFire(shooter, gun, message.FireCoordinates, out var firedShots, currentTime))
            {
                // TryFire hasn't handled the message yet so we'll run it next tick.
                return true;
            }

            if (firedShots < message.Shots)
            {
                // TODO: Dirty af
                message.Shots -= firedShots;
                return false;
            }

            if (firedShots > message.Shots)
            {
                Logger.ErrorS("guns", $"Fired more shots on server than client for entity {message.Uid} from {shooter}!");
                return true;
            }

            return true;
        }

        #region Shoot
        public override void ShootHitscan(IEntity? user, IGun weapon, HitscanPrototype hitscan, Angle angle, float damageRatio = 1, float alphaRatio = 1)
        {
            var currentTime = _gameTiming.CurTime;
            var ray = new CollisionRay(weapon.Owner.Transform.MapPosition.Position, angle.ToVec(), (int) hitscan.CollisionMask);
            var rayCastResults = _broadphase.IntersectRay(weapon.Owner.Transform.MapID, ray, hitscan.MaxLength, user, false).ToArray();
            var distance = hitscan.MaxLength;

            if (rayCastResults.Length >= 1)
            {
                var result = rayCastResults[0];
                distance = result.HitEntity != null ? result.Distance : hitscan.MaxLength;

                if (result.HitEntity == null || !result.HitEntity.TryGetComponent(out IDamageableComponent? damageable))
                    return;

                damageable.ChangeDamage(hitscan.DamageType, (int) Math.Round(hitscan.Damage, MidpointRounding.AwayFromZero), false, user);
                //I used Math.Round over Convert.toInt32, as toInt32 always rounds to
                //even numbers if halfway between two numbers, rather than rounding to nearest
            }

            // Fire effects
            HitscanMuzzleFlash(user, weapon, hitscan.MuzzleEffect, angle, distance, currentTime, alphaRatio);
            TravelFlash(user, weapon.Owner, hitscan, angle, distance, currentTime, alphaRatio);
            ImpactFlash(user, weapon.Owner, hitscan, angle, distance, currentTime, alphaRatio);
        }

        public override void ShootAmmo(IEntity? user, IGun weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            if (!ammoComponent.CanFire())
                return;

            List<Angle>? sprayAngleChange = null;
            var count = ammoComponent.ProjectilesFired;
            var evenSpreadAngle = ammoComponent.EvenSpreadAngle;
            var spreadRatio = weapon.AmmoSpreadRatio;

            if (ammoComponent.AmmoIsProjectile)
            {
                ShootProjectile(user, weapon, angle, ammoComponent.Owner.GetComponent<SharedProjectileComponent>(), ammoComponent.Velocity);
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

                if (_prototypeManager.TryIndex(ammoComponent.ProjectileId, out HitscanPrototype? hitscan))
                {
                    ShootHitscan(user, weapon, hitscan, angle);
                }
                else
                {
                    ShootProjectile(user, weapon, projectileAngle, projectile.GetComponent<SharedProjectileComponent>(), ammoComponent.Velocity);
                }
            }
        }

        public override void ShootProjectile(IEntity? user, IGun weapon, Angle angle, SharedProjectileComponent projectileComponent, float velocity)
        {
            var physicsComponent = projectileComponent.Owner.GetComponent<IPhysBody>();
            physicsComponent.BodyStatus = BodyStatus.InAir;

            if (user != null)
                projectileComponent.IgnoreEntity(user);

            physicsComponent
                .LinearVelocity = angle.ToVec() * velocity;

            projectileComponent.Owner.Transform.LocalRotation = angle.Theta;
        }

        protected override Filter GetFilter(IGun gun)
        {
            var filter = Filter.Pvs(gun.Owner);
            var shooter = gun.Shooter().PlayerSession();
            if (shooter != null)
            {
                filter.RemovePlayer(shooter);
            }

            return filter;
        }

        protected override bool TryShootMagazine(IMagazineGun weapon, Angle angle)
        {
            var chambered = weapon.Chambered;

            if (weapon.AutoCycle)
                Cycle(weapon);

            var shooter = weapon.Shooter();

            if (chambered == null)
            {
                // TODO: Bolt / pump do nothing?
                if (weapon.SoundEmpty != null)
                    SoundSystem.Play(
                        GetFilter(weapon),
                        weapon.SoundEmpty,
                        weapon.Owner,
                        AudioHelpers.WithVariation(IGun.EmptyVariation).WithVolume(IGun.EmptyVolume));

                var mag = weapon.Magazine;

                if (!weapon.BoltOpen && (mag == null || mag.ShotsLeft == 0))
                    TrySetBolt(weapon, true);

                return true;
            }

            var sound = chambered.Spent ? weapon.SoundEmpty : weapon.SoundGunshot;

            if (sound != null)
                Get<AudioSystem>().Play(
                    GetFilter(weapon),
                    sound,
                    weapon.Owner,
                    AudioHelpers.WithVariation(IGun.GunshotVariation).WithVolume(IGun.GunshotVolume));

            // TODO: Needs to handle hitscan
            if (!chambered.Spent)
            {
                ShootAmmo(shooter, weapon, angle, chambered);
                MuzzleFlash(shooter, weapon, angle);
                chambered.Spent = true;
            }

            return true;
        }

        protected override bool TryShootRevolver(IRevolver revolver, Angle angle)
        {
            throw new NotImplementedException();
        }

        protected override bool TryShootBattery(IBatteryGun weapon, Angle angle)
        {
            // TODO: Shared PowerCell
            var battery = weapon.Battery;
            if (battery == null)
                return false;

            if (battery.CurrentCharge < weapon.LowerChargeLimit)
                return false;

            // Can fire confirmed
            // Multiply the entity's damage / whatever by the percentage of charge the shot has.
            var chargeChange = Math.Min(battery.CurrentCharge, weapon.BaseFireCost);
            battery.UseCharge(chargeChange);

            var shooter = weapon.Shooter();
            var energyRatio = chargeChange / weapon.BaseFireCost;

            if (weapon.AmmoIsHitscan)
            {
                var prototype = _prototypeManager.Index<HitscanPrototype>(weapon.AmmoPrototype);
                ShootHitscan(shooter, weapon, prototype, angle, energyRatio, energyRatio);
            }
            else
            {
                var entity = EntityManager.SpawnEntity(weapon.AmmoPrototype, weapon.Owner.Transform.MapPosition);
                var ammoComponent = entity.GetComponent<SharedAmmoComponent>();
                var projectileComponent = entity.GetComponent<ProjectileComponent>();

                if (energyRatio < 1.0)
                {
                    var newDamages = new Dictionary<DamageType, int>(projectileComponent.Damages.Count);
                    foreach (var (damageType, damage) in projectileComponent.Damages)
                    {
                        newDamages.Add(damageType, (int) (damage * energyRatio));
                    }

                    projectileComponent.Damages = newDamages;
                }

                ShootAmmo(shooter, weapon, angle, ammoComponent);
                MuzzleFlash(shooter, weapon, angle, alphaRatio: energyRatio);
            }

            return true;
        }
        #endregion

        private List<Angle> Linspace(double start, double end, int intervals)
        {
            var linspace = new List<Angle>(intervals);

            for (var i = 0; i <= intervals - 1; i++)
            {
                linspace.Add(Angle.FromDegrees(start + (end - start) * i / (intervals - 1)));
            }
            return linspace;
        }

        #region Effects
        public override void MuzzleFlash(IEntity? user, IGun weapon, Angle angle, TimeSpan? currentTime = null, bool predicted = true, float alphaRatio = 1.0f)
        {
            var texture = weapon.MuzzleFlash;
            if (texture == null)
                return;

            currentTime ??= _gameTiming.CurTime;
            var offset = angle.ToVec().Normalized / 2;
            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = currentTime.Value,
                DeathTime = _gameTiming.CurTime + TimeSpan.FromSeconds(EffectDuration),
                AttachedEntityUid = weapon.Owner.Uid,
                AttachedOffset = offset,
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), alphaRatio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message, predicted ? user?.PlayerSession() : null);
        }

        private void HitscanMuzzleFlash(IEntity? user, IGun weapon, string? texture, Angle angle, float distance, TimeSpan? currentTime = null, float alphaRatio = 1.0f)
        {
            if (texture == null || distance <= 1.0f)
                return;

            currentTime ??= _gameTiming.CurTime;
            var parent = user ?? weapon.Owner;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = currentTime.Value,
                DeathTime = _gameTiming.CurTime + TimeSpan.FromSeconds(EffectDuration),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec().Normalized * 0.5f),
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), alphaRatio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }

        private void TravelFlash(IEntity? user, IEntity weapon, HitscanPrototype hitscan, Angle angle, float distance, TimeSpan? currentTime = null, float alphaRatio = 1.0f)
        {
            if (hitscan.TravelEffect == null || distance <= 1.5f)
                return;

            currentTime ??= _gameTiming.CurTime;
            var parent = user ?? weapon;
            const float offset = 0.5f;

            var message = new EffectSystemMessage
            {
                EffectSprite = hitscan.TravelEffect,
                Born = _gameTiming.CurTime,
                DeathTime = currentTime.Value + TimeSpan.FromSeconds(EffectDuration),
                Size = new Vector2(distance - offset , 1f),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec() * (distance + offset) / 2),
                //Rotated from east facing
                Rotation = (float) angle.FlipPositive(),
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), alphaRatio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }

        private void ImpactFlash(IEntity? user, IEntity weapon, HitscanPrototype hitscan, Angle angle, float distance, TimeSpan? currentTime = null, float alphaRatio = 1.0f)
        {
            if (hitscan.ImpactEffect == null)
                return;

            currentTime ??= _gameTiming.CurTime;
            var parent = user ?? weapon;

            var message = new EffectSystemMessage
            {
                EffectSprite = hitscan.ImpactEffect,
                Born = _gameTiming.CurTime,
                DeathTime = currentTime.Value + TimeSpan.FromSeconds(EffectDuration),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec().Normalized * distance),
                //Rotated from east facing
                Rotation = (float) angle.FlipPositive(),
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), alphaRatio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }
        #endregion

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null)
        {
            // TODO: Copy latest from master
            ejectDirections ??= new[] {Direction.East, Direction.North, Direction.South, Direction.West};

            const float ejectOffset = 0.2f;

            var ammo = casing.GetComponent<SharedAmmoComponent>();
            var offsetPos = (_robustRandom.NextFloat() * ejectOffset, _robustRandom.NextFloat() * ejectOffset);

            // Need to deparent it if applicable
            if (user != null && casing.Transform.ParentUid == user.Uid && user.Transform.Parent != null)
            {
                casing.Transform.Coordinates = user.Transform.Coordinates.Offset(offsetPos);
            }
            else
            {
                casing.Transform.Coordinates = casing.Transform.Coordinates.Offset(offsetPos);
            }

            casing.Transform.LocalRotation = _robustRandom.Pick(ejectDirections).ToAngle();

            if (ammo.SoundCollectionEject == null || !playSound)
                return;

            var soundCollection = _prototypeManager.Index<SoundCollectionPrototype>(ammo.SoundCollectionEject);
            var randomFile = _robustRandom.Pick(soundCollection.PickFiles);
            // Don't use excluded til cartridges predicted

            SoundSystem.Play(Filter.Pvs(new MapCoordinates(casing.Transform.WorldPosition, casing.Transform.MapID)), randomFile, casing, AudioHelpers.WithVariation(0.2f, _robustRandom).WithVolume(-1));
        }
    }
}
