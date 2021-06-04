using System;
using System.Linq;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
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

            if (component.BoltClosed)
            {
                ToggleBolt(component);
            }
            else
            {
                EjectMagazine(component);
            }
        }

        private void EjectMagazine(SharedGunComponent component)
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

        private void Cycle(SharedChamberedGunComponent component, IEntity? user = null, bool manual = false)
        {
            if (component.TryPopChamber(out var ammo))
            {
                EjectCasing(user, ammo.Owner);
            }

            component.TryFeedChamber();

            var mag = component.Magazine;

            if (component.AutoEjectOnEmpty && mag != null && mag.AmmoCount == 0 && component.Chamber.ContainedEntity == null)
                EjectMagazine(component);

            if (manual)
            {
                var cycle = component.SoundCycle;

                if (!string.IsNullOrEmpty(cycle))
                {
                    SoundSystem.Play(Filter.Pvs(component.Owner), cycle);
                }
            }
        }

        private void ToggleBolt(SharedChamberedGunComponent component)
        {
            var bolt = component.BoltClosed;

            component.BoltClosed ^= true;
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
            // TODO: Handle queued shoot messages and stuff
        }

        private void HandleShoot(ShootMessage shootMessage, EntitySessionEventArgs session)
        {
            if (shootMessage.Shots == 0)
            {
                Logger.ErrorS("gun", $"Received 0 shots message from {session.SenderSession}");
                return;
            }

            Logger.Debug("Handle shoot!");
        }

        public override void MuzzleFlash(IEntity? user, IEntity weapon, SharedAmmoComponent ammo, Angle angle, TimeSpan? currentTime = null,
            bool predicted = false, float alphaRatio = 1)
        {
            if (ammo.MuzzleFlash == null) return;

            var time = GameTiming.CurTime;
            var deathTime = time + TimeSpan.FromMilliseconds(200);
            // Offset the sprite so it actually looks like it's coming from the gun
            var offset = angle.ToVec().Normalized / 2;

            var message = new EffectSystemMessage
            {
                EffectSprite = ammo.MuzzleFlash,
                Born = time,
                DeathTime = deathTime,
                AttachedEntityUid = weapon.Uid,
                AttachedOffset = offset,
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 255), 1.0f),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            _effectSystem.CreateParticle(message);
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

            projectileComponent.Owner.Transform.WorldRotation = angle.Theta;
        }

        protected override Filter GetFilter(SharedGunComponent gun)
        {
            return Filter.Pvs(gun.Owner);
        }

        protected override Filter GetFilter(SharedAmmoProviderComponent ammoProvider)
        {
            return Filter.Pvs(ammoProvider.Owner);
        }
    }
}
