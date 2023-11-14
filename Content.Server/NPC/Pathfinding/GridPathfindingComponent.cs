using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.NPC.Pathfinding;

/// <summary>
/// Stores the relevant pathfinding data for grids.
/// </summary>
[RegisterComponent, Access(typeof(PathfindingSystem))]
public sealed partial class GridPathfindingComponent : Component
{
    [ViewVariables]
    public readonly Dictionary<Vector2i, List<PathPortal>> Portals = new();

    /// <summary>
    /// Retrieves the index where the specified portal is stored on this grid.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<PathPortal, Vector2i> PortalLookup = new();
}
