using System.Linq;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.AI.HTN.WorldState.States.Inventory
{
    public class FreeHands : IStateData
    {
        public string Name => "FreeHands";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public int GetValue()
        {
            if (!_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return 0;
            }

            var freeHands = handsComponent.ActivePriorityEnumerable().ToList().Count -
                            handsComponent.GetAllHeldItems().ToList().Count;

            DebugTools.Assert(freeHands > 0);

            return freeHands;
        }
    }
}
