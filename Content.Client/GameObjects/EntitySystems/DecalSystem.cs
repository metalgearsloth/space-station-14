using System;
using System.Collections.Generic;
using Content.Client.Utility;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.Player;
using Robust.Client.Utility;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class DecalSystem : SharedDecalSystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        // These shouldn't change between round restarts
        public Dictionary<SpriteSpecifier.Rsi, Texture> Textures { get; } = new Dictionary<SpriteSpecifier.Rsi, Texture>();

        public Dictionary<Decal, Texture> DecalTexture { get; } = new Dictionary<Decal, Texture>();

        private DecalOverlay _overlay;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<DecalOverlayMessage>(HandleDecalOverlayMessage);

            var overlayManager = IoCManager.Resolve<IOverlayManager>();
            _overlay = new DecalOverlay();
            overlayManager.AddOverlay(_overlay);
        }

        private Vector2i GetChunkIndices(EntityCoordinates coordinates)
        {
            var floored = new Vector2i((int) Math.Floor(coordinates.X), (int) Math.Floor(coordinates.Y));
            return GetChunkIndices(floored);
        }

        private void HandleDecalOverlayMessage(DecalOverlayMessage message)
        {
            var currentTick = GameTiming.CurTick;

            foreach (var (grid, decals) in message.Decals)
            {
                if (!Decals.TryGetValue(grid, out var existingChunks))
                {
                    existingChunks = new Dictionary<Vector2i, DecalChunk>();
                    Decals[grid] = existingChunks;
                }

                foreach (var decal in decals)
                {
                    var chunkIndices = GetChunkIndices(decal.Coordinates);

                    if (!existingChunks.TryGetValue(chunkIndices, out var chunk))
                    {
                        chunk = new DecalChunk(grid, chunkIndices, currentTick);
                        existingChunks[chunkIndices] = chunk;
                    }

                    chunk.AddDecal(currentTick, decal);
                    var rsi = new SpriteSpecifier.Rsi(decal.RsiPath, decal.State);

                    if (!Textures.TryGetValue(rsi, out var texture))
                    {
                        texture = rsi.Frame0();
                        Textures[rsi] = texture;
                    }

                    DecalTexture[decal] = texture;
                }
            }
        }

        public IEnumerable<Decal> GetDecals(GridId gridId)
        {
            var playerEntity = _playerManager.LocalPlayer?.ControlledEntity;

            if (playerEntity == null)
                yield break;

            foreach (var chunk in GetChunksInRange(playerEntity))
            {
                foreach (var (decal, _) in chunk.Decals)
                {
                    yield return decal;
                }
            }
        }
    }

    internal sealed class DecalOverlay : Overlay
    {
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;

        private DecalSystem _decalSystem;

        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        public DecalOverlay() : base(nameof(DecalOverlay))
        {
            _decalSystem = EntitySystem.Get<DecalSystem>();
            IoCManager.InjectDependencies(this);
        }

        protected override void Draw(DrawingHandleBase handle, OverlaySpace currentSpace)
        {
            var worldHandle = (DrawingHandleWorld) handle;
            // TODO: This is hacky if the crayons can be larger than a tile.
            var viewport = _eyeManager.GetWorldViewport().Enlarged(1.1f);

            foreach (var grid in _mapManager.FindGridsIntersecting(_eyeManager.CurrentMap, viewport))
            {
                foreach (var decal in _decalSystem.GetDecals(grid.Index))
                {
                    // DrawTexture uses the position as bottom-left so need to centre it.
                    var texture = _decalSystem.DecalTexture[decal];
                    var offset = new Vector2((float) texture.Width / EyeManager.PixelsPerMeter / 2, (float) texture.Height / EyeManager.PixelsPerMeter / 2);
                    var worldPos = grid.LocalToWorld(decal.Coordinates.Position) - offset;

                    if (!viewport.Contains(worldPos))
                        continue;

                    worldHandle.DrawTexture(texture, worldPos, decal.Color);
                }
            }
        }
    }
}
