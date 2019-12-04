using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.AI.Preconditions
{
    /// <summary>
    /// This essentially listens for events on the AiLogicController so rather than using CheckProceduralPreconditions everywhere you can cache things
    /// </summary>
    public class AiWorldState : IGoapProvider
    {
        private IEntity _owner;
        private HashSet<KeyValuePair<AiState, bool>> _aiStates = new HashSet<KeyValuePair<AiState, bool>>();

        public AiWorldState(IEntity owner)
        {
            _owner = owner;
        }

        public void UpdateState(AiState state, bool result)
        {
            throw new NotImplementedException();
        }

        public bool TryGetState(AiState state, out bool? result)
        {
            result = null;
            foreach (var (knownState, knownResult) in _aiStates)
            {
                if (knownState != state)
                {
                    continue;
                }
                result = knownResult;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Goes through components and sets up listeners for event changes
        /// </summary>
        public void SetupListeners()
        {
            // UpdateStatus blah
        }

    }

    public enum AiState
    {
        Hungry,
        Thirsty,
    }
}
