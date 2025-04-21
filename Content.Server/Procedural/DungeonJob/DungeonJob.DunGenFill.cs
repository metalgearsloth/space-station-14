using System.Numerics;
using System.Threading.Tasks;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// <see cref="FillGridDunGen"/>
    /// </summary>
    private async Task GenerateFillDunGen(FillGridDunGen fill, DungeonData data, Dungeon dungeon, HashSet<Vector2i> reservedTiles)
    {
        if (!data.Entities.TryGetValue(DungeonDataKey.Fill, out var fillEnt))
        {
            LogDataError(typeof(FillGridDunGen));
            return;
        }

        foreach (var tile in dungeon.AllTiles)
        {
            if (reservedTiles.Contains(tile))
                continue;

            if (!_maps.TryGetTileDef(_grid, tile, out var tileDef))
                continue;

            if (fill.AllowedTiles != null && !fill.AllowedTiles.Contains(tileDef.ID))
                continue;

            if (!_anchorable.TileFree(_grid, tile, DungeonSystem.CollisionLayer, DungeonSystem.CollisionMask))
                continue;

            var gridPos = _maps.GridTileToLocal(_gridUid, _grid, tile);
            AddLoadedEntity(fillEnt, gridPos);

            await SuspendDungeon();
            if (!ValidateResume())
                break;
        }
    }
}
