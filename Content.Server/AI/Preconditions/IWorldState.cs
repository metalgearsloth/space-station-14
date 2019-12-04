using System.Collections.Generic;

namespace Content.Server.AI.Preconditions
{
    public interface IWorldState
    {
        HashSet<KeyValuePair<string, bool>> GetState();
    }
}
