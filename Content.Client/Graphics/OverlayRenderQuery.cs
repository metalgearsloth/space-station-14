using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Graphics;

/// <summary>
/// Queries canonical spatial data for every map that can contribute a sample to the current render layer.
/// </summary>
public static class OverlayRenderQuery
{
    /// <summary>
    /// Finds grids whose renderer samples can intersect this overlay pass. Results still need to be filtered with
    /// <see cref="OverlayDrawArgs.TryGetEntityRenderLayer"/> before drawing.
    /// </summary>
    public static void FindRenderGrids(
        this in OverlayDrawArgs args,
        SharedMapSystem maps,
        ref List<Entity<MapGridComponent>> grids,
        float enlargement = 0f,
        bool approx = true,
        bool includeMap = false)
    {
        grids.Clear();
        foreach (var visibleMap in args.VisibleMaps)
        {
            if (!args.TryGetMapRenderBounds(visibleMap, out var mapId, out var bounds))
                continue;

            if (enlargement != 0f)
                bounds = bounds.Enlarged(enlargement);

            maps.FindGridsIntersecting(mapId, bounds, ref grids, approx, includeMap);
        }
    }

    /// <summary>
    /// Finds component-bearing entities whose renderer samples can intersect this overlay pass.
    /// </summary>
    public static void FindRenderEntities<T>(
        this in OverlayDrawArgs args,
        EntityLookupSystem lookup,
        HashSet<Entity<T>> entities,
        float enlargement = 0f,
        LookupFlags flags = EntityLookupSystem.DefaultFlags)
        where T : IComponent
    {
        entities.Clear();
        foreach (var visibleMap in args.VisibleMaps)
        {
            if (!args.TryGetMapRenderBounds(visibleMap, out var mapId, out var bounds))
                continue;

            if (enlargement != 0f)
                bounds = bounds.Enlarged(enlargement);

            lookup.GetEntitiesIntersecting(mapId, bounds, entities, flags);
        }
    }
}
