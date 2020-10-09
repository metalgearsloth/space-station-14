using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.GameObjects.EntitySystems
{
    [UsedImplicitly]
    public abstract class SharedDecalSystem : EntitySystem
    {
        [Dependency] protected readonly IGameTiming GameTiming = default!;
        [Dependency] protected readonly IMapManager MapManager = default!;
        
        public const byte ChunkSize = 4;
        
        protected Dictionary<GridId, Dictionary<MapIndices, DecalChunk>> Decals =
            new Dictionary<GridId, Dictionary<MapIndices, DecalChunk>>();
        
        public void RoundRestart()
        {
            Decals.Clear();
        }
        
        protected static MapIndices GetChunkIndices(MapIndices indices)
        {
            return new MapIndices((int) Math.Floor((float) indices.X / ChunkSize) * ChunkSize, (int) MathF.Floor((float) indices.Y / ChunkSize) * ChunkSize);
        }

        protected DecalChunk GetOrCreateChunk(GridId gridId, MapIndices indices)
        {
            if (!Decals.TryGetValue(gridId, out var chunks))
            {
                chunks = new Dictionary<MapIndices, DecalChunk>();
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
        
        protected virtual void HandleDecalMessage(DecalMessage message)
        {
            var gridId = message.Coordinates.GetGridId(EntityManager);

            if (gridId == GridId.Invalid)
                return;

            var indices = MapManager.GetGrid(gridId).GetTileRef(message.Coordinates).GridIndices;
            var chunk = GetOrCreateChunk(gridId, indices);
            
            var decal = new Decal(message.Coordinates, message.TexturePath);
            var currentTick = GameTiming.CurTick;
            chunk.AddDecal(currentTick, decal);
            chunk.Dirty(currentTick);
        }
    }

    public sealed class DecalChunk
    {
        public GridId GridId { get; }
        
        public MapIndices OriginIndices { get; }
        
        public GameTick LastModifiedTick { get; private set; }
        
        /// <summary>
        ///     The decal and the tick it was added.
        /// </summary>
        public Dictionary<Decal, GameTick> Decals { get; } = new Dictionary<Decal, GameTick>();

        public DecalChunk(GridId gridId, MapIndices originIndices, GameTick createdTick)
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

        public string TexturePath { get; }
        
        // TODO: Rotation
        public Decal(EntityCoordinates coordinates, string texturePath)
        {
            Coordinates = coordinates;
            TexturePath = texturePath;
        }
    }

    /// <summary>
    ///     What gets sent to the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class DecalOverlayMessage : EntitySystemMessage
    {
        // We could also potentially send a Dictionary<Origin, List<Decal>> instead to save the client a bit of perf
        // But the bandwidth's probably more important?
        public GridId GridId { get; }

        private List<Decal> Decals { get; }

        public DecalOverlayMessage(GridId gridId, List<Decal> decals)
        {
            GridId = gridId;
            Decals = decals;
        }
    }
    
    public sealed class DecalMessage : EntitySystemMessage
    {
        public EntityCoordinates Coordinates { get; }
        
        public string TexturePath { get; }

        public DecalMessage(EntityCoordinates coordinates, string texturePath)
        {
            Coordinates = coordinates;
            TexturePath = texturePath;
        }
    }
}