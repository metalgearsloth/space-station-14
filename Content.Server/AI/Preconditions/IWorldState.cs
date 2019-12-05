using System;
using System.Collections.Generic;

namespace Content.Server.AI.Preconditions
{
    public interface IWorldState
    {
        event Action<string, bool> StateUpdate;
        IDictionary<string, bool> GetState();
        void UpdateState(string state, bool result);
    }
}
