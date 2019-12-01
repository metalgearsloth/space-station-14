using System;
using System.Collections.Generic;
using Content.Server.AI.Routines;
using Content.Server.AI.Routines.Movers;
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
    public class SwarmAiProcessor : BaseAiProcessor
    {
#pragma warning disable 649
        [Dependency]
        private readonly IComponentManager _componentManager;

        [Dependency] private readonly IRobustRandom _robustRandom;
#pragma warning restore 649

        // Routines
        private IdleRoutine _idle = new IdleRoutine();
        private MeleeAttackAiRoutine _attacker = new MeleeAttackAiRoutine();
        private AcquireWeaponAiRoutine _weaponGrabber = new AcquireWeaponAiRoutine();

        private IEntity _target;

        public override IEnumerable<AiRoutine> GetRoutines()
        {
            return new List<AiRoutine>
            {
                _idle,
                _attacker,
                _weaponGrabber
            };
        }

        public override void Setup()
        {
            base.Setup();
            _idle.Setup(SelfEntity);
            _attacker.Setup(SelfEntity);
            _weaponGrabber.Setup(SelfEntity);

            // If we die just idle
            if (SelfEntity.TryGetComponent(out DamageableComponent damageableComponent))
            {
                damageableComponent.DamageThresholdPassed += (sender, args) =>
                {
                    if (args.DamageThreshold.ThresholdType == ThresholdType.Death)
                    {
                        ChangeActiveRoutine(_idle);
                        Logger.DebugS("ai", $"Target died, idling");
                        return;
                    }

                    if (args.DamageThreshold.ThresholdType != ThresholdType.Death && ActiveRoutine == _idle)
                    {
                        ChangeActiveRoutine(_weaponGrabber);
                    }
                };
            }

            ChangeActiveRoutine(_idle);
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
        protected override void ProcessLogic()
        {
            base.ProcessLogic();
            CheckRandomTarget();
            // Should only _idle when we die?
            if (_target == null)
            {
                return;
            }

            // Get weapon if needed
            if (!_weaponGrabber.HasWeapon)
            {
                ChangeActiveRoutine(_weaponGrabber);
                return;
            }
            // Find target
            CheckRandomTarget();
            // ELIMINATE!
            if (_target == null)
            {
                ChangeActiveRoutine(_idle);
                return;
            }

            _attacker.ChangeTarget(_target);
            ChangeActiveRoutine(_attacker);
        }

        /// <summary>
        /// Only 1 routine updating at a time
        /// </summary>
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
