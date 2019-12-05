using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.AI.Preconditions
{
    /// <summary>
    /// This essentially gets updated by components so rather than using
    /// CheckProceduralPreconditions everywhere you can cache things
    /// </summary>
    public class AiWorldState : IWorldState
    {
        private readonly IDictionary<string, bool> _aiStates = new Dictionary<string, bool>();

        public event Action<string, bool> StateUpdate;

        public IDictionary<string, bool> GetState()
        {
            return _aiStates;
        }

        public void UpdateState(string state, bool result)
        {
            if (!_aiStates.ContainsKey(state))
            {
                _aiStates.Add(state, result);
            }
            else
            {
                _aiStates[state] = result;
            }
            StateUpdate?.Invoke(state, result);
        }
    }
}
