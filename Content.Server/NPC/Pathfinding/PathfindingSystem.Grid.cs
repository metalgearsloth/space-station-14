using System.Numerics;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    // What relevant collision groups we track for pathfinding.
    // Stuff like chairs have collision but aren't relevant for mobs.
    public const int PathfindingCollisionMask = (int) CollisionGroup.MobMask;
    public const int PathfindingCollisionLayer = (int) CollisionGroup.MobLayer;

    private readonly Stopwatch _stopwatch = new();

    // Probably can't pool polys as there might be old pathfinding refs to them.

    private void InitializeGrid()
    {
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
    }

    private bool IsBodyRelevant(FixturesComponent fixtures)
    {
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            if ((fixture.CollisionMask & PathfindingCollisionLayer) != 0x0 ||
                (fixture.CollisionLayer & PathfindingCollisionMask) != 0x0)
            {
                return true;
            }
        }

        return false;
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        EnsureComp<GridPathfindingComponent>(ev.EntityUid);
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        RemComp<GridPathfindingComponent>(ev.EntityUid);
    }

    private byte GetIndex(int x, int y)
    {
        return (byte) (x * ChunkSize + y);
    }

    private Vector2i GetOrigin(Vector2 localPos)
    {
        return new Vector2i((int) Math.Floor(localPos.X / ChunkSize), (int) Math.Floor(localPos.Y / ChunkSize));
    }

    private Vector2i GetOrigin(EntityCoordinates coordinates, EntityUid gridUid)
    {
        var gridXform = Transform(gridUid);
        var localPos = _xformSystem.GetInvWorldMatrix(gridXform).Transform(coordinates.ToMapPos(EntityManager, _xformSystem));
        return new Vector2i((int) Math.Floor(localPos.X / ChunkSize), (int) Math.Floor(localPos.Y / ChunkSize));
    }

    /// <summary>
    /// Adds the neighbors for the poly to the neighbors list.
    /// </summary>
    public void GetNeighbors(PathPoly poly, List<PathPoly> neighbors)
    {
        // Okay so:
        // Pathfinder calls GetPoly, we gucci
        // From here we get neighbors by calling this, PathPoly never directly references its neighbors.


        // TODO: Pass in graph


        // TODO: Need GetPoly and shit to work
        // From there the graph iteration calls this which returns structs of neighbors.
    }

    /// <summary>
    /// Gets the polys for the specified tile.
    /// </summary>
    public List<PathPoly> GetPolys(EntityUid gridUid, GridPathfindingComponent pathfinding, MapGridComponent grid, Vector2i tilePos)
    {
        // Tile
        var tileEntities = new ValueList<EntityUid>();
        var tilePolys = new List<Box2i>();

        var tile = _maps.GetTileRef(gridUid, grid, tilePos);
        var flags = tile.Tile.IsEmpty ? PathfindingBreadcrumbFlag.Space : PathfindingBreadcrumbFlag.None;
        // var isBorder = x < 0 || y < 0 || x == ChunkSize - 1 || y == ChunkSize - 1;

        tileEntities.Clear();
        var available = _lookup.GetEntitiesIntersecting(tile, flags: LookupFlags.Dynamic | LookupFlags.Static);

        foreach (var ent in available)
        {
            // Irrelevant for pathfinding
            if (!_fixturesQuery.TryGetComponent(ent, out var fixtures) ||
                !IsBodyRelevant(fixtures))
            {
                continue;
            }

            var xform = _xformQuery.GetComponent(ent);

            if (xform.ParentUid != gridUid ||
                _maps.LocalToTile(gridUid, grid, xform.Coordinates) != tilePos)
            {
                continue;
            }

            tileEntities.Add(ent);
        }

        var (x, y) = tilePos;
        var points = new PathfindingBreadcrumb[SubStep,SubStep];

        for (var subX = 0; subX < SubStep; subX++)
        {
            for (var subY = 0; subY < SubStep; subY++)
            {
                var xOffset = x * SubStep + subX;
                var yOffset = y * SubStep + subY;

                // Subtile
                var localPos = new Vector2(StepOffset + tilePos.X + (float) subX / SubStep, StepOffset + tilePos.Y + (float) subY / SubStep);
                var collisionMask = 0x0;
                var collisionLayer = 0x0;
                var damage = 0f;

                foreach (var ent in tileEntities)
                {
                    if (!_fixturesQuery.TryGetComponent(ent, out var fixtures))
                        continue;

                    var colliding = false;

                    foreach (var fixture in fixtures.Fixtures.Values)
                    {
                        // Don't need to re-do it.
                        if (!fixture.Hard ||
                            (collisionMask & fixture.CollisionMask) == fixture.CollisionMask &&
                            (collisionLayer & fixture.CollisionLayer) == fixture.CollisionLayer)
                        {
                            continue;
                        }

                        // Do an AABB check first as it's probably faster, then do an actual point check.
                        var intersects = false;

                        foreach (var proxy in fixture.Proxies)
                        {
                            if (!proxy.AABB.Contains(localPos))
                                continue;

                            intersects = true;
                        }

                        if (!intersects ||
                            !_xformQuery.TryGetComponent(ent, out var xform))
                        {
                            continue;
                        }

                        if (!_fixtures.TestPoint(fixture.Shape, new Transform(xform.LocalPosition, xform.LocalRotation), localPos))
                        {
                            continue;
                        }

                        collisionLayer |= fixture.CollisionLayer;
                        collisionMask |= fixture.CollisionMask;
                        colliding = true;
                    }

                    // If entity doesn't intersect this node (e.g. thindows) then ignore it.
                    if (!colliding)
                        continue;

                    if (_accessQuery.HasComponent(ent))
                    {
                        flags |= PathfindingBreadcrumbFlag.Access;
                    }

                    if (_doorQuery.HasComponent(ent))
                    {
                        flags |= PathfindingBreadcrumbFlag.Door;
                    }

                    if (_climbableQuery.HasComponent(ent))
                    {
                        flags |= PathfindingBreadcrumbFlag.Climb;
                    }

                    if (_destructibleQuery.TryGetComponent(ent, out var damageable))
                    {
                        damage += _destructible.DestroyedAt(ent, damageable).Float();
                    }
                }

                var crumb = new PathfindingBreadcrumb()
                {
                    Coordinates = new Vector2i(xOffset, yOffset),
                    Data = new PathfindingData(flags, collisionLayer, collisionMask, damage),
                };

                points[xOffset, yOffset] = crumb;
            }
        }

        // Now we got tile data and we can get the polys
        var data = points[x * SubStep, y * SubStep].Data;
        var start = Vector2i.Zero;

        for (var i = 0; i < SubStep * SubStep; i++)
        {
            var ix = i / SubStep;
            var iy = i % SubStep;

            var nextX = (i + 1) / SubStep;
            var nextY = (i + 1) % SubStep;

            // End point
            if (iy == SubStep - 1 ||
                !points[x * SubStep + nextX, y * SubStep + nextY].Data.Equals(data))
            {
                tilePolys.Add(new Box2i(start, new Vector2i(ix, iy)));

                if (i < (SubStep * SubStep) - 1)
                {
                    start = new Vector2i(nextX, nextY);
                    data = points[x * SubStep + nextX, y * SubStep + nextY].Data;
                }
            }
        }

        // Now combine the lines
        var anyCombined = true;

        while (anyCombined)
        {
            anyCombined = false;

            for (var i = 0; i < tilePolys.Count; i++)
            {
                var poly = tilePolys[i];
                data = points[x * SubStep + poly.Left, y * SubStep + poly.Bottom].Data;

                for (var j = i + 1; j < tilePolys.Count; j++)
                {
                    var nextPoly = tilePolys[j];
                    var nextData = points[x * SubStep + nextPoly.Left, y * SubStep + nextPoly.Bottom].Data;

                    // Oh no, Combine
                    if (poly.Bottom == nextPoly.Bottom &&
                        poly.Top == nextPoly.Top &&
                        poly.Right + 1 == nextPoly.Left &&
                        data.Equals(nextData))
                    {
                        tilePolys.RemoveAt(j);
                        j--;
                        poly = new Box2i(poly.Left, poly.Bottom, poly.Right + 1, poly.Top);
                        anyCombined = true;
                    }
                }

                tilePolys[i] = poly;
            }
        }

        // TODO: Can store a hash for each tile and check if the breadcrumbs match and avoid allocating these at all.
        var

        var tilePoly = chunkPolys[x * ChunkSize + y];
        var polyOffset = gridOrigin + new Vector2(x, y);

        foreach (var poly in tilePolys)
        {
            var box = new Box2((Vector2) poly.BottomLeft / SubStep + polyOffset,
                (Vector2) (poly.TopRight + Vector2i.One) / SubStep + polyOffset);
            var polyData = points[x * SubStep + poly.Left, y * SubStep + poly.Bottom].Data;

            var neighbors = new HashSet<PathPoly>();
            tilePoly.Add(new PathPoly(gridUid, chunk.Origin, GetIndex(x, y), box, polyData, neighbors));
        }
    }

    private void BuildNavmesh(GridPathfindingChunk chunk, Entity<GridPathfindingComponent> pathfinding)
    {
        var sw = new Stopwatch();
        sw.Start();

        var chunkPolys = chunk.Polygons;
        var component = pathfinding.Comp;
        component.Chunks.TryGetValue(chunk.Origin + new Vector2i(-1, 0), out var leftChunk);
        component.Chunks.TryGetValue(chunk.Origin + new Vector2i(0, -1), out var bottomChunk);
        component.Chunks.TryGetValue(chunk.Origin + new Vector2i(1, 0), out var rightChunk);
        component.Chunks.TryGetValue(chunk.Origin + new Vector2i(0, 1), out var topChunk);

        // Now we can get the neighbors for our tile polys
        for (var x = 0; x < ChunkSize; x++)
        {
            for (var y = 0; y < ChunkSize; y++)
            {
                var index = GetIndex(x, y);
                var tile = chunkPolys[index];

                for (byte i = 0; i < tile.Count; i++)
                {
                    var poly = tile[i];
                    var enlarged = poly.Box.Enlarged(StepOffset);

                    // Shouldn't need to wraparound as previous neighbors would've handled us.
                    for (var j = (byte) (i + 1); j < tile.Count; j++)
                    {
                        var neighbor = tile[j];
                        var enlargedNeighbor = neighbor.Box.Enlarged(StepOffset);
                        var overlap = Box2.Area(enlarged.Intersect(enlargedNeighbor));

                        // Need to ensure they intersect by at least 2 tiles.
                        if (overlap <= 0.5f / SubStep)
                            continue;

                        AddNeighbors(poly, neighbor);
                    }

                    // TODO: Get neighbor tile polys
                    for (var ix = -1; ix <= 1; ix++)
                    {
                        for (var iy = -1; iy <= 1; iy++)
                        {
                            if (ix != 0 && iy != 0)
                                continue;

                            var neighborX = x + ix;
                            var neighborY = y + iy;
                            var neighborIndex = GetIndex(neighborX, neighborY);
                            List<PathPoly> neighborTile;

                            if (neighborX < 0)
                            {
                                if (leftChunk == null)
                                    continue;

                                neighborX = ChunkSize - 1;
                                neighborIndex = GetIndex(neighborX, neighborY);
                                neighborTile = leftChunk.Polygons[neighborIndex];
                            }
                            else if (neighborY < 0)
                            {
                                if (bottomChunk == null)
                                    continue;

                                neighborY = ChunkSize - 1;
                                neighborIndex = GetIndex(neighborX, neighborY);
                                neighborTile = bottomChunk.Polygons[neighborIndex];
                            }
                            else if (neighborX >= ChunkSize)
                            {
                                if (rightChunk == null)
                                    continue;

                                neighborX = 0;
                                neighborIndex = GetIndex(neighborX, neighborY);
                                neighborTile = rightChunk.Polygons[neighborIndex];
                            }
                            else if (neighborY >= ChunkSize)
                            {
                                if (topChunk == null)
                                    continue;

                                neighborY = 0;
                                neighborIndex = GetIndex(neighborX, neighborY);
                                neighborTile = topChunk.Polygons[neighborIndex];
                            }
                            else
                            {
                                neighborTile = chunkPolys[neighborIndex];
                            }

                            for (byte j = 0; j < neighborTile.Count; j++)
                            {
                                var neighbor = neighborTile[j];
                                var enlargedNeighbor = neighbor.Box.Enlarged(StepOffset);
                                var overlap = Box2.Area(enlarged.Intersect(enlargedNeighbor));

                                // Need to ensure they intersect by at least 2 tiles.
                                if (overlap <= 0.5f / SubStep)
                                    continue;

                                AddNeighbors(poly, neighbor);
                            }
                        }
                    }
                }
            }
        }

        // Log.Debug($"Built navmesh in {sw.Elapsed.TotalMilliseconds}ms");
        SendPolys(chunk, pathfinding, chunkPolys);
    }
}
