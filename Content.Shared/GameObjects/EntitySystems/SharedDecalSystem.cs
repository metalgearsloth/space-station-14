using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Configuration;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedDecalSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] protected readonly IGameTiming GameTiming = default!;
        [Dependency] protected readonly IMapManager MapManager = default!;

        public const byte ChunkSize = 4;
        // TODO: Look at what client gas tile overlays did to handle those
        protected const float UpdateRange = 12.5f;

        protected Dictionary<GridId, Dictionary<Vector2i, DecalChunk>> Decals =
            new Dictionary<GridId, Dictionary<Vector2i, DecalChunk>>();

        public override void Initialize()
        {
            base.Initialize();
            MapManager.OnGridRemoved += HandleGridRemoved;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            MapManager.OnGridRemoved -= HandleGridRemoved;
        }

        public void RoundRestart()
        {
            Decals.Clear();
        }

        protected virtual void HandleGridRemoved(GridId gridId)
        {
            Decals.Remove(gridId);
        }

        protected static Vector2i GetChunkIndices(Vector2i indices)
        {
            return new Vector2i((int) Math.Floor((float) indices.X / ChunkSize) * ChunkSize, (int) MathF.Floor((float) indices.Y / ChunkSize) * ChunkSize);
        }

        protected DecalChunk GetOrCreateChunk(GridId gridId, Vector2i indices)
        {
            if (!Decals.TryGetValue(gridId, out var chunks))
            {
                chunks = new Dictionary<Vector2i, DecalChunk>();
                Decals[gridId] = chunks;
            }

            var chunkIndices = GetChunkIndices(indices);

            if (!chunks.TryGetValue(chunkIndices, out var chunk))
            {
                chunk = new DecalChunk(gridId, chunkIndices, GameTiming.CurTick);
                chunks[chunkIndices] = chunk;
            }

            return chunk;
        }

        /// <summary>
        ///     Get every chunk in range of our entity that exists, including on other grids.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected List<DecalChunk> GetChunksInRange(IEntity entity)
        {
            var inRange = new List<DecalChunk>();

            // This is the max in any direction that we can get a chunk (e.g. max 2 chunks away of data).
            var (maxXDiff, maxYDiff) = ((int) (UpdateRange / ChunkSize) + 1, (int) (UpdateRange / ChunkSize) + 1);

            var worldBounds = Box2.CenteredAround(entity.Transform.WorldPosition,
                new Vector2(UpdateRange, UpdateRange));

            foreach (var grid in MapManager.FindGridsIntersecting(entity.Transform.MapID, worldBounds))
            {
                if (!Decals.TryGetValue(grid.Index, out var chunks))
                {
                    continue;
                }

                var entityTile = grid.GetTileRef(entity.Transform.Coordinates).GridIndices;

                for (var x = -maxXDiff; x <= maxXDiff; x++)
                {
                    for (var y = -maxYDiff; y <= maxYDiff; y++)
                    {
                        var chunkIndices = GetChunkIndices(new Vector2i(entityTile.X + x * ChunkSize, entityTile.Y + y * ChunkSize));

                        if (!chunks.TryGetValue(chunkIndices, out var chunk)) continue;

                        // Now we'll check if it's in range and relevant for us
                        // (e.g. if we're on the very edge of a chunk we may need more chunks).

                        var (xDiff, yDiff) = (chunkIndices.X - entityTile.X, chunkIndices.Y - entityTile.Y);
                        if (xDiff > 0 && xDiff > UpdateRange ||
                            yDiff > 0 && yDiff > UpdateRange ||
                            xDiff < 0 && Math.Abs(xDiff + ChunkSize) > UpdateRange ||
                            yDiff < 0 && Math.Abs(yDiff + ChunkSize) > UpdateRange) continue;

                        inRange.Add(chunk);
                    }
                }
            }

            return inRange;
        }
    }

    public sealed class DecalChunk
    {
        public GridId GridId { get; }

        public Vector2i OriginIndices { get; }

        public GameTick LastModifiedTick { get; private set; }

        /// <summary>
        ///     The decal and the tick it was added.
        /// </summary>
        public Dictionary<Decal, GameTick> Decals { get; } = new Dictionary<Decal, GameTick>();

        public DecalChunk(GridId gridId, Vector2i originIndices, GameTick createdTick)
        {
            GridId = gridId;
            OriginIndices = originIndices;
            LastModifiedTick = createdTick;
        }

        public IEnumerable<Decal> GetModifiedDecals(GameTick? currentTick = null)
        {
            currentTick ??= GameTick.Zero;

            foreach (var (decal, tick) in Decals)
            {
                if (tick <= currentTick)
                    continue;

                yield return decal;
            }
        }

        public void Dirty(GameTick currentTick)
        {
            LastModifiedTick = currentTick;
        }

        public void Clear(GameTick currentTick)
        {
            Decals.Clear();
            Dirty(currentTick);
        }

        public void AddDecal(GameTick currentTick, Decal decal)
        {
            Decals.Add(decal, currentTick);
            Dirty(currentTick);
        }

        public void RemoveDecal(GameTick currentTick, Decal decal)
        {
            if (Decals.Remove(decal))
                Dirty(currentTick);
        }
    }

    /// <summary>
    ///     Simple sprites that don't require entity or component information.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class Decal
    {
        public EntityCoordinates Coordinates { get; }

        public Color Color { get; }

        public ResourcePath RsiPath { get; }

        public string State { get; }

        // TODO: Rotation
        public Decal(EntityCoordinates coordinates, Color color, ResourcePath rsiPath, string state)
        {
            Coordinates = coordinates;
            Color = color;
            RsiPath = rsiPath;
            State = state;
        }
    }

    /// <summary>
    ///     What gets sent to the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class DecalOverlayMessage : EntitySystemMessage
    {
        public Dictionary<GridId, List<Decal>> Decals { get; }

        public DecalOverlayMessage(Dictionary<GridId, List<Decal>> decals)
        {
            Decals = decals;
        }
    }

    public sealed class DecalMessage : EntitySystemMessage
    {
        public EntityCoordinates Coordinates { get; }

        public Color Color { get; }

        public ResourcePath RsiPath { get; }

        public string State { get; }

        public DecalMessage(EntityCoordinates coordinates, Color color, ResourcePath rsiPath, string state)
        {
            Coordinates = coordinates;
            Color = color;
            RsiPath = rsiPath;
            State = state;
        }
    }
}
