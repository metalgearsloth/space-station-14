using System;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.Components.Weapon.Ranged;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.Routines
{
    public class RangedAttackAiRoutine : AiRoutine
    {
        // If you want to move in range first use the MovementAiRoutine
        private IEntity _owner;
#pragma warning disable 649
        [Dependency] private IEntitySystemManager _entitySystemManager;
#pragma warning restore 649
        private MoveToGridCoordsAiRoutine _mover = new MoveToGridCoordsAiRoutine();

        public float AttackRange { get; set; } = 5.0f;

        public GridCoordinates FiringSpot => _firingSpot;
        private GridCoordinates _firingSpot = default;

        public IEntity Target => _target;
        private IEntity _target;
        private bool _targetAlive = true;

        private bool _reloading = false;

        public void ChangeTarget(IEntity target)
        {
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
        }

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IoCManager.InjectDependencies(this); // TODO: Investigate removing this and using the base class
            _owner = owner;
            if (_owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                combatModeComponent.IsInCombatMode = true;
            }
        }

        public bool InRange()
        {
            var targetDiff = Target.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position;
            return (targetDiff.Length <= AttackRange);
        }

        private bool HasWeapon()
        {
            if (!_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            if (handsComponent.GetActiveHand == null)
            {
                return false;
            }

            if (!handsComponent.GetActiveHand.Owner.HasComponent<RangedWeaponComponent>())
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Will go towards the target and shoot at them; will also reload if it needs to
        /// </summary>
        public void GoAttack()
        {
            if (Target == null || !_targetAlive)
            {
                return;
            }

            if (_reloading)
            {
                return;
            }

            if (InRange())
            {
                _mover.HaveArrived();
                CheckAttack();
                return;
            }

            // If the firing spot is no longer in range we need a new one
            if ((Target.Transform.GridPosition.Position - _firingSpot.Position).Length > AttackRange)
            {
                FindFiringSpot();
            }
        }

        private void CheckReload()
        {
            // If empty and not already reloading find nearby sources of ammo or whatever
        }

        private void FindFiringSpot()
        {
            // This will pick a random spot (probably top-left tile every time?) although because the mover cuts short if it's in range this should be okay...
            if (Target == null)
            {
                return;
            }

            _mover.HaveArrived();
            _mover.TargetTolerance = AttackRange - 0.01f;
            _mover.GetRoute(_firingSpot);
            // Pick a random tile in AttackRange
        }

        private void CheckAttack()
        {
            if (!HasWeapon())
            {
                return;
            }

            // TODO: Try and swing a weapon if we're holding one
            if (_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                // Sick we got hands, can we glass 'em
                if (handsComponent.GetActiveHand.Owner.TryGetComponent(out RangedWeaponComponent rangedWeaponComponent))
                {
                    var interactionSystem = _entitySystemManager.GetEntitySystem<InteractionSystem>();
                    interactionSystem.UseItemInHand(_owner, Target.Transform.GridPosition, Target.Uid);
                }
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
