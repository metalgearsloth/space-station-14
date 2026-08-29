using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.ZLevels;

/// <summary>
/// Debug overlay for diagnosing thrown entity z-level render interpolation.
/// </summary>
public sealed partial class ZThrowDebugOverlay : Overlay
{
    private const int FontSize = 10;
    private const int MaxTrailPoints = 36;
    private const int TrailLifetimeTicks = 90;

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SharedMapSystem _maps;
    private readonly SharedTransformSystem _transform;
    private readonly ZLevelPhysicsVisualSystem _zVisuals;
    private readonly EntityQuery<PhysicsComponent> _physicsQuery;
    private readonly EntityQuery<ThrownItemComponent> _thrownQuery;
    private readonly EntityQuery<ZLevelMapComponent> _zMapQuery;
    private readonly Font _font;
    private readonly StringBuilder _text = new();
    private readonly Dictionary<EntityUid, List<ZThrowTrailPoint>> _trails = new();
    private readonly List<EntityUid> _staleTrails = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public ZThrowDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), FontSize);
        _maps = _entityManager.System<SharedMapSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _zVisuals = _entityManager.System<ZLevelPhysicsVisualSystem>();
        _physicsQuery = _entityManager.GetEntityQuery<PhysicsComponent>();
        _thrownQuery = _entityManager.GetEntityQuery<ThrownItemComponent>();
        _zMapQuery = _entityManager.GetEntityQuery<ZLevelMapComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.WorldSpace:
                DrawWorld(args);
                break;
            case OverlaySpace.ScreenSpace:
                DrawScreen(args);
                break;
        }
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        if (!TryGetRenderZLevel(args.MapId, out var renderZLevel))
            return;

        PruneTrails();

        var handle = args.WorldHandle;
        foreach (var (_, trail) in _trails)
        {
            for (var i = 1; i < trail.Count; i++)
            {
                var previous = trail[i - 1];
                var current = trail[i];
                if (previous.RenderZLevel != renderZLevel || current.RenderZLevel != renderZLevel)
                    continue;

                handle.DrawLine(previous.Position, current.Position, Color.Cyan.WithAlpha(0.75f));
            }
        }

        var query = _entityManager.EntityQueryEnumerator<ZLevelPhysicsComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhysics, out var xform, out var sprite))
        {
            if (!TryGetSample(uid, zPhysics, xform, sprite, args.MapId, args.Viewport.Eye?.Rotation ?? Angle.Zero, out var sample) ||
                !IsInteresting(sample) ||
                !args.WorldAABB.Enlarged(3f).Contains(sample.RenderedWorldPosition))
            {
                continue;
            }

            handle.DrawCircle(sample.TransformWorldPosition, 0.08f, Color.Yellow, false);
            handle.DrawCircle(sample.RenderedWorldPosition, 0.11f, Color.Cyan, false);
            handle.DrawLine(sample.TransformWorldPosition, sample.RenderedWorldPosition, Color.Cyan.WithAlpha(0.65f));

            if (sample.LinearVelocity != Vector2.Zero)
                handle.DrawLine(sample.TransformWorldPosition, sample.TransformWorldPosition + sample.LinearVelocity.Normalized() * 0.5f, Color.Orange);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var query = _entityManager.EntityQueryEnumerator<ZLevelPhysicsComponent, TransformComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var zPhysics, out var xform, out var sprite))
        {
            if (!TryGetSample(uid, zPhysics, xform, sprite, args.MapId, args.Viewport.Eye?.Rotation ?? Angle.Zero, out var sample) ||
                !IsInteresting(sample) ||
                !args.WorldAABB.Enlarged(3f).Contains(sample.RenderedWorldPosition))
            {
                continue;
            }

            AddTrail(sample);

            var screenPos = args.ViewportControl.WorldToScreen(sample.RenderedWorldPosition) + new Vector2(12f, -16f);
            var bodyStatus = sample.HasPhysics ? sample.BodyStatus.ToString() : "none";
            var nextPosition = sample.Transform.NextPosition?.ToString() ?? "none";
            var nextRotation = sample.Transform.NextRotation?.ToString() ?? "none";

            _text.Clear();
            _text.AppendLine(_entityManager.ToPrettyString(sample.Uid).ToString());
            _text.Append("tick ");
            _text.Append(_timing.CurTick);
            _text.Append('+');
            _text.Append(GetTickFraction().ToString("0.00"));
            _text.Append(" thrown=");
            _text.Append(sample.Thrown);
            _text.Append(" disabled=");
            _text.Append(sample.ZPhysics.Disabled);
            _text.AppendLine();
            _text.Append("src/render ");
            _text.Append(sample.RenderData.SourceZLevel);
            _text.Append('/');
            _text.Append(sample.RenderData.RenderZLevel);
            _text.Append(" absZ=");
            _text.Append(sample.RenderData.RenderAbsolutePosition.ToString("0.000"));
            _text.Append(" localZ=");
            _text.Append(sample.ZPhysics.LocalPosition.ToString("0.000"));
            _text.Append(" zVel=");
            _text.Append(sample.ZPhysics.Velocity.ToString("0.00"));
            _text.AppendLine();
            _text.Append("body=");
            _text.Append(bodyStatus);
            _text.Append(" lin=");
            _text.Append(FormatVector(sample.LinearVelocity));
            _text.Append(" ang=");
            _text.Append(sample.AngularVelocity.ToString("0.00"));
            _text.AppendLine();
            _text.Append("pos=");
            _text.Append(FormatVector(sample.TransformWorldPosition));
            _text.Append(" render=");
            _text.Append(FormatVector(sample.RenderedWorldPosition));
            _text.AppendLine();
            _text.Append("map=");
            _text.Append(sample.Transform.MapUid);
            _text.Append(" parent=");
            _text.Append(sample.Transform.ParentUid);
            _text.AppendLine();
            _text.Append("lerp=");
            _text.Append(sample.Transform.ActivelyLerping);
            _text.Append(" pred=");
            _text.Append(sample.Transform.PredictedLerp);
            _text.Append(" last=");
            _text.Append(sample.Transform.LastLerp);
            _text.AppendLine();
            _text.Append("prev=");
            _text.Append(FormatVector(sample.Transform.PrevPosition));
            _text.Append(" cur=");
            _text.Append(FormatVector(sample.Transform.LocalPosition));
            _text.Append(" next=");
            _text.Append(nextPosition);
            _text.AppendLine();
            _text.Append("prevRot=");
            _text.Append(sample.Transform.PrevRotation);
            _text.Append(" curRot=");
            _text.Append(sample.Transform.LocalRotation);
            _text.Append(" nextRot=");
            _text.Append(nextRotation);

            args.ScreenHandle.DrawString(_font, screenPos, _text.ToString(), Color.Cyan);
        }
    }

    private bool TryGetSample(
        EntityUid uid,
        ZLevelPhysicsComponent zPhysics,
        TransformComponent xform,
        SpriteComponent sprite,
        MapId renderMap,
        Angle viewRotation,
        out ZThrowDebugSample sample)
    {
        sample = default;

        var renderData = _zVisuals.GetVisualRenderData(uid, zPhysics, xform);
        if (!ShouldDrawOnRenderMap(renderMap, renderData, xform))
            return false;

        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(xform);
        var renderedWorldPosition = _zVisuals.GetSpriteRenderWorldPosition(
            worldPosition,
            sprite.Offset,
            renderData,
            worldRotation,
            viewRotation,
            sprite.NoRotation);

        var hasPhysics = _physicsQuery.TryComp(uid, out var physics);
        sample = new ZThrowDebugSample(
            uid,
            zPhysics,
            xform,
            hasPhysics,
            physics?.BodyStatus ?? BodyStatus.OnGround,
            physics?.LinearVelocity ?? Vector2.Zero,
            physics?.AngularVelocity ?? 0f,
            renderData,
            _thrownQuery.HasComp(uid),
            worldPosition,
            renderedWorldPosition);

        return true;
    }

    private bool ShouldDrawOnRenderMap(MapId renderMap, ZLevelVisualRenderData renderData, TransformComponent xform)
    {
        var renderMapUid = _maps.GetMapOrInvalid(renderMap);
        if (_zMapQuery.TryComp(renderMapUid, out var renderZMap))
            return renderData.RenderZLevel == renderZMap.Depth;

        return xform.MapID == renderMap;
    }

    private bool TryGetRenderZLevel(MapId renderMap, out int renderZLevel)
    {
        var renderMapUid = _maps.GetMapOrInvalid(renderMap);
        if (_zMapQuery.TryComp(renderMapUid, out var renderZMap))
        {
            renderZLevel = renderZMap.Depth;
            return true;
        }

        renderZLevel = 0;
        return false;
    }

    private static bool IsInteresting(ZThrowDebugSample sample)
    {
        return sample.Thrown ||
               sample.ZPhysics.Disabled ||
               MathF.Abs(sample.ZPhysics.Velocity) > 0.001f ||
               MathF.Abs(sample.ZPhysics.LocalPosition) > 0.001f ||
               sample is { HasPhysics: true, BodyStatus: BodyStatus.InAir } ||
               sample.Transform.ActivelyLerping;
    }

    private void AddTrail(ZThrowDebugSample sample)
    {
        if (!_trails.TryGetValue(sample.Uid, out var trail))
        {
            trail = new List<ZThrowTrailPoint>(MaxTrailPoints);
            _trails[sample.Uid] = trail;
        }

        if (trail.Count == 0 ||
            Vector2.DistanceSquared(trail[^1].Position, sample.RenderedWorldPosition) > 0.0001f ||
            trail[^1].RenderZLevel != sample.RenderData.RenderZLevel)
        {
            trail.Add(new ZThrowTrailPoint(sample.RenderedWorldPosition, sample.RenderData.RenderZLevel, _timing.CurTick));
        }

        while (trail.Count > MaxTrailPoints)
            trail.RemoveAt(0);
    }

    private void PruneTrails()
    {
        _staleTrails.Clear();
        foreach (var (uid, trail) in _trails)
        {
            if (trail.Count == 0 ||
                _timing.CurTick.Value - trail[^1].Tick.Value > TrailLifetimeTicks ||
                !_entityManager.EntityExists(uid))
            {
                _staleTrails.Add(uid);
            }
        }

        foreach (var uid in _staleTrails)
            _trails.Remove(uid);
    }

    private float GetTickFraction()
    {
        if (_timing.TickPeriod <= TimeSpan.Zero)
            return 0f;

        return Math.Clamp((float) (_timing.TickRemainder.TotalSeconds / _timing.TickPeriod.TotalSeconds), 0f, 1f);
    }

    private static string FormatVector(Vector2 vector)
    {
        return $"({vector.X:0.000},{vector.Y:0.000})";
    }

    private readonly record struct ZThrowDebugSample(
        EntityUid Uid,
        ZLevelPhysicsComponent ZPhysics,
        TransformComponent Transform,
        bool HasPhysics,
        BodyStatus BodyStatus,
        Vector2 LinearVelocity,
        float AngularVelocity,
        ZLevelVisualRenderData RenderData,
        bool Thrown,
        Vector2 TransformWorldPosition,
        Vector2 RenderedWorldPosition);

    private readonly record struct ZThrowTrailPoint(Vector2 Position, int RenderZLevel, GameTick Tick);
}
