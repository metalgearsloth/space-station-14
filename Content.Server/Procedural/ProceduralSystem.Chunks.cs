using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Decals;
using Robust.Server.Physics;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Procedural;

public sealed partial class ProceduralSystem
{
    private async Task<ProceduralMetaChunkData> GetData(int seed, Vector2i chunkOrigin, ProceduralMetaLayer meta)
    {
        var random = new Random(seed);

        var dungeons = await GetDungeons(position, _gen, _gen.Data, _gen.Layers, _gen.ReserveTiles, reservedTiles, _seed);
        return new ProceduralMetaChunkData();
    }

    private sealed class LoadChunkJob : Job<ProceduralMetaChunkData>
    {
        public EntityManager EntManager;

        public ProceduralSystem System;

        public Entity<MapGridComponent> Grid;
        public ProceduralMetaLayer Meta;

        public int Seed;

        public Vector2i ChunkOrigin;

        public LoadChunkJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public LoadChunkJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override async Task<ProceduralMetaChunkData> Process()
        {
            var data = await System.GetData(ChunkOrigin, Meta);

            // Defer splitting so they don't get spammed and so we don't have to worry about tracking the grid along the way.
            Grid.Comp.CanSplit = false;

            // TODO: Set tiles
            // TODO: Set decals
            // TODO: Set entities.

            Grid.Comp.CanSplit = true;
            EntManager.System<GridFixtureSystem>().CheckSplits(Grid);
            Meta.LoadedChunks[ChunkOrigin] = ChunkOrigin;
            return data;
        }
    }

    private sealed class UnloadChunkJob : Job<bool>
    {
        public EntityManager EntManager;

        public EntityLookupSystem Lookups;
        public ProceduralSystem System;
        public SharedDecalSystem Decals;
        public SharedMapSystem Maps;

        public Entity<MapGridComponent> Grid;

        public ProceduralMetaLayer Meta;

        public Vector2i ChunkOrigin;

        public UnloadChunkJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public UnloadChunkJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override Task<bool> Process()
        {
            var data = System.GetData(ChunkOrigin, Meta);
            var modified = false;

            // Check any tiles modified.
            if (!modified)
            {
                for (var i = 0; i < data.Tiles.Count; i++)
                {
                    var tile = data.Tiles[i];

                    if (!Maps.TryGetTileRef(Grid, Grid.Comp, tile.Index, out var tileRef) ||
                        tileRef.Tile != tile.Tile)
                    {
                        modified = true;
                        break;
                    }
                }
            }

            // Check any decals modified.
            if (!modified)
            {
                for (var i = 0; i < data.Decals.Count; i++)
                {
                    // TODO: I hate decals.
                    var decal = data.Decals[i];
                    var inRangeDecals = Decals.GetDecalsInRange(Grid, decal.Coordinates.Position, distance: 0.01f);
                    modified = true;
                    break;
                }
            }

            // Check any entities modified.
            if (!modified)
            {
                var entSet = new HashSet<EntityUid>();

                for (var i = 0; i < data.Entities.Count; i++)
                {
                    var entData = data.Entities[i];

                    entSet.Clear();
                    Lookups.GetEntitiesInRange(entData.Coordinates, 0.01f, entSet);

                    // Check if the entity has been modified.
                    foreach (var ent in entSet)
                    {
                        if (EntManager.GetComponent<TransformComponent>(ent).Coordinates != entData.Coordinates)
                        {
                            modified = true;
                            break;
                        }

                        var proto = EntManager.GetComponent<MetaDataComponent>(ent).EntityPrototype;

                        if (entData.Entity != proto)
                            continue;

                        if (EntManager.IsDefault(ent))
                            continue;

                        modified = true;
                        break;
                    }

                    if (modified)
                        break;
                }
            }

            // Finished now check what we need to do
            Meta.UnloadingChunks.Remove(ChunkOrigin);

            if (modified)
            {
                Meta.PersistedChunks.Add(ChunkOrigin);
                Meta.LoadedChunks.Remove(ChunkOrigin);
            }

            throw new NotImplementedException();
        }
    }
}
