using System.Threading.Tasks;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// <see cref="SampleMetaDunGen"/>
    /// </summary>
    private async Task PostGen(SampleMetaDunGen gen,
        DungeonData data,
        Dungeon dungeon,
        HashSet<Vector2i> reservedTiles,
        Random random)
    {
        gen.Noise.SetSeed(random.Next());

        // TODO: Iterate rooms, get dungeon for each room, this should keep the count down probably.
        foreach (var tile in dungeon.AllTiles)
        {
            if (reservedTiles.Contains(tile))
                continue;

            var invert = gen.Invert;
            var value = gen.Noise.GetNoise(tile.X, tile.Y);
            value = invert ? value * -1 : value;

            if (value < gen.Threshold)
                continue;

            // TODO: Get dungeon at specified tile.
            throw new NotImplementedException();
        }
    }
}
