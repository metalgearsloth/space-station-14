using System;
using System.Collections.Generic;
using Content.Server.AI.Routines;
using Content.Server.GameObjects;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;

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
        private MovementAiRoutine _mover = new MovementAiRoutine();
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
            // TODO: If dead just stop
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
