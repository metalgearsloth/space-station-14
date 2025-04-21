using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Procedural;
using Content.Shared.CCVar;
using Content.Shared.Ghost;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem : EntitySystem
{
    /*
     * Handles loading in biomes around players.
     * These are essentially chunked-areas that load in dungeons and can also be unloaded.
     *
     * How it works is that if the biome is idle (not loading) it works out what viewer bounds there are on the map,
     * then queues these areas up to load. The actual loaded area may be larger as each layer may have different chunk sizes.
     * While loading is occurring they can't load new areas until the old ones have loaded.
     */

    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;

    /// <summary>
    /// Jobs for biomes to load.
    /// </summary>
    private JobQueue _biomeQueue = default!;

    private float _checkUnloadAccumulator;
    private float _checkUnloadTime;
    private float _loadRange = 1f;
    private float LoadTime => (float) _biomeQueue.MaxTime;

    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<BiomeComponent> _biomeQuery;

    public override void Initialize()
    {
        base.Initialize();
        _biomeQueue = new JobQueue(float.MaxValue);
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _biomeQuery = GetEntityQuery<BiomeComponent>();

        SubscribeLocalEvent<BiomeComponent, MapInitEvent>(OnBiomeMapInit);

        Subs.CVar(_cfgManager, CCVars.BiomeLoadRange, OnLoadRange);
        Subs.CVar(_cfgManager, CCVars.BiomeLoadTime, OnLoadTime, true);
        Subs.CVar(_cfgManager, CCVars.BiomeCheckUnloadTime, OnCheckUnload, true);
    }

    private void OnCheckUnload(float obj)
    {
        _checkUnloadTime = obj;
    }

    private void OnLoadTime(float obj)
    {
        _biomeQueue.MaxTime = obj;
    }

    private void OnLoadRange(float obj)
    {
        _loadRange = obj;
    }

    private void OnBiomeMapInit(Entity<BiomeComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Template == null)
            return;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<BiomeComponent>();

        while (query.MoveNext(out var biome))
        {
            // If it's still loading then don't touch the observer bounds.
            if (biome.Loading)
                continue;

            biome.LoadedBounds.Clear();
        }

        // Get all relevant players.
        foreach (var player in new List<ICommonSession>())
        {
            if (player.AttachedEntity != null)
            {
                TryAddBiomeBounds(player.AttachedEntity.Value);
            }

            foreach (var viewer in player.ViewSubscriptions)
            {
                TryAddBiomeBounds(viewer);
            }
        }

        // Check for unloads before loads so we don't just increase memory forever if falling behind.
        _checkUnloadAccumulator += frameTime;

        if (_checkUnloadAccumulator > _checkUnloadTime)
        {
            // Work out chunks not in range and unload.
            UnloadChunks();

            _checkUnloadTime -= _checkUnloadAccumulator;
        }

        // Check if any biomes are intersected and queue up loads.
        while (query.MoveNext(out var biome))
        {
            if (biome.Loading || biome.LoadedBounds.Count == 0 || !biome.Enabled)
                continue;

            biome.Loading = true;
            var job = new BiomeLoadJob(LoadTime)
            {
                Biome = biome,
            };
            _biomeQueue.EnqueueJob(job);
        }

        // Process jobs.
        _biomeQueue.Process();
    }

    private void UnloadChunks()
    {
        var query = AllEntityQuery<BiomeComponent>();
        var toUnloadLayers = new Dictionary<string, ValueList<Vector2i>>();

        while (query.MoveNext(out var bUid, out var biome))
        {
            // Only start unloading if it's currently not loading anything.
            if (biome.Loading || !biome.Enabled)
                continue;

            foreach (var (layerId, loadedLayer) in biome.LoadedData)
            {
                var layer = biome.Layers[layerId];
                var toUnload = new ValueList<Vector2i>();

                // Go through each loaded chunk and check if they can be unloaded by checking if any players are in range.
                foreach (var chunk in loadedLayer.Keys)
                {
                    var chunkBounds = new Box2i(chunk, chunk + layer.Size);
                    var canUnload = true;

                    foreach (var playerView in biome.LoadedBounds)
                    {
                        // Still relevant
                        if (chunkBounds.Intersects(playerView))
                        {
                            canUnload = false;
                            break;
                        }
                    }

                    if (!canUnload)
                        continue;

                    toUnload.Add(chunk);
                }

                if (toUnload.Count == 0)
                    continue;

                toUnloadLayers.Add(layerId, toUnload);
            }

            if (toUnloadLayers.Count == 0)
                continue;

            // Queue up unloads.
            var job = new BiomeUnloadJob(LoadTime);
            job.Layers = toUnloadLayers;
            job.Biome = new Entity<BiomeComponent>(bUid, biome);
            job.System = this;

            _biomeQueue.EnqueueJob(job);
            biome.Loading = true;
        }
    }

    /// <summary>
    /// Gets the full bounds to be loaded. Considers layer dependencies where they may have different chunk sizes.
    /// </summary>
    private Box2i GetFullBounds(BiomeComponent component, Box2i bounds)
    {
        var baseBounds = bounds;

        foreach (var layer in component.Layers.Values)
        {
            var layerBounds = baseBounds;

            if (layer.DependsOn != null)
            {
                foreach (var sub in layer.DependsOn)
                {
                    var depLayer = component.Layers[sub];

                    layerBounds = layerBounds.Union(GetLayerBounds(depLayer, layerBounds));
                }
            }

            bounds = bounds.Union(layerBounds);
        }

        return bounds;
    }

    /// <summary>
    /// Tries to add the viewer bounds of this entity for loading.
    /// </summary>
    private void TryAddBiomeBounds(EntityUid uid)
    {
        // Ghosts can't load in.
        //if (_ghostQuery.HasComp(uid))
        //    return;

        var xform = Transform(uid);

        // No biome to load
        if (!_biomeQuery.TryComp(xform.MapUid, out var biome))
            return;

        // Currently already loading.
        if (biome.Loading)
            return;

        var center = _xforms.GetWorldPosition(uid);

        var bounds = new Box2i((center - new Vector2(_loadRange, _loadRange)).Floored(), (center + new Vector2(_loadRange, _loadRange)).Floored());

        biome.LoadedBounds.Add(bounds);
    }

    /// <summary>
    /// Converts a specified bounds area into the layer's relevant chunked area.
    /// </summary>
    /// <remarks>
    /// Useful for a chunk layer to determine what layers below it require what area to be loaded.
    /// </remarks>
    public Box2i GetLayerBounds(BiomeMetaLayer layer, Box2i layerBounds)
    {
        var chunkSize = (Vector2) layer.Size;

        // Need to round the bounds to our chunk size to ensure we load whole chunks.
        // We also need to know the minimum bounds for our dependencies to load.
        var layerBL = (layerBounds.BottomLeft / chunkSize).Floored() * chunkSize;
        var layerTR = (layerBounds.TopRight / chunkSize).Ceiled() * chunkSize;

        var loadBounds = new Box2i(layerBL.Floored(), layerTR.Ceiled());
        return loadBounds;
    }
}

 public sealed class BiomeLoadJob : Job<bool>
    {
        private BiomeSystem System = default!;

        public Entity<MapGridComponent> Grid;

        /// <summary>
        /// Biome that is getting loaded.
        /// </summary>
        public BiomeComponent Biome = default!;

        public BiomeLoadJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public BiomeLoadJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override async Task<bool> Process()
        {
            // Iterate each viewport
            foreach (var bound in Biome.LoadedBounds)
            {
                // Iterate each layer
                foreach (var (layerId, layer) in Biome.Layers)
                {
                    await LoadLayer(layerId, layer, bound);
                }
            }

            // Finished
            DebugTools.Assert(Biome.Loading);
            Biome.Loading = false;
            return true;
        }

        private async Task LoadLayer(string layerId, BiomeMetaLayer layer, Box2i parentBounds)
        {
            var loadBounds = System.GetLayerBounds(layer, parentBounds);

            // Make sure our dependencies are loaded first.
            if (layer.DependsOn != null)
            {
                foreach (var sub in layer.DependsOn)
                {
                    var actualLayer = Biome.Layers[sub];

                    await LoadLayer(sub, actualLayer, loadBounds);
                }
            }

            // Okay all of our dependencies loaded so we can send it.
            var chunkEnumerator = new ChunkIndicesEnumerator(loadBounds, layer.Size);

            while (chunkEnumerator.MoveNext(out var chunk))
            {
                var layerLoaded = Biome.LoadedData.GetOrNew(layerId);

                // Layer already loaded for this chunk.
                // This can potentially happen if we're moving and the player's bounds changed but some existing chunks remain.
                if (layerLoaded.ContainsKey(chunk.Value))
                {
                    continue;
                }

                int seedOffset;

                unchecked
                {
                    seedOffset = chunk.Value.X * 256 + chunk.Value.Y + Biome.Seed;
                }

                // Load dungeon here async await and all that jaz.
                var loadedData = await IoCManager.Resolve<IEntityManager>()
                    .System<DungeonSystem>()
                    .GenerateDungeonAsync(IoCManager.Resolve<IPrototypeManager>().Index(layer.Dungeon), Grid.Owner, Grid.Comp, chunk.Value, seedOffset);

                // Cleanup loading
                layerLoaded.Add(chunk.Value, loadedData);
            }
        }
    }

    public sealed class BiomeUnloadJob : Job<bool>
    {
        public BiomeSystem System = default!;

        public Entity<BiomeComponent> Biome;
        public Dictionary<string, ValueList<Vector2i>> Layers = new();

        public BiomeUnloadJob(double maxTime, CancellationToken cancellation = default) : base(maxTime, cancellation)
        {
        }

        public BiomeUnloadJob(double maxTime, IStopwatch stopwatch, CancellationToken cancellation = default) : base(maxTime, stopwatch, cancellation)
        {
        }

        protected override async Task<bool> Process()
        {
            foreach (var layerId in Layers)
            {

            }

            DebugTools.Assert(Biome.Comp.Loading);
            Biome.Comp.Loading = false;
            return true;
        }
    }
