using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Updates
{
    public class CollisionChange : IPathfindingGraphUpdate
    {
        public IEntity Owner { get; }
        public bool Value { get; }

        public CollisionChange(IEntity owner, bool value)
        {
            Owner = owner;
            Value = value;
        }
    }
}
