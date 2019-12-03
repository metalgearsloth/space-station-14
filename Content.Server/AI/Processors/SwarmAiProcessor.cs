using System;
using System.Collections.Generic;
using Content.Server.AI.Routines;
using Content.Server.AI.Routines.Combat;
using Content.Server.AI.Routines.Inventory;
using Content.Server.GameObjects;
using JetBrains.Annotations;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Log;

namespace Content.Server.AI.Processors
{
    /// <summary>
    ///     Designed to find a random player and attack them indefinitely.
    /// </summary>
    [UsedImplicitly]
    [AiLogicProcessor("Swarm")]
    public class SwarmAiProcessor : BaseAiProcessor
    {
        // Routines
        private readonly IdleRoutine _idle = new IdleRoutine();
        private readonly MeleeAttackAiRoutine _attacker = new MeleeAttackAiRoutine();
        private readonly AcquireItemRoutine _itemGrabber = new AcquireItemRoutine();

        private IEntity _target;

        public override float ProcessCooldown { get; set; } = 0.5f;

        public override void Setup()
        {
            base.Setup();

            // Routine setup
            _idle.Setup(SelfEntity, this);
            _attacker.Setup(SelfEntity, this);
            _itemGrabber.Setup(SelfEntity, this);

            // If we die just idle
            if (SelfEntity.TryGetComponent(out DamageableComponent damageableComponent))
            {
                damageableComponent.DamageThresholdPassed += (sender, args) =>
                {
                    if (args.DamageThreshold.ThresholdType == ThresholdType.Death)
                    {
                        ChangeRoutine(_idle);
                        Logger.DebugS("ai", $"We died, idling");
                        return;
                    }

                    if (args.DamageThreshold.ThresholdType != ThresholdType.Death && ActiveRoutine == _idle)
                    {
                        ChangeRoutine(_itemGrabber);
                    }
                };
            }

            // Finalise
            ChangeRoutine(_idle);
        }

        /// <summary>
        /// This where the majority of the processor specific logic should go.
        /// Any specific behaviors (e.g. ranged combat) should be a routine.
        /// </summary>
        protected override void ProcessLogic(float frameTime)
        {
            base.ProcessLogic(frameTime);

            // Get weapon if needed
            if (!_itemGrabber.HasItem)
            {
                ChangeRoutine(_itemGrabber);
                _itemGrabber.TargetCategory = ItemCategories.Melee;
                return;
            }

            // Find nearby targets; use cooldown so the idle isn't spamming
            if (_target == null || !_attacker.TargetAlive)
            {
                _target = Utils.RandomPlayerSpecies(SelfEntity, VisionRadius);
            }

            // Should only _idle when we die?
            if (_target == null)
            {
                ChangeRoutine(_idle);
                return;
            }

            _attacker.ChangeTarget(_target);
            ChangeRoutine(_attacker);
        }
    }
}
