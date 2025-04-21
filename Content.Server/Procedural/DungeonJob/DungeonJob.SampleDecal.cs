using System.Threading.Tasks;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;
using Robust.Shared.Random;

namespace Content.Server.Procedural.DungeonJob;

public sealed partial class DungeonJob
{
    /// <summary>
    /// <see cref="SampleDecalDunGen"/>
    /// </summary>
    private async Task PostGen(SampleDecalDunGen gen,
        DungeonData data,
        Dungeon dungeon,
        HashSet<Vector2i> reservedTiles,
        Random random)
    {
        gen.Noise.SetSeed(random.Next());

        foreach (var tile in dungeon.AllTiles)
        {
            if (reservedTiles.Contains(tile))
                continue;

            var invert = gen.Invert;
            var value = gen.Noise.GetNoise(tile.X, tile.Y);
            value = invert ? value * -1 : value;

            if (value < gen.Threshold)
                continue;

            AddLoadedDecal(tile, random.Pick(gen.Decals));
        }
    }
}
