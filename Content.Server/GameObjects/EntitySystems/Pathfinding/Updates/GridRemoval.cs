using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Updates
{
    public struct GridRemoval : IPathfindingGraphUpdate
    {
        public GridId GridId { get; }

        public GridRemoval(GridId gridId)
        {
            GridId = gridId;
        }
    }
}
