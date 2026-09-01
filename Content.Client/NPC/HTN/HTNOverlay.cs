using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client.NPC.HTN;

public sealed class HTNOverlay : Overlay
{
    private readonly IEntityManager _entManager = default!;
    private readonly Font _font = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public HTNOverlay(IEntityManager entManager, IResourceCache resourceCache)
    {
        _entManager = entManager;
        _font = new VectorFont(resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;

        var query = _entManager.AllEntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (string.IsNullOrEmpty(comp.DebugText))
                continue;

            if (!args.TryGetEntityPresentedView(uid, out var presented))
                continue;

            if (!args.WorldAABB.Contains(presented.Position))
                continue;

            var screenPos = args.ViewportControl.WorldToScreen(presented.Position);
            handle.DrawString(
                _font,
                screenPos + new Vector2(0, 10f),
                comp.DebugText,
                Color.White.WithAlpha(presented.Opacity));
        }
    }
}
