using System.Threading.Tasks;
using Content.Shared.Procedural;
using Content.Shared.Procedural.DungeonGenerators;
using Content.Shared.Procedural.PostGeneration;
using Robust.Shared.Random;

namespace Content.Server.Procedural;

public sealed partial class ProceduralSystem
{
    /// <summary>
    /// Gets the relevant dungeon, running recursively as relevant.
    /// </summary>
    /// <param name="reserve">Should we reserve tiles even if the config doesn't specify.</param>
    private async Task<List<Dungeon>> GetDungeons(
        Vector2i position,
        DungeonConfigPrototype config,
        ProceduralData data,
        List<IDunGenLayer> layers,
        bool reserve,
        HashSet<Vector2i> reservedTiles,
        int seed)
    {
        var rand = new Random(seed);
        var count = rand.Next(config.MinCount, config.MaxCount);

        for (var i = 0; i < count; i++)
        {
            position += rand.NextPolarVector2(config.MinOffset, config.MaxOffset).Floored();

            foreach (var layer in layers)
            {
                await RunLayer(dungeons, data, position, layer, reservedTiles, seed);

                if (reserve)
                {
                    foreach (var dungeon in dungeons)
                    {
                        reservedTiles.UnionWith(dungeon.AllTiles);
                    }
                }

                await SuspendDungeon();
                if (!ValidateResume())
                    return new List<Dungeon>();
            }

            seed = rand.Next();
        }

        return dungeons;
    }

    private async Task RunLayer(ProceduralData data, Vector2i position, IDunGenLayer layer, HashSet<Vector2i> reservedTiles, int seed)
    {
        _sawmill.Debug($"Doing postgen {layer.GetType()} for {_gen.ID} with seed {_seed}");

        // If there's a way to just call the methods directly for the love of god tell me.
        // Some of these don't care about reservedtiles because they only operate on dungeon tiles (which should
        // never be reserved)
        var random = new Random(seed);

        switch (layer)
        {
            // Dungeon generators
            case ExteriorDunGen exterior:
                dungeons.AddRange(await GenerateExteriorDungeon(position, data, exterior, reservedTiles, seed));
                break;
            case FillGridDunGen fill:
                await GenerateFillDungeon(position, data, fill, reservedTiles, seed);
                break;
            case NoiseDistanceDunGen distance:
                dungeons.Add(await GenerateNoiseDistanceDungeon(position, data, distance, reservedTiles, seed));
                break;
            case NoiseDunGen noise:
                dungeons.Add(await GenerateNoiseDungeon(position, data, noise, reservedTiles, seed));
                break;
            case PrototypeDunGen prototypo:
                var groupConfig = _prototype.Index(prototypo.Proto);
                position = (position + random.NextPolarVector2(groupConfig.MinOffset, groupConfig.MaxOffset)).Floored();

                var dataCopy = groupConfig.Data.Clone();
                dataCopy.Apply(data);

                dungeons.AddRange(await GetDungeons(position, groupConfig, dataCopy, groupConfig.Layers, groupConfig.ReserveTiles, reservedTiles, seed));
                break;
            case PrefabDunGen prefab:
                dungeons.Add(await GeneratePrefabDungeon(position, data, prefab, reservedTiles, seed));
                break;

            case ReplaceTileDunGen replace:
                dungeons.Add(await GenerateTileReplacementDungeon(replace, data, reservedTiles, random));
                break;

            // Postgen
            case AutoCablingPostGen cabling:
                await PostGen(cabling, data, dungeons[^1], reservedTiles, random);
                break;
            case BiomePostGen biome:
                await PostGen(biome, data, dungeons[^1], reservedTiles, random);
                break;
            case BoundaryWallPostGen boundary:
                await PostGen(boundary, data, dungeons[^1], reservedTiles, random);
                break;
            case CornerClutterPostGen clutter:
                await PostGen(clutter, data, dungeons[^1], reservedTiles, random);
                break;
            case CorridorClutterPostGen corClutter:
                await PostGen(corClutter, data, dungeons[^1], reservedTiles, random);
                break;
            case CorridorPostGen cordor:
                await PostGen(cordor, data, dungeons[^1], reservedTiles, random);
                break;
            case CorridorDecalSkirtingPostGen decks:
                await PostGen(decks, data, dungeons[^1], reservedTiles, random);
                break;
            case EntranceFlankPostGen flank:
                await PostGen(flank, data, dungeons[^1], reservedTiles, random);
                break;
            case JunctionPostGen junc:
                await PostGen(junc, data, dungeons[^1], reservedTiles, random);
                break;
            case MiddleConnectionPostGen dordor:
                await PostGen(dordor, data, dungeons[^1], reservedTiles, random);
                break;
            case DungeonEntrancePostGen entrance:
                await PostGen(entrance, data, dungeons[^1], reservedTiles, random);
                break;
            case ExternalWindowPostGen externalWindow:
                await PostGen(externalWindow, data, dungeons[^1], reservedTiles, random);
                break;
            case InternalWindowPostGen internalWindow:
                await PostGen(internalWindow, data, dungeons[^1], reservedTiles, random);
                break;
            case BiomeMarkerLayerPostGen markerPost:
                await PostGen(markerPost, data, dungeons[^1], reservedTiles, random);
                break;
            case RoomEntrancePostGen rEntrance:
                await PostGen(rEntrance, data, dungeons[^1], reservedTiles, random);
                break;
            case WallMountPostGen wall:
                await PostGen(wall, data, dungeons[^1], reservedTiles, random);
                break;
            case WormCorridorPostGen worm:
                await PostGen(worm, data, dungeons[^1], reservedTiles, random);
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
