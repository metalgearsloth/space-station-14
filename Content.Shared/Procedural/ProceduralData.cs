using Content.Shared.Maps;
using Content.Shared.Storage;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Procedural;

/// <summary>
/// Used to set procedural values for shared data.
/// </summary>
/// <remarks>
/// This lets us share data between different dungeon configs without having to repeat entire configs.
/// </remarks>
[DataRecord]
public sealed class ProceduralData
{
    // I hate this but it also significantly reduces yaml bloat if we add like 10 variations on the same set of layers
    // e.g. science rooms, engi rooms, cargo rooms all under PlanetBase for example.
    // without having to do weird nesting. It also means we don't need to copy-paste the same prototype across several layers
    // The alternative is doing like,
    // 2 layer prototype, 1 layer with the specified data, 3 layer prototype, 2 layers with specified data, etc.
    // As long as we just keep the code clean over time it won't be bad to maintain.

    public static ProceduralData Empty = new();

    public Dictionary<ProceduralDataKey, Color> Colors = new();
    public Dictionary<ProceduralDataKey, EntProtoId> Entities = new();
    public Dictionary<ProceduralDataKey, HashSet<Vector2i>> Indices = new();
    public Dictionary<ProceduralDataKey, ProtoId<EntitySpawnEntryPrototype>> SpawnGroups = new();
    public Dictionary<ProceduralDataKey, ProtoId<ContentTileDefinition>> Tiles = new();
    public Dictionary<ProceduralDataKey, EntityWhitelist> Whitelists = new();

    /// <summary>
    /// Applies the specified data to this data.
    /// </summary>
    public void Apply(ProceduralData data)
    {
        // Copy-paste moment.
        foreach (var color in data.Colors)
        {
            Colors[color.Key] = color.Value;
        }

        foreach (var color in data.Entities)
        {
            Entities[color.Key] = color.Value;
        }

        foreach (var color in data.SpawnGroups)
        {
            SpawnGroups[color.Key] = color.Value;
        }

        foreach (var color in data.Tiles)
        {
            Tiles[color.Key] = color.Value;
        }

        foreach (var color in data.Whitelists)
        {
            Whitelists[color.Key] = color.Value;
        }
    }

    public ProceduralData Clone()
    {
        return new ProceduralData
        {
            // Only shallow clones but won't matter for DungeonJob purposes.
            Colors = Colors.ShallowClone(),
            Entities = Entities.ShallowClone(),
            SpawnGroups = SpawnGroups.ShallowClone(),
            Tiles = Tiles.ShallowClone(),
            Whitelists = Whitelists.ShallowClone(),
        };
    }
}

public enum ProceduralDataKey : byte
{
    // Colors
    Decals,

    // Entities
    Cabling,
    CornerWalls,
    Junction,
    Walls,

    // Indices

    /// <summary>
    /// Tiles that shouldn't be re-used across layers.
    /// </summary>
    ReservedTiles,

    // SpawnGroups
    CornerClutter,
    Entrance,
    EntranceFlank,
    WallMounts,
    Window,

    // Tiles
    FallbackTile,

    // Whitelists
    Rooms,
}
