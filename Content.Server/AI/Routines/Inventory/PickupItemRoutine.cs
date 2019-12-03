using System;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines.Inventory
{
    /// <summary>
    ///  AI will move to and pickup the specified item
    /// </summary>
    public class PickupItemRoutine : AiRoutine
    {
        private MoveToEntityAiRoutine _mover = new MoveToEntityAiRoutine();

        public IEntity TargetItem => _targetItem;
        private IEntity _targetItem;
        private bool _newItem;

        protected override float ProcessCooldown { get; set; } = 1.0f;

        /// <summary>
        /// Updates the item to target. This will also get a new pathfinding route
        /// </summary>
        /// <param name="target"></param>
        public void ChangeItemTo(IEntity target)
        {
            if (target == TargetItem)
            {
                return;
            }

            _targetItem = target;
            _newItem = true;
        }

        public override void Setup(IEntity owner, AiLogicProcessor processor)
        {
            base.Setup(owner, processor);
            IoCManager.InjectDependencies(this);
            _mover.Setup(owner, processor);
        }

        private bool TryPickupItem(IEntity target)
        {
            if ((target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length <
                InteractionSystem.InteractionRange)
            {
                Owner.TryGetComponent(out HandsComponent handsComponent);
                // TODO: Interact with item instead
                target.TryGetComponent(out ItemComponent itemComponent);
                handsComponent.PutInHand(itemComponent);
                if (handsComponent.GetActiveHand != itemComponent)
                {
                    handsComponent.SwapHands();
                }
                return true;
            }

            return false;
        }

        // TODO: Also check clothing slots
        /// <summary>
        /// Will check whether the item is in our posession
        /// </summary>
        /// <returns></returns>
        public bool HasItem()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (item.Owner == TargetItem)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Will try and move to the target item to pick it up
        /// </summary>
        public void GoPickupItem(float frameTime)
        {
            if (HasItem() || TargetItem == null)
            {
                return;
            }

            if (TryPickupItem(TargetItem))
            {
                _mover.HaveArrived();
                return;
            }

            if (!_mover.Arrived)
            {
                _mover.Update(frameTime);
            }

            // Just to avoid spamming routes or interactions
            if (RemainingProcessCooldown > 0)
            {
                return;
            }

            // TODO: This might try and re-route if we became in range after the pickup earlier; investigate furtther
            if (_mover.Arrived || _newItem)
            {
                _newItem = false;
                // Just to give some tolerance for the pathfinder
                const float itemProximity = InteractionSystem.InteractionRange - 0.5f;
                _mover.TargetProximity = itemProximity;
                _mover.GetRoute(TargetItem, itemProximity);
            }
        }

        public override void Update(float frameTime)
        {
            GoPickupItem(frameTime);
            base.Update(frameTime);
        }

    }
}
