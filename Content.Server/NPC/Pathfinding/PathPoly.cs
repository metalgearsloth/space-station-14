using Content.Shared.NPC;
using Robust.Shared.Map;

namespace Content.Server.NPC.Pathfinding;

public record struct PathPoly(EntityUid GraphUid, Vector2i Index, PathfindingData Data)
{
    [ViewVariables]
    public readonly EntityUid GraphUid = GraphUid;

    [ViewVariables]
    public readonly Vector2i Index = Index;

    /// <summary>
    /// Box of this poly on its tile.
    /// </summary>
    public readonly Box2 Box;

    [ViewVariables]
    public PathfindingData Data = Data;

    public bool IsValid()
    {
        return (Data.Flags & PathfindingBreadcrumbFlag.Invalid) == 0x0;
    }

    [ViewVariables]
    public EntityCoordinates Coordinates => new(GraphUid, Box.Center);

    // Explicitly don't check neighbors.

    public bool IsEquivalent(PathPoly other)
    {
        return GraphUid.Equals(other.GraphUid) &&
               Index.Equals(other.Index) &&
               Data.IsEquivalent(other.Data) &&
               Box.Equals(other.Box);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GraphUid, Index, Box);
    }
}
