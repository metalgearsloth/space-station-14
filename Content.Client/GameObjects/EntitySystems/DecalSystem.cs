using System.Collections.Generic;
using Content.Client.Utility;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class DecalSystem : SharedDecalSystem
    {
        [Dependency] private IResourceCache _resourceCache = default!;
        
        // These shouldn't change between round restarts
        public Dictionary<string, Texture> Textures { get; private set; }

        private DecalOverlay _overlay;
        
        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<DecalMessage>(HandleDecalMessage);

            var overlayManager = IoCManager.Resolve<IOverlayManager>();
            _overlay = new DecalOverlay();
            overlayManager.AddOverlay(_overlay);
        }

        protected override void HandleDecalMessage(DecalMessage message)
        {
            base.HandleDecalMessage(message);
            if (!Textures.ContainsKey(message.TexturePath))
            {
                Textures[message.TexturePath] = _resourceCache.GetTexture(message.TexturePath);
            }
        }

        public IEnumerable<Decal> GetDecals(GridId gridId)
        {
            if (!Decals.TryGetValue(gridId, out var chunks))
            {
                yield break;
            }

            // TODO: Only get relevant
            foreach (var (_, chunk) in chunks)
            {
                foreach (var decal in chunk.Decals)
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
            var viewport = _eyeManager.GetWorldViewport().Enlarged(1.1f);

            foreach (var grid in _mapManager.FindGridsIntersecting(_eyeManager.CurrentMap, viewport))
            {
                foreach (var decal in _decalSystem.GetDecals(grid.Index))
                {
                    var worldPos = grid.LocalToWorld(decal.Coordinates.Position);
                    if (!viewport.Contains(worldPos))
                        continue;
                    
                    var texture = _decalSystem.Textures[decal.TexturePath];
                    worldHandle.DrawTexture(texture, worldPos);
                }
            }
        }
    }
}