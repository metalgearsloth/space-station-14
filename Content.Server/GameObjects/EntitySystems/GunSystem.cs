using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class GunSystem : SharedGunSystem
    {
        private EffectSystem _effectSystem = default!;
        private SharedBroadPhaseSystem _broadphase = default!;

        private const float BoltVariation = 0.01f;
        private const float BoltVolume = 0.0f;

        private const float MagEjectOffset = 0.5f;

        private static Direction[] _ejectDirections = {Direction.East, Direction.North, Direction.NorthWest, Direction.South, Direction.SouthEast, Direction.West};

        public override void Initialize()
        {
            base.Initialize();
            _broadphase = Get<SharedBroadPhaseSystem>();
            _effectSystem = Get<EffectSystem>();
            SubscribeNetworkEvent<ShootMessage>(HandleShoot);
            SubscribeLocalEvent<SharedChamberedGunComponent, UseInHandEvent>(HandleUse);
            SubscribeLocalEvent<SharedGunComponent, InteractUsingEvent>(HandleInteractUsing);
            SubscribeLocalEvent<SharedChamberedGunComponent, InteractUsingEvent>(HandleChamberedInteractUsing);
        }

        private void HandleChamberedInteractUsing(EntityUid uid, SharedChamberedGunComponent component, InteractUsingEvent args)
        {
            if (!args.Used.TryGetComponent(out SharedAmmoComponent? ammoComponent)) return;

            if (component.Chamber.ContainedEntity != null)
            {
                // TODO: Chamber is full
                return;
            }

            if (component.BoltClosed)
            {
                // TODO: Need to open bolt
                return;
            }

            component.Chamber.Insert(ammoComponent.Owner);
            component.UpdateAppearance();

            // TODO: Sound for dis
        }

        private void HandleInteractUsing(EntityUid uid, SharedGunComponent component, InteractUsingEvent args)
        {
            if (args.Used.TryGetComponent(out SharedAmmoProviderComponent? ammoProviderComponent))
            {
                TryInsertMagazine(component, ammoProviderComponent);
                return;
            }

            // TODO: Attachments
        }

        private void HandleUse(EntityUid uid, SharedChamberedGunComponent component, UseInHandEvent args)
        {
            var mag = component.Magazine;

            // TODO: predict someday
            if (mag == null || !component.BoltClosed && mag.AmmoCount > 0)
            {
                ToggleBolt(component);
                return;
            }

            Cycle(component, args.User, true);

            if (Chambered(component) || mag.AmmoCount != 0) return;

            EjectMagazine(component);
        }

        protected override void EjectMagazine(SharedGunComponent component)
        {
            if (component.InternalMagazine) return;

            var mag = component.Magazine;
            if (mag == null) return;

            component.MagazineSlot.Remove(mag.Owner);

            var offsetPos = ((RobustRandom.NextFloat() - 0.5f) * MagEjectOffset, (RobustRandom.NextFloat() - 0.5f) * MagEjectOffset);
            var transform = mag.Owner.Transform;

            transform.Coordinates = transform.Coordinates.Offset(offsetPos);
            transform.LocalRotation = RobustRandom.Pick(_ejectDirections).ToAngle();
            component.UpdateAppearance();

            var sound = component.SoundMagEject;

            if (!string.IsNullOrEmpty(sound))
                SoundSystem.Play(Filter.Pvs(component.Owner), sound);
        }

        private bool TryInsertMagazine(SharedGunComponent component, SharedAmmoProviderComponent mag)
        {
            if (component.InternalMagazine) return false;

            var existing = component.Magazine;

            if (existing != null)
            {
                // TODO: Popup message that one already there
                return false;
            }

            if ((component.MagazineTypes & mag.MagazineType) == 0)
            {
                // TODO: Popup wrong caliber
                return false;
            }

            component.MagazineSlot.Insert(mag.Owner);
            component.UpdateAppearance();

            var sound = component.SoundMagInsert;

            if (!string.IsNullOrEmpty(sound))
            {
                SoundSystem.Play(Filter.Pvs(component.Owner), sound);
            }

            return true;
        }

        private bool Chambered(SharedChamberedGunComponent component)
        {
            return component.Chamber.ContainedEntity != null;
        }

        protected override void Cycle(SharedChamberedGunComponent component, IEntity? user = null, bool manual = false)
        {
            if (component.TryPopChamber(out var ammo))
            {
                EjectCasing(user, ammo.Owner);
            }

            component.TryFeedChamber();

            var mag = component.Magazine;

            if (mag is {AmmoCount: 0} && component.Chamber.ContainedEntity == null)
            {
                ToggleBolt(component);

                if (component.AutoEjectOnEmpty)
                    EjectMagazine(component);
            }

            if (manual)
            {
                var cycle = component.SoundCycle;

                if (!string.IsNullOrEmpty(cycle))
                {
                    SoundSystem.Play(Filter.Pvs(component.Owner), cycle);
                }
            }
        }

        protected override void ToggleBolt(SharedChamberedGunComponent component)
        {
            var bolt = component.BoltClosed;

            component.BoltClosed ^= true;
            component.Dirty();
            component.UpdateAppearance();
            string? sound;

            if (bolt)
            {
                sound = component.SoundBoltOpen;
            }
            else
            {
                sound = component.SoundBoltClosed;
            }

            if (!string.IsNullOrEmpty(sound))
            {
                SoundSystem.Play(Filter.Pvs(component.Owner), sound, AudioHelpers.WithVariation(BoltVariation).WithVolume(BoltVolume));
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_shootQueue.Count == 0) return;

            var currentTime = GameTiming.CurTime;

            for (var i = _shootQueue.Count - 1; i >= 0; i--)
            {
                var (msg, user) = _shootQueue[i];
                if (HandleShoot(user, msg, currentTime))
                {
                    _shootQueue.RemoveAt(i);
                }
            }
        }

        private void ResetShoot(SharedGunComponent gunComponent, TimeSpan currentTime)
        {
            gunComponent.ShotCounter = 0;
            gunComponent.NextFire = TimeSpan.FromSeconds(Math.Max(gunComponent.NextFire.TotalSeconds, currentTime.TotalSeconds));
        }

        /// <summary>
        /// Try to pew pew the gun.
        /// </summary>
        /// <returns>true if the message has been handled regardless of whether it fired</returns>
        private bool HandleShoot(IEntity user, ShootMessage message, TimeSpan currentTime)
        {
            if (!EntityManager.TryGetEntity(message.Gun, out var gun) ||
                !gun.TryGetComponent(out SharedGunComponent? gunComponent))
            {
                return true;
            }

            var diff = (message.Time - currentTime).TotalSeconds;

            if (diff > 0)
            {
                if (diff > 5)
                {
                    Logger.WarningS("gun", $"Found a ShootMessage for {message.Gun} that was significantly ahead?");
                    ResetShoot(gunComponent, currentTime);
                    return true;
                }

                return false;
            }

            // TODO: Need to copy a lot of this shit from client to play sounds and stuff.
            if (!TryFire(user, gunComponent, message.Coordinates, out var shots, currentTime))
            {
                ResetShoot(gunComponent, currentTime);
                return true;
            }

            message.Shots -= shots;

            if (shots > 0)
            {
                Logger.DebugS("gun", $"Shot gun {gunComponent.Owner} {shots} times at {currentTime}");
            }

            if (message.Shots < 0)
            {
                // Uh ohh
                Logger.ErrorS("gun", $"Fired too many shots for gun: {message.Gun} Expected {message.Shots + shots} but got {shots}");
                ResetShoot(gunComponent, currentTime);
                return true;
            }

            // Keep firing
            if (message.Shots > 0)
            {
                return false;
            }

            ResetShoot(gunComponent, currentTime);
            return true;
        }

        private List<(ShootMessage Message, IEntity user)> _shootQueue = new();

        private void HandleShoot(ShootMessage shootMessage, EntitySessionEventArgs session)
        {
            if (shootMessage.Shots == 0)
            {
                Logger.ErrorS("gun", $"Received 0 shots message from {session.SenderSession}");
                return;
            }

            if (!EntityManager.TryGetEntity(shootMessage.Gun, out var gun))
            {
                return;
            }

            var user = session.SenderSession.AttachedEntity;

            if (!gun.TryGetContainerMan(out var manager) || manager.Owner != user)
            {
                return;
            }

            if (_shootQueue.Count == 0)
            {
                _shootQueue.Add((shootMessage, user));
                return;
            }

            for (var i = 0; i < _shootQueue.Count; i++)
            {
                var (msg, _) = _shootQueue[i];
                if (msg.Time > shootMessage.Time)
                {
                    _shootQueue.Insert(i, (shootMessage, user));
                    return;
                }
            }

            _shootQueue.Add((shootMessage, user));
        }

        public override void MuzzleFlash(IEntity? user, SharedGunComponent weapon, Angle angle, TimeSpan currentTime, bool predicted = false)
        {
            if (weapon.MuzzleFlash == null) return;

            // TODO: These get dropped in transit like a motherfucker at high ping.
            var deathTime = currentTime + TimeSpan.FromMilliseconds(200);
            // Offset the sprite so it actually looks like it's coming from the gun
            var offset = angle.ToVec().Normalized / 2;

            var message = new EffectSystemMessage
            {
                EffectSprite = weapon.MuzzleFlash,
                Born = currentTime,
                DeathTime = deathTime,
                AttachedEntityUid = weapon.Owner.Uid,
                AttachedOffset = offset,
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 255), 1.0f),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message, predicted ? user.PlayerSession() : null);
        }

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true)
        {
            const float ejectOffset = 1.8f;
            var ammo = casing.GetComponent<SharedAmmoComponent>();
            var offsetPos = ((RobustRandom.NextFloat() - 0.5f) * ejectOffset, (RobustRandom.NextFloat() - 0.5f) * ejectOffset);
            casing.Transform.Coordinates = casing.Transform.Coordinates.Offset(offsetPos);
            casing.Transform.LocalRotation = RobustRandom.Pick(_ejectDirections).ToAngle();

            if (ammo.SoundCollectionEject == null || !playSound)
            {
                return;
            }

            var soundCollection = PrototypeManager.Index<SoundCollectionPrototype>(ammo.SoundCollectionEject);
            var randomFile = RobustRandom.Pick(soundCollection.PickFiles);
            SoundSystem.Play(Filter.Pvs(casing), randomFile, casing.Transform.Coordinates, AudioParams.Default.WithVolume(-1));
        }

        public override void ShootHitscan(IEntity? user, IGun weapon, HitscanPrototype hitscan, Angle angle,
            float damageRatio = 1, float alphaRatio = 1)
        {
            var currentTime = GameTiming.CurTime;
            var ray = new CollisionRay(weapon.Owner.Transform.MapPosition.Position, angle.ToVec(), (int) hitscan.CollisionMask);
            var rayCastResults = _broadphase.IntersectRay(weapon.Owner.Transform.MapID, ray, hitscan.MaxLength, user, false).ToArray();
            var distance = hitscan.MaxLength;

            if (rayCastResults.Length >= 1)
            {
                var result = rayCastResults[0];
                distance = result.Distance;

                if (!result.HitEntity.TryGetComponent(out IDamageableComponent? damageable))
                    return;

                damageable.ChangeDamage(hitscan.DamageType, (int) Math.Round(hitscan.Damage, MidpointRounding.AwayFromZero), false, user);
                //I used Math.Round over Convert.toInt32, as toInt32 always rounds to
                //even numbers if halfway between two numbers, rather than rounding to nearest
            }

            // Fire effects
            HitscanMuzzleFlash(user, weapon.Owner, hitscan.MuzzleEffect, angle, distance, currentTime, alphaRatio);
            TravelFlash(user, weapon.Owner, hitscan.TravelEffect, angle, distance, currentTime, alphaRatio);
            ImpactFlash(user, weapon.Owner, hitscan.ImpactEffect, angle, distance, currentTime, alphaRatio);
        }

        // I'm not too worried about putting these in shared as the effect sucks anyway and would be better as a shader
        // TODO: Find a laser shader to use and apply that; also need a particle system re-write
        private void HitscanMuzzleFlash(IEntity? user, IEntity weapon, string? texture, Angle angle, float distance, TimeSpan? currentTime = null, float ratio = 1.0f)
        {
            if (texture == null || distance <= 1.0f)
                return;

            currentTime ??= GameTiming.CurTime;
            var parent = user ?? weapon;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = currentTime.Value,
                DeathTime = GameTiming.CurTime + TimeSpan.FromSeconds(EffectDuration),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec().Normalized * 0.5f),
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), ratio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }

        private void TravelFlash(IEntity? user, IEntity weapon, string? texture, Angle angle, float distance, TimeSpan? currentTime = null, float ratio = 1.0f)
        {
            if (texture == null || distance <= 1.5f)
                return;

            currentTime ??= GameTiming.CurTime;
            var parent = user ?? weapon;
            const float offset = 0.5f;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = GameTiming.CurTime,
                DeathTime = currentTime.Value + TimeSpan.FromSeconds(EffectDuration),
                Size = new Vector2(distance - offset , 1f),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec() * (distance + offset) / 2),
                //Rotated from east facing
                Rotation = (float) angle.FlipPositive(),
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), ratio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }

        private void ImpactFlash(IEntity? user, IEntity weapon, string? texture, Angle angle, float distance, TimeSpan? currentTime = null, float ratio = 1.0f)
        {
            if (texture == null)
                return;

            currentTime ??= GameTiming.CurTime;
            var parent = user ?? weapon;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = GameTiming.CurTime,
                DeathTime = currentTime.Value + TimeSpan.FromSeconds(EffectDuration),
                Coordinates = parent.Transform.Coordinates.Offset(angle.ToVec().Normalized * distance),
                //Rotated from east facing
                Rotation = (float) angle.FlipPositive(),
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), ratio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
        }

        public override void ShootAmmo(IEntity? user, IGun weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            // This is kinda weird but essentially say we have a battery it could just store the hitscan prototypes directly
            // and we can just call ShootHitscan directly. Alternatively we could have ammo that fires hitscan bullets
            // which we also need to handle it. It seemed like the easiest way to do it.

            if (ammoComponent.Spent) return;

            if (ammoComponent.IsHitscan(PrototypeManager))
            {
                var hitscan = PrototypeManager.Index<HitscanPrototype>(ammoComponent.ProjectileId);
                ShootHitscan(user, weapon, hitscan, angle);
            }
            else
            {
                var projectile = EntityManager.SpawnEntity(ammoComponent.ProjectileId, weapon.Owner.Transform.MapPosition).GetComponent<SharedProjectileComponent>();
                ShootProjectile(user, weapon, angle, projectile, ammoComponent.Velocity);
            }

            ammoComponent.Spent = true;
        }

        public override void ShootProjectile(IEntity? user, IGun weapon, Angle angle,
            SharedProjectileComponent projectileComponent, float velocity)
        {
            projectileComponent.Owner.Transform.AttachToGridOrMap();
            var physicsComponent = projectileComponent.Owner.EnsureComponent<PhysicsComponent>();
            physicsComponent.BodyStatus = BodyStatus.InAir;

            if (user != null)
                projectileComponent.Shooter = user.Uid;

            physicsComponent
                .LinearVelocity = angle.ToVec() * velocity;

            projectileComponent.Owner.Transform.WorldRotation = angle.Theta + MathF.PI / 2;
        }

        protected override Filter GetFilter(IEntity user, SharedGunComponent gun)
        {
            var filter = Filter.Pvs(gun.Owner);
            var session = user.PlayerSession();

            return session == null ? filter : filter.RemovePlayer(session);
        }

        protected override Filter GetFilter(SharedAmmoProviderComponent ammoProvider)
        {
            return Filter.Pvs(ammoProvider.Owner);
        }
    }
}
