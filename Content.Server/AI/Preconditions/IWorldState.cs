using System.Collections.Generic;
using Content.Server.AI.Preconditions;

namespace Content.Server.AI
{
    public interface IWorldState
    {
        HashSet<KeyValuePair<AiState, bool>> GetState();
    }
}
