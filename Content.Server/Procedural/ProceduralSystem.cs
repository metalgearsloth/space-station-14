using System.Numerics;
using System.Threading;
using Robust.Server.Player;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Procedural;

public sealed partial class ProceduralSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ProceduralComponent> _proceduralQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly JobQueue _proceduralQueue = new();

    public const int LoadRange = 16;

    public override void Initialize()
    {
        base.Initialize();
        _proceduralQuery = GetEntityQuery<ProceduralComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<ProceduralComponent, TransformComponent>();

        while (query.MoveNext(out var procedural, out var xform))
        {
            if (procedural.NextUpdate < _timing.CurTime)
                continue;

            procedural.InvMatrix = _transform.GetInvWorldMatrix(xform);
            procedural.AreasOfInterest.Clear();
        }

        // Get chunks in range
        foreach (var pSession in Filter.GetAllPlayers(_player))
        {
            if (_xformQuery.TryGetComponent(pSession.AttachedEntity, out var xform) &&
                _proceduralQuery.TryGetComponent(xform.GridUid, out var procedural) &&
                procedural.NextUpdate >= _timing.CurTime &&
                procedural.Enabled)
            {
                var worldPos = _transform.GetWorldPosition(xform);
                var localPos = Vector2.Transform(worldPos, procedural.InvMatrix);
                var area = Box2.FromDimensions(localPos + new Vector2(LoadRange, LoadRange) / 2f, new Vector2(LoadRange, LoadRange));

                procedural.AreasOfInterest.Add(area);
            }
        }

        var loadQuery = AllEntityQuery<ProceduralComponent>();

        while (loadQuery.MoveNext(out var pUid, out var procedural))
        {
            if (procedural.NextUpdate < _timing.CurTime)
                continue;

            foreach (var meta in procedural.Layers)
            {
                var chunksInRange = new HashSet<Vector2i>();
                var toLoadChunks = new HashSet<Vector2i>();

                foreach (var area in procedural.AreasOfInterest)
                {
                    var areaChunks = new ChunkIndicesEnumerator(area, meta.ChunkSize);

                    while (areaChunks.MoveNext(out var origin))
                    {
                        chunksInRange.Add(origin.Value);
                    }
                }

                toLoadChunks.UnionWith(chunksInRange);

                // Remove chunks that we don't care about (loaded or persisted).
                toLoadChunks.IntersectWith(chunksInRange);
                toLoadChunks.IntersectWith(meta.PersistedChunks);

                foreach (var chunk in toLoadChunks)
                {
                    // If for some reason it's deloading then cancel it.
                    if (meta.UnloadingChunks.Remove(chunk, out var unloadTcs))
                    {
                        unloadTcs.Cancel();
                    }

                    // Already loading, skip it.
                    if (meta.LoadingChunks.ContainsKey(chunk) || meta.LoadedChunks.ContainsKey(chunk))
                        continue;

                    var tcs = new CancellationTokenSource();
                    var loadChunkJob = new LoadChunkJob(0.002, tcs.Token);

                    _proceduralQueue.EnqueueJob(loadChunkJob);
                    meta.LoadingChunks[chunk] = tcs;
                }

                // Jobs should be ordered so we can just chuck them in or out.
                foreach (var origin in meta.LoadedChunks.Keys)
                {
                    // If it's in range still / persisted / loading / unable to deload for any reason keep it.
                    if (chunksInRange.Contains(origin) ||
                        meta.PersistedChunks.Contains(origin) ||
                        (meta.Flags & ProceduralMetaFlags.NoUnload) != 0x0)
                    {
                        // Cancel unloading if something happened but it was inadvertantly added.
                        if (meta.UnloadingChunks.Remove(origin, out var token))
                        {
                            token.Cancel();
                        }

                        continue;
                    }

                    // Unload it.
                    var tcs = new CancellationTokenSource();
                    var unloadChunkJob = new UnloadChunkJob(0.002, tcs.Token);

                    _proceduralQueue.EnqueueJob(unloadChunkJob);
                    meta.UnloadingChunks[origin] = tcs;
                }
            }
        }

        _proceduralQueue.Process();
    }
}
