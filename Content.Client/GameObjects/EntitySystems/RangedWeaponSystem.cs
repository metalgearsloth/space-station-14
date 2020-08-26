#nullable enable
using System;
using Content.Client.GameObjects.Components.Items;
using Content.Client.GameObjects.Components.Weapons.Ranged;
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
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.EntitySystems
{
    public class RangedWeaponSystem : EntitySystem
    {
        
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
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

        private IRangedWeapon? GetRangedWeapon(IEntity? entity)
        {
            if (entity == null || !entity.TryGetComponent(out HandsComponent? hands))
            {
                return null;
            }

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out IRangedWeapon? weapon))
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
            if (weapon == null)
            {
                _blocked = true;
                return;
            }
            
            // TODO: The above needs cleanup.

            // TODO: Should I just move the below into a SharedRangedWeapon class? It's really fucking tempting.
            var currentTime = _gameTiming.CurTime;
            if (currentTime < weapon.NextFire)
            {
                return;
            }
            
            // If it's our first shot then we'll fire at least 1 bullet now.
            if (_shotCounter == 0 && weapon.NextFire <= currentTime)
            {
                weapon.NextFire = currentTime;
            }
            
            // We'll send them a popup explaining why they can't as well.
            if (!weapon.CanFire(player))
            {
                return;
            }
            
            // TODO: Server-side as well.
            weapon.MuzzleFlash();
            
            // TODO: Set NextFire to current time whenever the weapon is equipped
            //
            
            // TODO: Look at how they initially set it, because if there's a 10 second gap between firing das a whole lot of firing.
            // TODO: Look at Valve's example, need to iterate over it multiple times.

            var firedShots = 0;
            var soundSystem = Get<AudioSystem>();
            
            // To handle guns with firerates higher than framerate / tickrate
            while (weapon.NextFire <= currentTime)
            {
                // soundSystem.Play();
                weapon.NextFire += TimeSpan.FromSeconds(1 / weapon.FireRate);
                
                // Mainly check if we can get more bullets (e.g. if there's only 1 left in the clip).
                if (!weapon.TryFire())
                {
                    break;
                }
                
                firedShots++;
            }

            /* TODO: Move into CanFire?
            switch (weapon.Selector)
            {
                case FireRateSelector.Safety:
                    _blocked = true;
                    return;
                case FireRateSelector.Single:
                    if (_shotCounter >= 1)
                    {
                        _blocked = true;
                        return;
                    }

                    break;
                case FireRateSelector.Automatic:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            */

            var mousePos = _eyeManager.ScreenToMap(_inputManager.MouseScreenPosition);
            weapon.NextFire = _gameTiming.CurTime + TimeSpan.FromSeconds(1 / weapon.FireRate);
            RaiseNetworkEvent(new RangedFireMessage());
            // TODO: Recoil / viewkick
        }
    }
}
