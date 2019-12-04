using System.Collections.Generic;
using Content.Server.AI.Preconditions;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI
{
    public class GoapAgent
    {
        public IEntity Owner;
        private AiWorldState _worldState;
        /// <summary>
        /// Goal state and priority for it
        /// </summary>
        public Dictionary<GoapGoal, int> Goals { get; set; }

        public void Setup()
        {
            _worldState = new AiWorldState(Owner);
            _worldState.SetupListeners();
        }

        public bool TryMoveAgent(GoapAgent action)
        {

        }
    }
}
