#nullable enable
using System;
using Content.Client.GameObjects.Components.Items;
using Content.Client.GameObjects.Components.Mobs;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal sealed class RangedWeaponSystem : SharedRangedWeaponSystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        private InputSystem _inputSystem = default!;
        private CombatModeSystem _combatModeSystem = default!;
        private SharedRangedWeaponComponent? _firingWeapon;

        private bool _lastFireResult = true;

        public override void Initialize()
        {
            base.Initialize();

            IoCManager.InjectDependencies(this);
            _inputSystem = EntitySystemManager.GetEntitySystem<InputSystem>();
            _combatModeSystem = EntitySystemManager.GetEntitySystem<CombatModeSystem>();
        }

        private SharedRangedWeaponComponent? GetRangedWeapon(IEntity entity)
        {
            if (!entity.TryGetComponent(out HandsComponent? hands))
                return null;

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out SharedRangedWeaponComponent? weapon))
                return null;

            return weapon;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_gameTiming.IsFirstTimePredicted)
                return;

            var currentTime = _gameTiming.CurTime;
            var state = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
            if (!_combatModeSystem.IsInCombatMode() || state != BoundKeyState.Down)
            {
                // Result this so we can queue up more firing.
                _lastFireResult = false;

                if (_firingWeapon != null)
                {
                    StopFiring(_firingWeapon);
                    _firingWeapon.ShotCounter = 0;
                    _firingWeapon = null;
                }

                return;
            }

            var player = _playerManager.LocalPlayer?.ControlledEntity;
            if (player == null)
                return;

            var lastFiringWeapon = _firingWeapon;
            _firingWeapon = GetRangedWeapon(player);

            if (lastFiringWeapon != _firingWeapon && lastFiringWeapon != null)
                StopFiring(lastFiringWeapon);

            if (_firingWeapon == null)
                return;

            var mouseCoordinates = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);

            if (TryFire(player, _firingWeapon, mouseCoordinates, out var shots, currentTime))
            {
                RaiseNetworkEvent(new ShootMessage(_firingWeapon.Owner.Uid, mouseCoordinates, shots, currentTime));
            }
            else
            {
                StopFiring(_firingWeapon);
            }
        }

        private void StopFiring(SharedRangedWeaponComponent weaponComponent)
        {
            /*
            if (weaponComponent.Firing)
                RaiseNetworkEvent(new StopFiringMessage(weaponComponent.Owner.Uid, weaponComponent.ShotCounter));
            */
            weaponComponent.Firing = false;
        }

        public override void MuzzleFlash(IEntity? user, IGun weapon, Angle angle, TimeSpan? currentTime = null, bool predicted = true, float alphaRatio = 1)
        {
            var texture = weapon.MuzzleFlash;
            if (texture == null || !predicted)
                return;

            var offset = angle.ToVec().Normalized / 2;

            var message = new EffectSystemMessage
            {
                EffectSprite = texture,
                Born = _gameTiming.CurTime,
                DeathTime = _gameTiming.CurTime + TimeSpan.FromSeconds(0.2),
                AttachedEntityUid = weapon.Owner.Uid,
                AttachedOffset = offset,
                //Rotated from east facing
                Rotation = (float) angle.Theta,
                Color = Vector4.Multiply(new Vector4(255, 255, 255, 750), alphaRatio),
                ColorDelta = new Vector4(0, 0, 0, -1500f),
                Shaded = false
            };

            RaiseLocalEvent(message);
        }

        // TODO: Won't be used until container prediction
        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null)
        {
            throw new InvalidOperationException();
        }

        public override void ShootHitscan(IEntity? user, IGun weapon, HitscanPrototype hitscan, Angle angle,
            float damageRatio = 1, float alphaRatio = 1)
        {
            // TODO: Predict
            return;
        }

        public override void ShootAmmo(IEntity? user, IGun weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            // TODO
            return;
        }

        public override void ShootProjectile(IEntity? user, IGun weapon, Angle angle,
            SharedProjectileComponent projectileComponent, float velocity)
        {
            // TODO
            return;
        }

        protected override Filter GetFilter(IGun gun)
        {
            return Filter.Local();
        }

        protected override bool TryShootBallistic(IBallisticGun weapon, Angle angle)
        {
            // TODO: Checks here

            if (!base.TryShootBallistic(weapon, angle)) return false;
            return true;
        }

        protected override bool TryShootMagazine(IMagazineGun magazine, Angle angle)
        {
            throw new NotImplementedException();
        }

        protected override bool TryShootRevolver(IRevolver revolver, Angle angle)
        {
            throw new NotImplementedException();
        }

        protected override bool TryShootBattery(IBatteryGun weapon, Angle angle)
        {
            if (!base.TryShootBattery(weapon, angle))
                return false;

            if (weapon.PowerCell == null)
                return false;

            var (currentCharge, maxCharge) = weapon.PowerCell.Value;
            if (currentCharge < weapon.LowerChargeLimit)
            {
                if (weapon.SoundEmpty != null)
                    SoundSystem.Play(GetFilter(weapon), weapon.SoundEmpty, weapon.Owner, AudioHelpers.WithVariation(IGun.EmptyVariation).WithVolume(IGun.EmptyVolume));

                return false;
            }

            var chargeChange = Math.Min(currentCharge, weapon.BaseFireCost);
            weapon.PowerCell = (currentCharge - chargeChange, maxCharge);

            var shooter = weapon.Shooter();
            CameraRecoilComponent? cameraRecoilComponent = null;
            shooter?.TryGetComponent(out cameraRecoilComponent);

            cameraRecoilComponent?.Kick(-angle.ToVec().Normalized * weapon.RecoilMultiplier * chargeChange / weapon.BaseFireCost);

            if (!weapon.AmmoIsHitscan)
                MuzzleFlash(shooter, weapon, angle);

            if (weapon.SoundGunshot != null)
                SoundSystem.Play(GetFilter(weapon), weapon.SoundGunshot, weapon.Owner, AudioHelpers.WithVariation(IGun.GunshotVariation).WithVolume(IGun.GunshotVolume));

            // TODO: Show effect here once we can get the full hitscan predicted

            weapon.UpdateAppearance();
            weapon.UpdateStatus();
            return true;
        }
    }
}
