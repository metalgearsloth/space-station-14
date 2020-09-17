#nullable enable
using System;
using Content.Client.GameObjects.Components.Items;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Input;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.Input;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class RangedWeaponSystem : SharedRangedWeaponSystem
    {
        
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;

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
            // Update server as well if necessary
            _firingWeapon.FireAngle = angle;
            _firingWeapon.Firing = true;

            _lastFireResult = _firingWeapon.TryFire(currentTime, player, angle);
        }

        public override void PlaySound(IEntity? user, IEntity weapon, string? sound, bool randomPitch = false)
        {
            if (sound == null)
                return;

            Get<AudioSystem>().Play(sound, weapon, AudioHelpers.WithVariation(0.2f, _robustRandom));
        }

        public override void MuzzleFlash(IEntity? user, IEntity weapon, string texture, Angle angle)
        {
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

            RaiseLocalEvent(message);
        }

        public override void EjectCasings()
        {
            throw new NotImplementedException();
        }
    }
}
