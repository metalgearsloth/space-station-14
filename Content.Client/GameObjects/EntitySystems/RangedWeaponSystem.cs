#nullable enable
using Content.Client.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Input;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class RangedWeaponSystem : EntitySystem
    {
        
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        private InputSystem _inputSystem = null!;
        private CombatModeSystem _combatModeSystem = null!;
        private SharedRangedWeapon? _firingWeapon = null;
        private bool _lastFireResult = true;

        public override void Initialize()
        {
            base.Initialize();

            IoCManager.InjectDependencies(this);
            _inputSystem = EntitySystemManager.GetEntitySystem<InputSystem>();
            _combatModeSystem = EntitySystemManager.GetEntitySystem<CombatModeSystem>();
        }

        private SharedRangedWeapon? GetRangedWeapon(IEntity entity)
        {
            if (!entity.TryGetComponent(out HandsComponent? hands))
            {
                return null;
            }

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out SharedRangedWeapon? weapon))
            {
                return null;
            }

            return weapon;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_gameTiming.IsFirstTimePredicted)
            {
                return;
            }
            
            var state = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
            if (!_combatModeSystem.IsInCombatMode() || state != BoundKeyState.Down)
            {
                // Result this so we can queue up more firing.
                _lastFireResult = true;
                
                if (_firingWeapon != null)
                {
                    _firingWeapon.Firing = false;
                    _firingWeapon = null;
                }
                return;
            }

            var player = _playerManager.LocalPlayer?.ControlledEntity;
            if (player == null)
            {
                return;
            }

            var currentFiringWeapon = _firingWeapon;
            _firingWeapon = GetRangedWeapon(player);

            if (currentFiringWeapon != _firingWeapon && currentFiringWeapon != null)
            {
                currentFiringWeapon.Firing = false;
            }
            
            if (_firingWeapon == null)
            {
                return;
            }
            
            // We'll block any more firings so a single shot weapon doesn't spam the SoundEmpty for example.
            if (!_lastFireResult)
            {
                _firingWeapon.FireAngle = null;
                _firingWeapon.Firing = false;
                return;
            }
            
            var currentTime = _gameTiming.CurTime;
            var mousePos = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);
            var angle = (mousePos.Position - player.Transform.MapPosition.Position).ToAngle();

            _lastFireResult = _firingWeapon.TryFire(currentTime, player, angle);

            // Update server as well if necessary
            _firingWeapon.FireAngle = angle;
            _firingWeapon.Firing = true;
        }
    }
}
