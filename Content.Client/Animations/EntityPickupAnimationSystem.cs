using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client.Animations;

/// <summary>
/// Handles presentation-only pickup clones without changing authored sprite offsets.
/// </summary>
public sealed partial class EntityPickupAnimationSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private ZLevelPresentationSystem _zPresentation = default!;
    [Dependency] private ZLevelSystem _zLevels = default!;

    /// <summary>
    /// Animates a clone of an entity between two live render coordinate spaces before deleting it.
    /// </summary>
    public void AnimateEntityPickup(
        EntityUid uid,
        EntityCoordinates initial,
        EntityCoordinates final,
        Angle initialAngle,
        EntityUid? target = null)
    {
        if (Deleted(uid) || !initial.IsValid(EntityManager) || !final.IsValid(EntityManager))
            return;

        var metadata = MetaData(uid);
        if (IsPaused(uid, metadata))
            return;

        if (!TryComp(uid, out SpriteComponent? sourceSprite))
        {
            Log.Error("Entity ({0}) couldn't be animated for pickup since it doesn't have a {1}!",
                metadata.EntityName,
                nameof(SpriteComponent));
            return;
        }

        var initialMap = _transform.ToMapCoordinates(initial);
        var finalMap = _transform.ToMapCoordinates(final);
        if (initialMap.MapId == MapId.Nullspace || finalMap.MapId == MapId.Nullspace)
            return;

        var referenceMap = _maps.GetMapOrInvalid(initialMap.MapId);
        if (referenceMap == EntityUid.Invalid)
            return;

        // The clone is map-parented. Each endpoint is resampled below, so neither a moving source grid nor a
        // moving destination grid is frozen by a one-time conversion into the source parent's local coordinates.
        var clone = Spawn("clientsideclone", initialMap);
        var pickupAnimation = EnsureComp<EntityPickupAnimationComponent>(clone);
        _metaData.SetEntityName(clone, metadata.EntityName);

        var sprite = Comp<SpriteComponent>(clone);
        _sprite.CopySprite((uid, sourceSprite), (clone, sprite));
        _sprite.SetVisible((clone, sprite), true);

        var despawn = EnsureComp<TimedDespawnComponent>(clone);
        despawn.Lifetime = 0.25f;

        var clonePresentation = EnsureComp<ZLevelPresentationComponent>(clone);
        pickupAnimation.StartTime = _timing.CurTime;
        pickupAnimation.Duration = 0.125f;
        pickupAnimation.StartCoordinates = initial;
        pickupAnimation.EndCoordinates = final;
        pickupAnimation.Target = target;
        pickupAnimation.ReferenceMap = referenceMap;
        pickupAnimation.ReferenceDepth = GetMapDepth(initialMap.MapId);
        pickupAnimation.StartAbsoluteZ = pickupAnimation.ReferenceDepth +
            (TryComp(uid, out ZLevelPresentationComponent? sourcePresentation)
                ? sourcePresentation.LocalHeight
                : 0f);
        pickupAnimation.InitialAngle = initialAngle;

        UpdatePresentation(clone, pickupAnimation, clonePresentation, 0f);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<EntityPickupAnimationComponent, ZLevelPresentationComponent>();
        while (query.MoveNext(out var uid, out var animation, out var presentation))
        {
            var elapsed = (float) (_timing.CurTime - animation.StartTime).TotalSeconds;
            var progress = animation.Duration <= 0f
                ? 1f
                : Math.Clamp(elapsed / animation.Duration, 0f, 1f);
            UpdatePresentation(uid, animation, presentation, progress);

            if (progress >= 1f)
                QueueDel(uid);
        }
    }

    internal void UpdatePresentation(
        EntityUid uid,
        EntityPickupAnimationComponent animation,
        ZLevelPresentationComponent presentation,
        float progress)
    {
        var projectionOffset = _zLevels.TryGetMapData(animation.ReferenceMap, out _, out var network)
            ? network.ProjectionOffset
            : Vector2.Zero;
        var source = GetCoordinateEndpoint(
            animation.StartCoordinates,
            animation.StartAbsoluteZ,
            animation.InitialAngle,
            animation.ReferenceDepth,
            projectionOffset);
        var target = GetTargetEndpoint(animation, animation.ReferenceDepth, projectionOffset);
        var absoluteZ = MathHelper.Lerp(source.AbsoluteZ, target.AbsoluteZ, progress);
        var projectedPosition = Vector2.Lerp(source.ProjectedPosition, target.ProjectedPosition, progress);
        var canonicalPosition = ZLevelProjection.Unproject(
            projectedPosition,
            absoluteZ,
            animation.ReferenceDepth,
            projectionOffset);

        _transform.SetLocalPositionNoLerp(uid, canonicalPosition);
        _transform.SetLocalRotationNoLerp(uid, Angle.Lerp(source.Rotation, target.Rotation, progress));
        _zPresentation.SetLocalHeight((uid, presentation), absoluteZ - animation.ReferenceDepth);

        // This clone is already driven by one presentation timeline. Do not interpolate its authored samples a
        // second time in the network render-pose timeline.
        _transform.SnapRenderPose(uid);
    }

    private PickupEndpoint GetTargetEndpoint(
        EntityPickupAnimationComponent animation,
        int referenceDepth,
        Vector2 projectionOffset)
    {
        var finalMap = _transform.ToMapCoordinates(animation.EndCoordinates);
        var targetAbsoluteZ = animation.Target is { } target && Exists(target)
            ? _transform.GetRenderWorldPose(target).AbsoluteZ
            : GetMapDepth(finalMap.MapId);
        return GetCoordinateEndpoint(
            animation.EndCoordinates,
            targetAbsoluteZ,
            animation.InitialAngle,
            referenceDepth,
            projectionOffset);
    }

    private PickupEndpoint GetCoordinateEndpoint(
        EntityCoordinates coordinates,
        float absoluteZ,
        Angle localRotation,
        int referenceDepth,
        Vector2 projectionOffset)
    {
        if (!coordinates.IsValid(EntityManager) || !Exists(coordinates.EntityId))
            return default;

        var parentPose = _transform.GetRenderWorldPose(coordinates.EntityId);
        var canonicalPosition = parentPose.CanonicalPosition + parentPose.Rotation.RotateVec(coordinates.Position);
        return new PickupEndpoint(
            ZLevelProjection.Project(canonicalPosition, absoluteZ, referenceDepth, projectionOffset),
            parentPose.Rotation + localRotation,
            absoluteZ);
    }

    private int GetMapDepth(MapId mapId)
    {
        var mapUid = _maps.GetMapOrInvalid(mapId);
        return _zLevels.TryGetMapDepth(mapUid, out var depth) ? depth.Value : 0;
    }

    private readonly record struct PickupEndpoint(Vector2 ProjectedPosition, Angle Rotation, float AbsoluteZ);
}
