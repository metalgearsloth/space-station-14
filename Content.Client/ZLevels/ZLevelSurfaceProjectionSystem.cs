using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client.ZLevels;

/// <summary>
/// Shared client projection and hit-testing for authored support surfaces. Presentation, picking, and indicators use
/// this one calculation so moving/rotating providers cannot acquire a separate visual and interaction footprint.
/// </summary>
public sealed partial class ZLevelSurfaceProjectionSystem : EntitySystem
{
    [Dependency] private TransformSystem _transforms = default!;
    [Dependency] private ZLevelPhysicsSystem _zPhysics = default!;
    [Dependency] private ZLevelSystem _zLevels = default!;

    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<ZLevelHighGroundComponent> _highGroundQuery = default!;

    public bool TryGetProjectedSurface(
        Entity<ZLevelHighGroundComponent?> provider,
        EntityUid viewedMap,
        Span<Vector2> projected,
        out int count,
        out float averageHeight)
    {
        count = 0;
        averageHeight = 0f;
        if (projected.Length < 4 ||
            !_highGroundQuery.Resolve(provider.Owner, ref provider.Comp, false) ||
            !_xformQuery.TryComp(provider.Owner, out var xform))
        {
            return false;
        }

        Span<ZLevelSupportPoint> support = stackalloc ZLevelSupportPoint[4];
        if (!_zPhysics.TryGetSupportSurfacePoints((provider.Owner, provider.Comp), support, out count))
            return false;

        var pose = _transforms.GetRenderWorldPoseForLayer(provider.Owner, viewedMap, xform);
        var inverseSimulation = _transforms.GetInvWorldMatrix(xform);
        for (var i = 0; i < count; i++)
        {
            averageHeight += support[i].AbsoluteHeight;
            var localPoint = Vector2.Transform(support[i].CanonicalPosition, inverseSimulation);
            projected[i] = pose.Position +
                           pose.Rotation.RotateVec(localPoint) +
                           pose.ProjectionOffset * (support[i].AbsoluteHeight - pose.AbsoluteZ);
        }

        averageHeight /= count;
        return true;
    }

    public bool TryGetSurfaceLayer(EntityUid provider, float averageHeight, out EntityUid layer)
    {
        layer = EntityUid.Invalid;
        if (!_xformQuery.TryComp(provider, out var xform) ||
            !_zLevels.TryGetMapData(xform.MapUid ?? EntityUid.Invalid, out var source, out _) ||
            !_zLevels.TryGetMapAtDepth(source.Network, (int) MathF.Round(averageHeight), out var target))
        {
            return false;
        }

        layer = target.Value;
        return true;
    }

    public static bool ContainsPoint(ReadOnlySpan<Vector2> polygon, Vector2 point)
    {
        var inside = false;
        for (var i = 0; i < polygon.Length; i++)
        {
            var previous = i == 0 ? polygon.Length - 1 : i - 1;
            var a = polygon[previous];
            var b = polygon[i];

            var segment = b - a;
            var lengthSquared = segment.LengthSquared();
            if (lengthSquared > 0f)
            {
                var t = Math.Clamp(Vector2.Dot(point - a, segment) / lengthSquared, 0f, 1f);
                if (Vector2.DistanceSquared(point, a + segment * t) <= 0.000001f)
                    return true;
            }

            if ((a.Y > point.Y) == (b.Y > point.Y))
                continue;

            var crossingX = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < crossingX)
                inside = !inside;
        }

        return inside;
    }
}
