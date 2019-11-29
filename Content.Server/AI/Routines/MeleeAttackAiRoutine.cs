using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.EntitySystems;
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
        private IEntity _owner;
        private IEntitySystemManager _entitySystemManager;
        private MoveToEntityAiRoutine _mover;

        public IEntity Target { get; set; }

        public float AttackRange
        {
            get
            {
                if (!_owner.TryGetComponent(out HandsComponent handsComponent))
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

        public override bool RequiresMover => true;

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            _owner = owner;
            if (_owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                combatModeComponent.IsInCombatMode = true;
            }

            _entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
        }

        public override void InjectMover(MoveToEntityAiRoutine mover)
        {
            _mover = mover;
        }

        public bool InRange(IEntity target)
        {
            var targetDiff = target.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position;
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

            if (!handsComponent.GetActiveHand.Owner.TryGetComponent(out MeleeWeaponComponent weaponComponent))
            {
                return false;
            }
            return true;
        }

        public void GoAttack()
        {
            if (Target == null)
            {
                return;
            }

            if (InRange(Target))
            {
                _mover.ClearRoute();
                _mover.MovementAllowed = false;
                CheckAttack();
                return;
            }

            _mover.MovementAllowed = true;

            // If we're not already tracking target or there's no route already and we're far away
            if (_mover.TargetEntity != Target ||
                (_mover.Route.Count == 0 && (_owner.Transform.GridPosition.Position - Target.Transform.GridPosition.Position).Length > 5.0f))
            {
                _mover.GetRoute(Target);
            }

            _mover.TargetTolerance = AttackRange;
            _mover.HandleMovement();
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
                if (handsComponent.GetActiveHand.Owner.TryGetComponent(out MeleeWeaponComponent meleeWeaponComponent))
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
        }
    }
}
