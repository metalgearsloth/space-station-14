using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;

namespace Content.Client.Decals.Overlays
{
    public sealed class DecalOverlay : GridOverlay
    {
        private readonly SpriteSystem _sprites;
        private readonly IEntityManager _entManager;
        private readonly IPrototypeManager _prototypeManager;

        private readonly Dictionary<string, CachedTexture> _cachedTextures = new(64);

        // Sorting key per texture to make batching ez.
        private readonly Dictionary<Texture, int> _textureSortKeys = new();

        // CPU-side render data for each decal chunk. As we don't persist chunk textures in VRAM this just makes it easier
        // to re-use the data every frame.
        private readonly Dictionary<(EntityUid Grid, Vector2i Chunk), ChunkRenderCache> _chunkCache = new();

        private readonly List<StaticRenderEntry> _staticEntries = new();
        private readonly List<DynamicRenderEntry> _dynamicEntries = new();
        private readonly List<WorldTextureRect> _quadBuffer = new();

        public DecalOverlay(
            SpriteSystem sprites,
            IEntityManager entManager,
            IPrototypeManager prototypeManager)
        {
            _sprites = sprites;
            _entManager = entManager;
            _prototypeManager = prototypeManager;
        }

        public void InvalidateChunk(EntityUid gridUid, Vector2i chunk)
        {
            _chunkCache.Remove((gridUid, chunk));
        }

        public void InvalidateChunks(EntityUid gridUid, IEnumerable<Vector2i> chunks)
        {
            foreach (var chunk in chunks)
            {
                InvalidateChunk(gridUid, chunk);
            }
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (args.MapId == MapId.Nullspace)
                return;

            var owner = Grid.Owner;

            if (!_entManager.TryGetComponent(owner, out DecalGridComponent? decalGrid) ||
                !_entManager.TryGetComponent(owner, out TransformComponent? xform))
            {
                return;
            }

            if (xform.MapID != args.MapId)
                return;

            // Shouldn't need to clear cached textures unless the prototypes get reloaded.
            var handle = args.WorldHandle;
            var xformSystem = _entManager.System<TransformSystem>();
            var eyeAngle = args.Viewport.Eye?.Rotation ?? Angle.Zero;

            var gridAABB = xformSystem.GetInvWorldMatrix(xform).TransformBox(args.WorldBounds.Enlarged(1f));
            var chunkEnumerator = new ChunkIndicesEnumerator(gridAABB, SharedDecalSystem.ChunkSize);
            _staticEntries.Clear();
            _dynamicEntries.Clear();

            // Pull render entries from visible cached chunks, then sort the combined visible set. This keeps z-ordering
            // correct across chunk boundaries instead of drawing one chunk at a time.
            while (chunkEnumerator.MoveNext(out var index))
            {
                if (!decalGrid.ChunkCollection.ChunkCollection.TryGetValue(index.Value, out var chunk))
                    continue;

                var cache = GetChunkCache(owner, index.Value, chunk);

                foreach (var entry in cache.StaticEntries)
                {
                    if (!gridAABB.Contains(entry.Coordinates))
                        continue;

                    _staticEntries.Add(entry);
                }

                foreach (var entry in cache.DynamicEntries)
                {
                    if (!gridAABB.Contains(entry.Decal.Coordinates))
                        continue;

                    _dynamicEntries.Add(entry);
                }
            }

            if (_staticEntries.Count == 0 && _dynamicEntries.Count == 0)
                return;

            _staticEntries.Sort(CompareStaticEntries);
            _dynamicEntries.Sort(CompareDynamicEntries);

            var (_, worldRot, worldMatrix) = xformSystem.GetWorldPositionRotationMatrix(xform);
            handle.SetTransform(worldMatrix);
            var worldAngle = eyeAngle + worldRot;

            var staticIndex = 0;
            var dynamicIndex = 0;

            // Draw static / dynamic 1 layer at a time.
            // This will essentially flip-flop between the two. If you squint hard enough it's how the clyde sprite renderer does it.
            var statEntries = CollectionsMarshal.AsSpan(_staticEntries);
            var dynEntries = CollectionsMarshal.AsSpan(_dynamicEntries);

            while (staticIndex < statEntries.Length || dynamicIndex < dynEntries.Length)
            {
                int zIndex;

                if (staticIndex >= statEntries.Length)
                {
                    zIndex = dynEntries[dynamicIndex].ZIndex;
                }
                else if (dynamicIndex >= dynEntries.Length)
                {
                    zIndex = statEntries[staticIndex].ZIndex;
                }
                else
                {
                    zIndex = Math.Min(statEntries[staticIndex].ZIndex, dynEntries[dynamicIndex].ZIndex);
                }

                staticIndex = DrawStaticZ(handle, statEntries, staticIndex, zIndex);
                dynamicIndex = DrawDynamicZ(handle, dynEntries, dynamicIndex, zIndex, worldAngle);
            }

            handle.SetTransform(Matrix3x2.Identity);
        }

        private ChunkRenderCache GetChunkCache(EntityUid gridUid, Vector2i chunkIndex, DecalGridComponent.DecalChunk chunk)
        {
            var key = (gridUid, chunkIndex);

            if (_chunkCache.TryGetValue(key, out var cache) && ReferenceEquals(cache.Chunk, chunk))
                return cache;

            cache = BuildChunkCache(chunk);
            _chunkCache[key] = cache;
            return cache;
        }

        private ChunkRenderCache BuildChunkCache(DecalGridComponent.DecalChunk chunk)
        {
            var cache = new ChunkRenderCache(chunk);

            foreach (var (id, decal) in chunk.Decals)
            {
                if (!TryGetCachedTexture(decal.Id, out var texture))
                    continue;

                if (texture.SnapCardinals)
                {
                    // Snap-cardinal decals depend on the current eye/grid rotation so we can't just cache the Frame0 texture.
                    cache.DynamicEntries.Add(new DynamicRenderEntry(id, decal, texture.Texture, decal.ZIndex));
                    continue;
                }

                var entry = new StaticRenderEntry(
                    id,
                    decal.Coordinates,
                    decal.ZIndex,
                    texture.Texture,
                    texture.TextureSortKey,
                    BuildTextureQuad(texture.Texture, decal));

                cache.StaticEntries.Add(entry);
            }

            cache.StaticEntries.Sort(CompareStaticEntries);
            cache.DynamicEntries.Sort(CompareDynamicEntries);

            return cache;
        }

        private bool TryGetCachedTexture(string id, out CachedTexture cache)
        {
            if (_cachedTextures.TryGetValue(id, out cache))
                return true;

            // Nothing to cache, someone messed up.
            if (!_prototypeManager.TryIndex<DecalPrototype>(id, out var decalProto))
                return false;

            var texture = _sprites.Frame0(decalProto.Sprite);
            cache = new CachedTexture(
                texture,
                GetTextureSortKey(texture),
                decalProto.SnapCardinals);

            _cachedTextures[id] = cache;
            return true;
        }

        private int DrawStaticZ(DrawingHandleWorld handle, ReadOnlySpan<StaticRenderEntry> statEntries, int index, int zIndex)
        {
            while (index < _staticEntries.Count)
            {
                var entry = statEntries[index];

                if (entry.ZIndex != zIndex)
                    break;

                var texture = entry.Texture;
                _quadBuffer.Clear();

                // Batch consecutive static decals that share z-index and texture. We must stop before the next texture
                // so Clyde receives one texture per batch.
                while (index < _staticEntries.Count)
                {
                    entry = statEntries[index];

                    if (entry.ZIndex != zIndex || !ReferenceEquals(entry.Texture, texture))
                        break;

                    _quadBuffer.Add(entry.Quad);
                    index++;
                }

                handle.DrawTextureRects(texture, CollectionsMarshal.AsSpan(_quadBuffer));
            }

            return index;
        }

        private static int DrawDynamicZ(
            DrawingHandleWorld handle,
            ReadOnlySpan<DynamicRenderEntry> entries,
            int index,
            int zIndex,
            Angle worldAngle)
        {
            var cardinal = worldAngle.GetCardinalDir().ToAngle();

            while (index < entries.Length && entries[index].ZIndex == zIndex)
            {
                var (_, decal, texture, _) = entries[index];
                var angle = decal.Angle - cardinal;

                if (angle.Equals(Angle.Zero))
                    handle.DrawTexture(texture, decal.Coordinates, decal.Color);
                else
                    handle.DrawTexture(texture, decal.Coordinates, angle, decal.Color);

                index++;
            }

            return index;
        }

        private static WorldTextureRect BuildTextureQuad(Texture texture, Decal decal)
        {
            var size = texture.Size / (float) EyeManager.PixelsPerMeter;
            var quad = Box2.FromDimensions(decal.Coordinates, size);
            var rotated = new Box2Rotated(quad, decal.Angle, quad.Center);

            return new WorldTextureRect(rotated, decal.Color);
        }

        private int GetTextureSortKey(Texture texture)
        {
            if (_textureSortKeys.TryGetValue(texture, out var key))
                return key;

            key = _textureSortKeys.Count;
            _textureSortKeys[texture] = key;
            return key;
        }

        private static int CompareStaticEntries(StaticRenderEntry x, StaticRenderEntry y)
        {
            var zComp = x.ZIndex.CompareTo(y.ZIndex);

            if (zComp != 0)
                return zComp;

            var textureComp = x.TextureSortKey.CompareTo(y.TextureSortKey);

            if (textureComp != 0)
                return textureComp;

            return x.Id.CompareTo(y.Id);
        }

        private static int CompareDynamicEntries(DynamicRenderEntry x, DynamicRenderEntry y)
        {
            var zComp = x.ZIndex.CompareTo(y.ZIndex);

            if (zComp != 0)
                return zComp;

            return x.Id.CompareTo(y.Id);
        }

        private sealed class ChunkRenderCache(DecalGridComponent.DecalChunk chunk)
        {
            public readonly DecalGridComponent.DecalChunk Chunk = chunk;
            public readonly List<StaticRenderEntry> StaticEntries = new(chunk.Decals.Count);
            public readonly List<DynamicRenderEntry> DynamicEntries = new();
        }

        private readonly record struct CachedTexture(
            Texture Texture,
            int TextureSortKey,
            bool SnapCardinals);

        /// <summary>
        /// Decals that don't use SnapCardinal / change texture with eye rotation.
        /// </summary>
        private readonly record struct StaticRenderEntry(
            uint Id,
            Vector2 Coordinates,
            int ZIndex,
            Texture Texture,
            int TextureSortKey,
            WorldTextureRect Quad);

        /// <summary>
        /// Decals that may have their texture changed based on eye angle.
        /// </summary>
        private readonly record struct DynamicRenderEntry(
            uint Id,
            Decal Decal,
            Texture Texture,
            int ZIndex);
    }
}

