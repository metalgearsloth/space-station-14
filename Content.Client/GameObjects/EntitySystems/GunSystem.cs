using System;
using Content.Client.GameObjects.Components.Items;
using Content.Client.GameObjects.Components.Weapons.Gun;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class GunSystem : SharedGunSystem
    {
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private IInputManager _inputManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;

        private CombatModeSystem _combatModeSystem = default!;
        private InputSystem _inputSystem = default!;
        private EffectSystem _effectSystem = default!;

        private SharedGunComponent? _firingWeapon;

        private bool _firing;

        public override void Initialize()
        {
            base.Initialize();
            _combatModeSystem = Get<CombatModeSystem>();
            _inputSystem = Get<InputSystem>();
            _effectSystem = Get<EffectSystem>();
        }

        private SharedGunComponent? GetRangedWeapon(IEntity entity)
        {
            if (!entity.TryGetComponent(out HandsComponent? hands))
                return null;

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out SharedGunComponent? weapon))
                return null;

            return weapon;
        }

        private bool Prediction => !GameTiming.InSimulation || GameTiming.IsFirstTimePredicted;

        private void GunUpdate(float frameTime)
        {
            var currentTime = GameTiming.CurTime;
            var state = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);

            if (!_combatModeSystem.IsInCombatMode() || state != BoundKeyState.Down)
            {
                StopFiring(currentTime);
                _firingWeapon = null;
                return;
            }

            var player = _playerManager.LocalPlayer?.ControlledEntity;
            if (player == null)
                return;

            var lastFiringWeapon = _firingWeapon;
            _firingWeapon = GetRangedWeapon(player);

            if (lastFiringWeapon != _firingWeapon && lastFiringWeapon != null)
            {
                StopFiring(currentTime);
            }

            if (_firingWeapon == null)
                return;

            if (!_firing)
            {
                _firingWeapon.NextFire = TimeSpan.FromSeconds(Math.Max(_firingWeapon.NextFire.TotalSeconds, currentTime.TotalSeconds));
                _firing = true;
            }

            var mouseCoordinates = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);
            var fireAngle = (mouseCoordinates.Position - player.Transform.WorldPosition).ToAngle();

            if (TryFire(player, _firingWeapon, mouseCoordinates, out var shots, currentTime) && shots > 0)
            {
                switch (_firingWeapon)
                {
                    case ChamberedGunComponent chamberedGun:
                        var mag = chamberedGun.Magazine;

                        EntityManager.EventBus.RaiseLocalEvent(
                            _firingWeapon.Owner.Uid,
                            new AmmoUpdateEvent(chamberedGun.Chamber != null, mag?.AmmoCount, mag?.AmmoMax));

                        break;
                }

                if (Prediction)
                {
                    var kickBack = _firingWeapon.KickBack;

                    if (kickBack > 0.0f && player.TryGetComponent(out SharedCameraRecoilComponent? cameraRecoil))
                    {
                        cameraRecoil.Kick(-fireAngle.ToVec() * kickBack * shots);
                    }

                    Logger.DebugS("gun", $"Fired {shots} shots at {currentTime}");
                    RaiseNetworkEvent(new ShootMessage(_firingWeapon.Owner.Uid, mouseCoordinates, shots, currentTime));
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            GunUpdate(frameTime);
        }

        private void StopFiring(TimeSpan currentTime)
        {
            if (_firingWeapon != null)
            {
                // Stop people switching hands rapidly
                if (_firingWeapon.FireRate > 0f)
                    _firingWeapon.NextFire = currentTime + TimeSpan.FromSeconds(1 / _firingWeapon.FireRate);

                _firingWeapon.ShotCounter = 0;
            }

            _firing = false;
        }

        protected override void Cycle(SharedChamberedGunComponent component, IEntity? user = null, bool manual = false)
        {
            if (component is not ChamberedGunComponent chamberedGun) return;

            if (component.TryPopChamber(out var ammo))
            {
                // EjectCasing(user, ammo.Owner);
            }

            component.TryFeedChamber();

            var mag = component.Magazine;

            if (mag is {AmmoCount: 0} && chamberedGun.Chamber == null)
            {
                ToggleBolt(chamberedGun);

                if (component.AutoEjectOnEmpty)
                    EjectMagazine(component);
            }
        }

        protected override void EjectMagazine(SharedGunComponent component)
        {
            return;
        }

        protected override void ToggleBolt(SharedChamberedGunComponent component)
        {
            component.BoltClosed ^= true;
            // Server will play sound for now until more predicted (main reason is the interactions aren't done client-side
            // so we can't really predict all cases of the bolt being toggled).
            component.UpdateAppearance();
        }

        protected override void PlayGunSound(IEntity? user, IEntity entity, string? sound, float variation = 0, float volume = 0)
        {
            if (string.IsNullOrEmpty(sound) || !Prediction) return;
            SoundSystem.Play(Filter.Local(), sound, AudioHelpers.WithVariation(variation).WithVolume(volume));
        }

        public override void MuzzleFlash(IEntity? user, SharedGunComponent weapon, Angle angle, TimeSpan currentTime, bool predicted = false)
        {
            if (!predicted || weapon.MuzzleFlash == null || !Prediction) return;

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

            _effectSystem.CreateEffect(message);

        }

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true)
        {
            throw new NotImplementedException();
        }

        public override void ShootHitscan(IEntity? user, IGun weapon, HitscanPrototype hitscan, Angle angle,
            float damageRatio = 1, float alphaRatio = 1)
        {
            throw new NotImplementedException();
        }

        public override void ShootAmmo(IEntity? user, IGun weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            throw new NotImplementedException();
        }

        public override void ShootProjectile(IEntity? user, IGun weapon, Angle angle,
            SharedProjectileComponent projectileComponent, float velocity)
        {
            throw new NotImplementedException();
        }

        protected override Filter GetFilter(IEntity user, SharedGunComponent gun)
        {
            return Filter.Local();
        }

        protected override Filter GetFilter(SharedAmmoProviderComponent ammoProvider)
        {
            return Filter.Local();
        }
    }
}
