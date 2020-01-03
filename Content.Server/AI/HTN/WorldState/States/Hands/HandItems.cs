using System.Collections.Generic;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Hands
{
    public sealed class HeldItems : EnumerableStateData<IEntity>
    {
        public override string Name => "HeldItems";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                yield break;
            }

            foreach (var hand in handsComponent.ActivePriorityEnumerable())
            {
                var heldItem = handsComponent.GetHand(hand);

                if (heldItem != null)
                {
                    yield return heldItem.Owner;
                }
            }
        }
    }
}
