using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Access;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Accessible;
using Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Pathfinders;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Shared.AI;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.HPA
{
    public sealed class HPAAccessibleArgs
    {
        public float VisionRadius { get; }
        public ICollection<string> Access { get; }
        public int CollisionMask { get; }

        public HPAAccessibleArgs(float visionRadius, ICollection<string> access, int collisionMask)
        {
            VisionRadius = visionRadius;
            Access = access;
            CollisionMask = collisionMask;
        }

        public static HPAAccessibleArgs GetArgs(IEntity entity)
        {
            var collisionMask = 0;
            if (entity.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            var access = AccessReader.FindAccessTags(entity);
            var visionRadius = entity.GetComponent<AiControllerComponent>().VisionRadius;
            
            return new HPAAccessibleArgs(visionRadius, access, collisionMask);
        }
    }
    
    [UsedImplicitly]
    public sealed class HPAPathfindingSystem : EntitySystem
    {
        /*
         * The purpose of this is to provide a higher-level / hierarchical abstraction of the actual pathfinding graph
         * The goal is so that we can more quickly discern if a specific node is reachable or not rather than
         * Pathfinding the entire bloody graph.
         *
         * There's a lot of different implementations of hierarchical or some variation of it: HPA*, PRA, HAA*, etc.
         * (HPA* technically caches the edge nodes of each chunk), e.g. Rimworld, Factorio, etc.
         * so we'll just write one with SS14's requirements in mind.
         */

        // TODO: Lots of optimisation work. Like a lot.
        // Need to optimise the neighbors for regions more
#pragma warning disable 649
        [Dependency] private IMapManager _mapmanager;
#pragma warning restore 649
        private PathfindingSystem _pathfindingSystem;
        
        private HashSet<PathfindingChunk> _queuedUpdates = new HashSet<PathfindingChunk>();
        
        // Oh god the nesting. Shouldn't need to go beyond this
        private Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>> _regions = 
            new Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>>();
        
        /// <summary>
        /// Minimum time for the cache to be stored
        /// </summary>
        private const float MinCacheTime = 1.0f;
        
        // Cache what regions are accessible from this region. Cached per accessible args
        // so multiple entities in the same region with the same args should all be able to share their accessibility lookup
        // Also need to store when we cached it to know if it's stale
        // TODO: There's probably a more memory-efficient way to cache this
        // Also, didn't use a dictionary because there didn't seem to be a clean way to do the lookup
        // Plus this way we can check if everything is equal except for vision so an entity with a lower vision radius can use an entity with a higher vision radius' cached result
        private Dictionary<HPAAccessibleArgs, Dictionary<HPARegion, (TimeSpan, HashSet<HPARegion>)>> _cachedAccessible = 
            new Dictionary<HPAAccessibleArgs, Dictionary<HPARegion, (TimeSpan, HashSet<HPARegion>)>>();

        public override void Initialize()
        {
            _pathfindingSystem = Get<PathfindingSystem>();
            SubscribeLocalEvent<PathfindingChunkUpdateMessage>(RecalculateNodeRegions);
#if DEBUG
            SubscribeLocalEvent<PlayerAttachSystemMessage>(SendDebugMessage);
#endif
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var chunk in _queuedUpdates)
            {
                GenerateRegions(chunk);
            }

#if DEBUG
            if (_queuedUpdates.Count > 0)
            {
                foreach (var (gridId, _) in _regions)
                {
                    SendRegionsDebugMessage(gridId);
                }
            }
#endif
            _queuedUpdates.Clear();
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _queuedUpdates.Clear();
            _regions.Clear();
            _cachedAccessible.Clear();
        }

        private void RecalculateNodeRegions(PathfindingChunkUpdateMessage message)
        {
            // TODO: Only need to do changed nodes ideally
            _queuedUpdates.Add(message.Chunk);
        }

        /// <summary>
        /// Can the entity reach the target?
        /// </summary>
        /// First it does a quick check to see if there are any traversable nodes in range.
        /// Then it will go through the regions to try and see if there's a region connection between the target and itself
        /// Will used a cached region if available
        /// <param name="entity"></param>
        /// <param name="target"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public bool CanAccess(IEntity entity, IEntity target, float range = 0.0f)
        {
            var targetTile = _mapmanager.GetGrid(target.Transform.GridID).GetTileRef(target.Transform.GridPosition);
            var targetNode = _pathfindingSystem.GetNode(targetTile);

            var collisionMask = 0;
            if (entity.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            var access = AccessReader.FindAccessTags(entity);

            // We'll do a quick traversable check before going through regions
            // If we can't access it we'll try to get a valid node in range (this is essentially an early-out)
            if (!PathfindingHelpers.Traversable(collisionMask, access, targetNode))
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (range == 0.0f)
                {
                    return false;
                }

                var pathfindingArgs = new PathfindingArgs(entity.Uid, access, collisionMask, default, targetTile, range);
                foreach (var node in BFSPathfinder.GetNodesInRange(pathfindingArgs, false))
                {
                    targetNode = node;
                }
            }
            
            return CanAccess(entity, targetNode);
        }

        public bool CanAccess(IEntity entity, PathfindingNode targetNode)
        {
            if (entity.Transform.GridID != targetNode.TileRef.GridIndex)
            {
                return false;
            }
            
            var entityTile = _mapmanager.GetGrid(entity.Transform.GridID).GetTileRef(entity.Transform.GridPosition);
            var entityNode = _pathfindingSystem.GetNode(entityTile);
            var entityRegion = GetRegion(entityNode);
            var targetRegion = GetRegion(targetNode);
            // TODO: Regional pathfind from target to entity
            // Early out
            if (entityRegion == targetRegion)
            {
                return true;
            }

            // We'll go from target's position to us because most of the time it's probably in a locked room rather than vice versa
            var accessibleArgs = HPAAccessibleArgs.GetArgs(entity);
            var cachedArgs = GetCachedArgs(accessibleArgs);
            (TimeSpan, HashSet<HPARegion>)? cached;

            if (!IsCacheValid(cachedArgs, targetRegion))
            {
                cached = GetVisionAccessible(cachedArgs, targetRegion);
                _cachedAccessible[cachedArgs][targetRegion] = cached.Value;
            }
            else
            {
                cached = _cachedAccessible[cachedArgs][targetRegion];
            }

            return cached.Value.Item2.Contains(entityRegion);
        }

        /// <summary>
        /// Get any adequate cached args if possible, otherwise just use ours
        /// </summary>
        /// <param name="accessibleArgs"></param>
        /// <returns></returns>
        private HPAAccessibleArgs GetCachedArgs(HPAAccessibleArgs accessibleArgs)
        {
            HPAAccessibleArgs foundArgs = null;

            // Get a "good enough" cache for our args (e.g. if their vision is higher but all the rest is the same it's fine)
            foreach (var (cachedAccessible, _) in _cachedAccessible)
            {
                if (Equals(cachedAccessible.Access, accessibleArgs.Access) &&
                    cachedAccessible.CollisionMask == accessibleArgs.CollisionMask &&
                    cachedAccessible.VisionRadius <= accessibleArgs.VisionRadius)
                {
                    foundArgs = cachedAccessible;
                    break;
                }
            }

            return foundArgs ?? accessibleArgs;
        }

        /// <summary>
        /// Checks whether there's a valid cache for our accessibility args.
        /// Most regular mobs can share their cached accessibility with each other
        /// </summary>
        /// Will also remove it from the cache if it is invalid
        /// <param name="accessibleArgs"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        private bool IsCacheValid(HPAAccessibleArgs accessibleArgs, HPARegion region)
        {
            if (!_cachedAccessible.TryGetValue(accessibleArgs, out var cachedArgs))
            {
                _cachedAccessible.Add(accessibleArgs, new Dictionary<HPARegion, (TimeSpan, HashSet<HPARegion>)>());
                return false;
            }
            
            if (!cachedArgs.TryGetValue(region, out var regionCache))
            {
                return false;
            }

            // Just so we don't invalidate the cache every tick we'll store it for a minimum amount of time
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            if ((currentTime - regionCache.Item1).TotalSeconds < MinCacheTime)
            {
                return true;
            }

            var checkedAccess = new HashSet<HPARegion>();
            
            // Check if cache is stale
            foreach (var accessibleRegion in regionCache.Item2)
            {
                if (checkedAccess.Contains(accessibleRegion)) continue;
                
                // Any applicable chunk has been invalidated OR one of our neighbors has been invalidated (i.e. new connections)
                if (accessibleRegion.ParentChunk.LastUpdate > regionCache.Item1)
                {
                    // Remove the stale cache to be updated later
                    _cachedAccessible[accessibleArgs].Remove(region);
                    return false;
                }

                foreach (var neighbor in accessibleRegion.Neighbors)
                {
                    if (checkedAccess.Contains(neighbor)) continue;
                    if (neighbor.ParentChunk.LastUpdate > regionCache.Item1)
                    {
                        _cachedAccessible[accessibleArgs].Remove(region);
                        return false;
                    }
                    checkedAccess.Add(neighbor);
                }
                checkedAccess.Add(accessibleRegion);
            }
            return true;
        }

        /// <summary>
        /// Caches the entity's nearby accessible regions in vision radius
        /// </summary>
        /// Longer-term TODO: Hierarchical pathfinding in which case this function would probably get bulldozed
        /// Also TODO: Can share cache with other entities as well if similar to us. Can also cache indefinitely until pathfinding graph changes
        /// <param name="accessibleArgs"></param>
        /// <param name="entityRegion"></param>
        private (TimeSpan, HashSet<HPARegion>) GetVisionAccessible(HPAAccessibleArgs accessibleArgs, HPARegion entityRegion)
        {
            // TODO: Still not working
            var openSet = new Queue<HPARegion>();
            openSet.Enqueue(entityRegion);
            var closedSet = new HashSet<HPARegion> {entityRegion};
            var accessible = new HashSet<HPARegion> {entityRegion};

            while (openSet.Count > 0)
            {
                var region = openSet.Dequeue();
                closedSet.Add(region);

                foreach (var neighbor in region.Neighbors)
                {
                    if (closedSet.Contains(neighbor))
                    {
                        continue;
                    }
                    // TODO: The HashSet was allowing duplicate HPARegion which it probably shouldn't be allowing
                    // (under the old implementation)
                    // Technically it'll also cache outside of visionradius because it's only checking origin
                    // Not sure how to make it less jank and more optimised
                    // TODO: The distance check is jank a.f.
                    if (!neighbor.RegionTraversable(accessibleArgs) ||
                        neighbor.Distance(entityRegion) > accessibleArgs.VisionRadius * 2)
                    {
                        closedSet.Add(neighbor);
                        continue;
                    }
                    
                    openSet.Enqueue(neighbor);
                    accessible.Add(neighbor);
                }
            }

            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            return (currentTime, accessible);
        }

        /// <summary>
        /// Grab the related cardinal nodes and if they're in different regions then add to our edge and their edge
        /// </summary>
        /// Implicitly they would've already been merged if possible
        /// <param name="region"></param>
        /// <param name="node"></param>
        private void UpdateRegionEdge(HPARegion region, PathfindingNode node)
        {
            // TODO: Moar work to fix
            DebugTools.Assert(region.Nodes.Contains(node));
            // Originally I tried just doing bottom and left but that doesn't work as the chunk update order is not guaranteed

            var checkDirections = new[] {Direction.East, Direction.South, Direction.West, Direction.North};
            foreach (var direction in checkDirections)
            {
                var directionNode = node.GetNeighbor(direction);
                if (directionNode == null) continue;
                
                var directionRegion = GetRegion(directionNode);
                if (directionRegion == null || directionRegion == region) continue;

                region.Neighbors.Add(directionRegion);
                directionRegion.Neighbors.Add(region);
            }
        }

        private HPARegion GetRegion(PathfindingNode node)
        {
            // Not sure on the best way to optimise this
            // On the one hand, just storing each node's region is faster buuutttt muh memory
            // On the other hand, you might need O(n) lookups on regions for each chunk, though it's probably not too bad with smaller chunk sizes?

            var parentChunk = node.ParentChunk;
            
            // No guarantee the node even has a region yet (if we're doing neighbor lookups)
            if (!_regions[parentChunk.GridId].TryGetValue(parentChunk, out var regions))
            {
                return null;
            }

            foreach (var region in regions)
            {
                if (region.Nodes.Contains(node))
                {
                    return region;
                }
            }

            // Longer term this will probably be guaranteed a region but for now space etc. are no region
            return null;
        }

        /// <summary>
        /// Add this node to the relevant region.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="existingRegions"></param>
        /// <returns></returns>
        private HPARegion CalculateNode(PathfindingNode node, Dictionary<PathfindingNode, HPARegion> existingRegions)
        {
            if (node.BlockedCollisionMask != 0x0 || node.TileRef.Tile.IsEmpty)
            {
                return null;
            }

            // TODO: Need to check left and bottom nodes to see if they're in a different region (that we can't merge with)
            // If that's the case then set this node as an edge for us (joining an existing edge if possible) and add it as an edge for them
            
            var parentChunk = node.ParentChunk;
            // Doors will be their own separate region
            // We won't store them in existingRegions so they don't show up
            if (node.AccessReaders.Count > 0)
            {
                var region = new HPARegion(node, new HashSet<PathfindingNode>(1) {node}, true);
                _regions[parentChunk.GridId][parentChunk].Add(region);
                UpdateRegionEdge(region, node);
                return region;
            }

            // Relative x and y of the chunk
            // If one of our bottom / left neighbors are in a region try to join them
            // Otherwise, make our own region.
            var x = node.TileRef.X - parentChunk.Indices.X;
            var y = node.TileRef.Y - parentChunk.Indices.Y;
            var leftNeighbor = x > 0 ? parentChunk.Nodes[x - 1, y] : null;
            var bottomNeighbor = y > 0 ? parentChunk.Nodes[x, y - 1] : null;
            HPARegion leftRegion;
            HPARegion bottomRegion;

            // We'll check if our left or down neighbors are already in a region and join them, unless we're a door
            if (node.AccessReaders.Count == 0)
            {
                if (x > 0 && leftNeighbor != null)
                {
                    if (existingRegions.TryGetValue(leftNeighbor, out leftRegion) && !leftRegion.IsDoor)
                    {
                        // We'll try and connect the left node's region to the bottom region if they're separate
                        if (bottomNeighbor != null && existingRegions.TryGetValue(bottomNeighbor, out bottomRegion) &&
                            !bottomRegion.IsDoor)
                        {
                            bottomRegion.Add(node);
                            existingRegions.Add(node, bottomRegion);
                            MergeInto(leftRegion, bottomRegion);
                            return bottomRegion;
                        }

                        leftRegion.Add(node);
                        existingRegions.Add(node, leftRegion);
                        return leftRegion;
                    }
                }

                if (y > 0 && bottomNeighbor != null)
                {
                    if (existingRegions.TryGetValue(bottomNeighbor, out bottomRegion) && !bottomRegion.IsDoor)
                    {
                        bottomRegion.Add(node);
                        existingRegions.Add(node, bottomRegion);
                        return bottomRegion;
                    }
                }
            }

            // If we can't join an existing region then we'll make our own
            var newRegion = new HPARegion(node, new HashSet<PathfindingNode> {node}, node.AccessReaders.Count > 0);
            _regions[parentChunk.GridId][parentChunk].Add(newRegion);
            existingRegions.Add(node, newRegion);
            UpdateRegionEdge(newRegion, node);
            
            return newRegion;
        }

        /// <summary>
        /// Combines the two regions into one bigger region
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        private void MergeInto(HPARegion source, HPARegion target)
        {
            DebugTools.AssertNotNull(source);
            DebugTools.AssertNotNull(target);
            foreach (var node in source.Nodes)
            {
                target.Add(node);
            }

            source.Shutdown();
            _regions[source.ParentChunk.GridId][source.ParentChunk].Remove(source);

            foreach (var node in target.Nodes)
            {
                UpdateRegionEdge(target, node);
            }
        }
        
        /// <summary>
        /// Generate all of the regions within a chunk
        /// These can't across over into another chunk and doors are their own region
        /// </summary>
        /// <param name="chunk"></param>
        private void GenerateRegions(PathfindingChunk chunk)
        {
            if (!_regions.ContainsKey(chunk.GridId))
            {
                _regions.Add(chunk.GridId, new Dictionary<PathfindingChunk, HashSet<HPARegion>>());
            }

            if (_regions[chunk.GridId].TryGetValue(chunk, out var regions))
            {
                foreach (var region in regions)
                {
                    region.Shutdown();
                }
                _regions[chunk.GridId].Remove(chunk);
            }

            // Temporarily store the corresponding for each node
            var nodeRegions = new Dictionary<PathfindingNode, HPARegion>();
            var chunkRegions = new HashSet<HPARegion>();
            _regions[chunk.GridId].Add(chunk, chunkRegions);
            
            for (var y = 0; y < PathfindingChunk.ChunkSize; y++)
            {
                for (var x = 0; x < PathfindingChunk.ChunkSize; x++)
                {
                    var node = chunk.Nodes[x, y];
                    var region = CalculateNode(node, nodeRegions);
                    // Currently we won't store a separate region for each mask / space / whatever because muh effort
                    // Long-term you'll want to account for it probably
                    if (region == null)
                    {
                        continue;
                    }
                    
                    chunkRegions.Add(region);
                }
            }
#if DEBUG
            SendRegionsDebugMessage(chunk.GridId);
#endif
        }

#if DEBUG
        private void SendDebugMessage(PlayerAttachSystemMessage message)
        {
            foreach (var (grid, _) in _regions)
            {
                SendRegionsDebugMessage(grid);
            }
        }
        
        /// <summary>
        /// Holy fuckballs this is hammering the connection, make better
        /// </summary>
        /// <param name="gridId"></param>
        private void SendRegionsDebugMessage(GridId gridId)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(gridId);
            // Chunk / Regions / Nodes
            var debugResult = new Dictionary<int, Dictionary<int, List<Vector2>>>();
            var chunkIdx = 0;
            var regionIdx = 0;
            
            foreach (var (_, regions) in _regions[gridId])
            {
                var debugRegions = new Dictionary<int, List<Vector2>>();
                debugResult.Add(chunkIdx, debugRegions);

                foreach (var region in regions)
                {
                    var debugRegionNodes = new List<Vector2>(region.Nodes.Count);
                    debugResult[chunkIdx].Add(regionIdx, debugRegionNodes);

                    foreach (var node in region.Nodes)
                    {
                        var nodeVector = grid.GridTileToLocal(node.TileRef.GridIndices).ToMapPos(mapManager);
                        debugRegionNodes.Add(nodeVector);
                    }

                    regionIdx++;
                }

                chunkIdx++;
            }
            RaiseNetworkEvent(new SharedAiDebug.HpaChunkRegionsDebugMessage(gridId, debugResult));
        }
#endif

        private void GenerateRooms()
        {
            /* TODO: Go through each chunk's regions on the borders
             * foreach relevant neighboring chunk check if we can
             * 
             */
        }
    }

    public class HPARegion : IEquatable<HPARegion>
    {
        /// <summary>
        /// Bottom-left node of the region
        /// </summary>
        public PathfindingNode OriginNode { get; }

        public PathfindingChunk ParentChunk => OriginNode.ParentChunk;
        public HashSet<HPARegion> Neighbors { get; } = new HashSet<HPARegion>();

        public bool IsDoor { get; }
        public HashSet<PathfindingNode> Nodes => _nodes;
        private HashSet<PathfindingNode> _nodes;

        public HPARegion(PathfindingNode originNode, HashSet<PathfindingNode> nodes, bool isDoor = false)
        {
            OriginNode = originNode;
            _nodes = nodes;
            IsDoor = isDoor;
        }

        public void Shutdown()
        {
            var neighbors = new List<HPARegion>(Neighbors);
            
            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                neighbor.Neighbors.Remove(this);
            }
        }

        public float Distance(HPARegion otherRegion)
        {
            return PathfindingHelpers.OctileDistance(otherRegion.OriginNode, OriginNode);
        }

        /// <summary>
        /// Can the given args can traverse this region?
        /// </summary>
        /// <param name="accessibleArgs"></param>
        /// <returns></returns>
        public bool RegionTraversable(HPAAccessibleArgs accessibleArgs)
        {
            // The assumption is that all nodes in a region have the same pathfinding traits
            // As such we can just use the origin node for checking.
            return PathfindingHelpers.Traversable(accessibleArgs.CollisionMask, accessibleArgs.Access,
                OriginNode);
        }

        public void Add(PathfindingNode node)
        {
            _nodes.Add(node);
        }

        public bool Equals(HPARegion other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            return GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return OriginNode.GetHashCode();
        }
    }
}