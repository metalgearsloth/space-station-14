using System.Collections.Generic;
using Content.Server.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Hands
{
    public sealed class FreeHands : EnumerableStateData<string>
    {
        public override string Name => "FreeHands";

        public override IEnumerable<string> GetValue()
        {
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                yield break;
            }

            foreach (var hand in handsComponent.ActivePriorityEnumerable())
            {
                if (handsComponent.GetHand(hand) == null)
                {
                    yield return hand;
                }
            }
        }
    }
}
