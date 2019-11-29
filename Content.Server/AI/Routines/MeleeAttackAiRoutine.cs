using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines
{
    /// <summary>
    /// If in range will attack the target entity
    /// </summary>
    public class MeleeAttackAiRoutine : AiRoutine
    {
        // If you want to move in range first use the MovementAiRoutine
        private IEntity _owner;
        private IEntitySystemManager _entitySystemManager;
        private MovementAiRoutine _mover;

        public IEntity Target { get; set; }

        public float AttackRange
        {
            get => _attackRange;
            set => _attackRange = value;
        }

        public override bool RequiresMover => true;

        private float _attackRange = InteractionSystem.InteractionRange;
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

        public override void InjectMover(MovementAiRoutine mover)
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
                CheckAttack();
                return;
            }

            // Else, use movement routine
            if (_mover == null)
            {
                return;
            }

            if (_mover.Route.Count == 0)
            {
                _mover.GetRoute(Target);
            }
            _mover.HandleMovement();
        }

        private void CheckAttack()
        {
            if (!HasWeapon())
            {
                return;
            }

            if (!InRange(Target))
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
