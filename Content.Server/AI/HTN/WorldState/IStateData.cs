using System;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState
{
    public interface IStateData
    {
        string Name { get; }
        void Setup(IEntity owner);
    }
}
