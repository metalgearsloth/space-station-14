using System.Numerics;
using Content.Server.Administration.Components;
using Content.Shared.Administration.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Administration.Systems;

/// <inheritdoc />
public sealed class EntityCloningSystem : SharedEntityCloningSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;

    /// <summary>
    /// Move entities along just so they're actually readable if we ever go there.
    /// </summary>
    private int _copyOffset;

    private EntityUid EnsureCloningMap()
    {
        var query = AllEntityQuery<ClonedEntityMapComponent>();

        while (query.MoveNext(out var uid, out _))
            return uid;

        var mapId = _mapManager.CreateMap();
        var mapUid = _mapManager.GetMapEntityId(mapId);
        AddComp<ClonedEntityMapComponent>(mapUid);
        _mapManager.SetMapPaused(mapId, true);
        return _mapManager.GetMapEntityId(mapId);
    }

    /// <summary>
    /// Attempts to clone the target to the cloning map.
    /// </summary>
    public void Clone(EntityUid target)
    {
        if (!Exists(target))
            return;

        var cloneMap = EnsureCloningMap();
        var isEven = _copyOffset % 2 == 0;
        var copy = Spawn(null, new EntityCoordinates(cloneMap, new Vector2(_copyOffset * (isEven ? 1f : -1f), 0f)));
        _copyOffset++;
        EntityManager.Copy(target, copy);
    }
}
