using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Light;

public sealed class CloudOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public CloudOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = AfterLightTargetOverlay.ContentZIndex + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId,
            args.WorldBounds,
            ref _grids);

        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldBounds = args.WorldBounds;
        var targetSize = viewport.LightRenderTarget.Size;

        if (_target?.Size != targetSize)
        {
            _target = _clyde
                .CreateRenderTarget(targetSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                    name: "sun-shadow-target");

            if (_blurTarget?.Size != targetSize)
            {
                _blurTarget = _clyde
                    .CreateRenderTarget(targetSize, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "sun-shadow-blur");
            }
        }

        var lightScale = viewport.LightRenderTarget.Size / (Vector2)viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        foreach (var grid in _grids)
        {
            if (!_entManager.TryGetComponent(grid.Owner, out SunShadowComponent? sun))
            {
                continue;
            }

            var direction = sun.Direction;
            var alpha = Math.Clamp(sun.Alpha, 0f, 1f);

            // Nowhere to cast to so ignore it.
            if (direction.Equals(Vector2.Zero) || alpha == 0f)
                continue;

            // Feature todo: dynamic shadows for mobs and trees. Also ideally remove the fake tree shadows.
            // TODO: Jittering still not quite perfect

            var expandedBounds = worldBounds.Enlarged(direction.Length() + 0.01f);
            _shadows.Clear();

            // Draw shadow polys to stencil
            args.WorldHandle.RenderInRenderTarget(_target,
                () =>
                {
                    var invMatrix =
                        _target.GetWorldToLocalMatrix(eye, scale);
                    var indices = new Vector2[PhysicsConstants.MaxPolygonVertices * 2];

                    // Go through shadows in range.

                    // For each one we:
                    // - Get the original vertices.
                    // - Extrapolate these along the sun direction.
                    // - Combine the above into 1 single polygon to draw.

                    // Note that this is range-limited for accuracy; if you set it too high it will clip through walls or other undesirable entities.
                    // This is probably not noticeable most of the time but if you want something "accurate" you'll want to code a solution.
                    // Ideally the CPU would have its own shadow-map copy that we could just ray-cast each vert into though
                    // You might need to batch verts or the likes as this could get expensive.
                    _lookup.GetEntitiesIntersecting(mapId, expandedBounds, _shadows);

                    foreach (var ent in _shadows)
                    {
                        var xform = _entManager.GetComponent<TransformComponent>(ent.Owner);
                        var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform);
                        // Need no rotation on matrix as sun shadow direction doesn't care.
                        var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
                        var renderMatrix = Matrix3x2.Multiply(worldMatrix, invMatrix);
                        var pointCount = ent.Comp.Points.Length;

                        Array.Copy(ent.Comp.Points, indices, pointCount);

                        for (var i = 0; i < pointCount; i++)
                        {
                            // Update point based on entity rotation.
                            indices[i] = worldRot.RotateVec(indices[i]);

                            // Add the offset point by the sun shadow direction.
                            indices[pointCount + i] = indices[i] + direction;
                        }

                        var points = PhysicsHull.ComputePoints(indices, pointCount * 2);
                        worldHandle.SetTransform(renderMatrix);

                        worldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Color.White);
                    }
                },
                Color.Transparent);
    }
}
