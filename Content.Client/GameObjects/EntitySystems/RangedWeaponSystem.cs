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

namespace Content.Client.GameObjects.EntitySystems
{
    public class RangedWeaponSystem : EntitySystem
    {
        
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        private InputSystem _inputSystem = null!;
        private CombatModeSystem _combatModeSystem = null!;
        private bool _blocked;
        private int _shotCounter;

        public override void Initialize()
        {
            base.Initialize();

            IoCManager.InjectDependencies(this);
            _inputSystem = EntitySystemManager.GetEntitySystem<InputSystem>();
            _combatModeSystem = EntitySystemManager.GetEntitySystem<CombatModeSystem>();
        }

        private SharedRangedWeapon? GetRangedWeapon(IEntity? entity)
        {
            if (entity == null || !entity.TryGetComponent(out HandsComponent? hands))
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

            // TODO: Will also need parts of the logic server-side.
            var state = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
            if (!_combatModeSystem.IsInCombatMode() || state != BoundKeyState.Down)
            {
                _shotCounter = 0;
                _blocked = false;
                return;
            }

            var player = _playerManager.LocalPlayer?.ControlledEntity;
            var weapon = GetRangedWeapon(player);
            
            if (player == null || weapon == null)
            {
                _blocked = true;
                return;
            }

            var currentTime = _gameTiming.CurTime;
            var mousePos = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);
            weapon.TryFire(currentTime, player, mousePos);
        }
    }
}
