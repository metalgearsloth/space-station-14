using System;
using System.Collections.Generic;
using Content.Server.AI.Routines;
using Content.Server.GameObjects;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Server.AI
{
    /// <summary>
    ///     Designed to find a random player and attack them indefinitely.
    /// </summary>
    [AiLogicProcessor("Swarm")]
    public class SwarmProcessor : AiLogicProcessor
    {
#pragma warning disable 649
        [Dependency]
        private readonly IComponentManager _componentManager;

        [Dependency] private readonly IRobustRandom _robustRandom;
#pragma warning restore 649

        // Routines
        private MoveToEntityAiRoutine _mover = new MoveToEntityAiRoutine();
        private IdleRoutine _idle = new IdleRoutine();
        private MeleeAttackAiRoutine _attacker = new MeleeAttackAiRoutine();
        private AcquireWeaponAiRoutine _weaponGrabber = new AcquireWeaponAiRoutine();

        private List<AiRoutine> _routines;

        public AiRoutine ActiveRoutine => _activeRoutine;
        private AiRoutine _activeRoutine;

        private void SwitchRoutine(AiRoutine routine)
        {
            if (routine == _activeRoutine)
            {
                return;
            }

            Logger.DebugS("ai", $"Set {this} AiRoutine to {routine}");
            _activeRoutine = routine;
        }

        private IEntity _target;

        public override void Setup()
        {
            base.Setup();
            _routines = new List<AiRoutine>()
            {
                _mover,
                _idle,
                _attacker,
                _weaponGrabber
            };

            _mover.Setup(SelfEntity);

            foreach (var routine in _routines.GetRange(1, _routines.Count - 1))
            {
                routine.Setup(SelfEntity);
                if (routine.RequiresMover)
                {
                    routine.InjectMover(_mover);
                }
            }

            // If we die just idle
            if (SelfEntity.TryGetComponent(out DamageableComponent damageableComponent))
            {
                damageableComponent.DamageThresholdPassed += (sender, args) =>
                {
                    if (args.DamageThreshold.ThresholdType == ThresholdType.Death)
                    {
                        SwitchRoutine(_idle);
                        return;
                    }

                    if (args.DamageThreshold.ThresholdType != ThresholdType.Death && ActiveRoutine == _idle)
                    {
                        SwitchRoutine(_weaponGrabber);
                    }
                };
            }
        }

        public void CheckRandomTarget()
        {
            if (_target != null)
            {
                return;
            }
            var targetList = new List<IEntity>();
            foreach (var species in _componentManager.GetAllComponents(typeof(SpeciesComponent)))
            {
                // No seppuku for u
                if (species.Owner == SelfEntity)
                {
                    continue;
                }
                targetList.Add(species.Owner);
            }

            if (targetList.Count == 0)
            {
                return;
            }

            var targetNumber = _robustRandom.Next(targetList.Count);
            _target = targetList[targetNumber];
        }

        /// <summary>
        /// This where the majority of the processor specific logic should go.
        /// Any specific behaviors (e.g. ranged combat) should be a routine.
        /// </summary>
        private void ProcessLogic()
        {
            // Should only idle when we die
            if (ActiveRoutine == _idle)
            {
                return;
            }

            // Get weapon if needed
            if (!_weaponGrabber.HasWeapon)
            {
                SwitchRoutine(_weaponGrabber);
                return;
            }
            // Find target
            CheckRandomTarget();
            // ELIMINATE!
            if (_target == null)
            {
                SwitchRoutine(_idle);
                return;
            }

            _attacker.Target = _target;
            SwitchRoutine(_attacker);
        }

        private void UpdateRoutine()
        {
            ActiveRoutine.Update();
        }

        public override void Update(float frameTime)
        {
            ProcessLogic();
            UpdateRoutine();
        }
    }
}
