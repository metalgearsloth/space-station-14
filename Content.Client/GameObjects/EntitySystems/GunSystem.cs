using System;
using Content.Client.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
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

        private SharedGunComponent? _firingWeapon;

        private SharedGunComponent? GetRangedWeapon(IEntity entity)
        {
            if (!entity.TryGetComponent(out HandsComponent? hands))
                return null;

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out SharedGunComponent? weapon))
                return null;

            return weapon;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!GameTiming.IsFirstTimePredicted)
                return;

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

            var mouseCoordinates = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);

            if (TryFire(player, _firingWeapon, mouseCoordinates, out var shots, currentTime))
            {
                RaiseNetworkEvent(new ShootMessage(_firingWeapon.Owner.Uid, mouseCoordinates, shots, currentTime));
            }
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
        }

        public override void MuzzleFlash(IEntity? user, IEntity weapon, SharedAmmoComponent ammo, Angle angle, TimeSpan? currentTime = null,
            bool predicted = false, float alphaRatio = 1)
        {
            throw new NotImplementedException();
        }

        public override void EjectCasing(IEntity? user, IEntity casing, bool playSound = true, Direction[]? ejectDirections = null)
        {
            throw new NotImplementedException();
        }

        public override void ShootHitscan(IEntity? user, SharedGunComponent weapon, HitscanPrototype hitscan, Angle angle,
            float damageRatio = 1, float alphaRatio = 1)
        {
            throw new NotImplementedException();
        }

        public override void ShootAmmo(IEntity? user, SharedGunComponent weapon, Angle angle, SharedAmmoComponent ammoComponent)
        {
            throw new NotImplementedException();
        }

        public override void ShootProjectile(IEntity? user, SharedGunComponent weapon, Angle angle,
            SharedProjectileComponent projectileComponent, float velocity)
        {
            throw new NotImplementedException();
        }

        protected override Filter GetFilter(SharedGunComponent gun)
        {
            throw new NotImplementedException();
        }

        protected override Filter GetFilter(SharedAmmoProviderComponent ammoProvider)
        {
            throw new NotImplementedException();
        }
    }
}
