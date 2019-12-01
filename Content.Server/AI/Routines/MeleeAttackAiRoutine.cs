using System;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.EntitySystems;
using Content.Server.Interfaces.Chat;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines
{
    /// <summary>
    /// Move to melee range and hit the target
    /// </summary>
    public class MeleeAttackAiRoutine : AiRoutine
    {
        // If you want to move in range first use the MovementAiRoutine
#pragma warning disable 649
        [Dependency] private IEntitySystemManager _entitySystemManager;
#pragma warning restore 649
        private MoveToEntityAiRoutine _mover = new MoveToEntityAiRoutine();

        public IEntity Target => _target;
        private IEntity _target;
        private bool _targetAlive = true;

        /// <summary>
        /// Range from the target to start sprinting
        /// </summary>
        public float ChargeRange { get; set; } = 3.0f;

        /// <summary>
        /// How long in seconds until we can charge again
        /// </summary>
        public float ChargeCooldown { get; set; } = 3.0f;
        private DateTime _lastCharge = DateTime.Now;

        public bool Charging => _charging;
        private bool _charging = false;

        public float AttackRange
        {
            get
            {
                if (!Owner.TryGetComponent(out HandsComponent handsComponent))
                {
                    return 1.0f;
                }

                if (handsComponent.GetActiveHand == null)
                {
                    return 1.0f;
                }

                if (handsComponent.GetActiveHand.Owner.TryGetComponent(out MeleeWeaponComponent meleeWeaponComponent))
                {
                    return meleeWeaponComponent.Range * 0.8f;
                }

                return 1.0f;
            }
        }

        public void ChangeTarget(IEntity target)
        {
            if (target == _target)
            {
                return;
            }
            _target = target;

            // If target can't be damaged should we even attack them? IDK
            if (_target.TryGetComponent(out DamageableComponent damageableComponent))
            {
                damageableComponent.DamageThresholdPassed += (sender, args) =>
                {
                    if (args.DamageThreshold.ThresholdType == ThresholdType.Death)
                    {
                        _targetAlive = false;
                        return;
                    }

                    _targetAlive = true;
                };
            }

            _mover.GetRoute(target);
        }

        public override bool RequiresMover => true;

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IoCManager.InjectDependencies(this);
            Owner = owner;
            if (Owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                combatModeComponent.IsInCombatMode = true;
            }
            _mover.Setup(owner);
        }

        public bool InRange()
        {
            var targetDiff = Target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position;
            return (targetDiff.Length <= AttackRange);
        }

        /// <summary>
        /// Check if we need to start "charging" to target if they're nearby (but out of melee range)
        /// </summary>
        public void CheckCharge()
        {
            if ((DateTime.Now - _lastCharge).TotalSeconds < ChargeCooldown)
            {
                return;
            }
            var targetDiff = Target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position;
            if (AttackRange < targetDiff.Length && targetDiff.Length <= ChargeRange && !_charging)
            {
                _lastCharge = DateTime.Now;
                Owner.TryGetComponent(out AiControllerComponent mover);
                mover.Sprinting = true;
                _charging = true;
                return;
            }
        }

        private void StopCharging()
        {
            _charging = false;
            Owner.TryGetComponent(out AiControllerComponent mover);
            mover.Sprinting = false;
        }

        private bool HasWeapon()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            if (handsComponent.GetActiveHand == null)
            {
                return false;
            }

            if (!handsComponent.GetActiveHand.Owner.TryGetComponent(out MeleeWeaponComponent weaponComponent))
            {
                return false;
            }
            return true;
        }

        private void CheckAttack()
        {
            if (!HasWeapon())
            {
                return;
            }

            // TODO: Try and swing a weapon if we're holding one
            if (Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                // Sick we got hands, can we glass 'em
                if (handsComponent.GetActiveHand.Owner.TryGetComponent(out MeleeWeaponComponent meleeWeaponComponent))
                {
                    var interactionSystem = _entitySystemManager.GetEntitySystem<InteractionSystem>();
                    interactionSystem.UseItemInHand(Owner, Target.Transform.GridPosition, Target.Uid);
                }
            }
        }

        public void GoAttack()
        {
            if (Target == null || !_targetAlive)
            {
                return;
            }

            CheckCharge();

            if (InRange())
            {
                StopCharging();

                if (!_mover.Arrived)
                {
                    _mover.HaveArrived();
                }
                CheckAttack();
                return;
            }

            if (!_mover.Arrived)
            {
                _mover.HandleMovement();
                return;
            }

            // If we're not already tracking the target
            if (_mover.TargetEntity != Target || _mover.Arrived)
            {
                _mover.TargetProximity = AttackRange - 0.5f;
                _mover.GetRoute(Target);
            }
        }

        public override void Update()
        {
            base.Update();
            GoAttack();
            if (!_mover.Arrived)
            {
                _mover.HandleMovement();
            }
        }
    }
}
