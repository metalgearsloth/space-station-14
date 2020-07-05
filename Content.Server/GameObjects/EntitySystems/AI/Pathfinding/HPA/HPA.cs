using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Access;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Pathfinders;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Shared.AI;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.HPA
{
    public sealed class HPAAccessible
    {
        public float VisionRadius { get; }
        public ICollection<string> Access { get; }
        public int CollisionMask { get; }

        public HPAAccessible(float visionRadius, ICollection<string> access, int collisionMask)
        {
            VisionRadius = visionRadius;
            Access = access;
            CollisionMask = collisionMask;
        }

        public static HPAAccessible GetArgs(IEntity entity)
        {
            var collisionMask = 0;
            if (entity.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            var access = AccessReader.FindAccessTags(entity);
            var visionRadius = entity.GetComponent<AiControllerComponent>().VisionRadius;
            
            return new HPAAccessible(visionRadius, access, collisionMask);
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
        
        [Dependency] private IMapManager _mapmanager;
        private PathfindingSystem _pathfindingSystem;
        
        private HashSet<PathfindingChunk> _queuedUpdates = new HashSet<PathfindingChunk>();
        
        // Oh god the nesting. Shouldn't need to go beyond this
        private Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>> _regions = 
            new Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>>();
        
        // Cache what regions are accessible from this region. Cached per accessible args
        // so multiple entities in the same region with the same args should all be able to share their accessibility lookup
        // TODO: There's probably a more memory-efficient way to cache this
        private Dictionary<HPARegion, Dictionary<HPAAccessible, HashSet<HPARegion>>> _cachedAccessible = 
            new Dictionary<HPARegion, Dictionary<HPAAccessible, HashSet<HPARegion>>>();

        public override void Initialize()
        {
            _pathfindingSystem = Get<PathfindingSystem>();
            SubscribeLocalEvent<PathfindingChunkUpdateMessage>(RecalculateNodeRegions);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var chunk in _queuedUpdates)
            {
                GenerateRegions(chunk);
            }
            
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

        public bool CanAccess(IEntity entity, IEntity target)
        {
            var targetTile = _mapmanager.GetGrid(target.Transform.GridID).GetTileRef(target.Transform.GridPosition);
            return CanAccess(entity, targetTile);
        }

        public bool CanAccess(IEntity entity, TileRef targetTile)
        {
            if (entity.Transform.GridID != targetTile.GridIndex)
            {
                return false;
            }
            
            var entityTile = _mapmanager.GetGrid(entity.Transform.GridID).GetTileRef(entity.Transform.GridPosition);
            var entityNode = _pathfindingSystem.GetNode(entityTile);
            var targetNode = _pathfindingSystem.GetNode(targetTile);
            var entityRegion = GetRegion(entityNode);
            var targetRegion = GetRegion(targetNode);
            // TODO: Regional pathfind from target to entity
            // Early out
            if (entityRegion == targetRegion)
            {
                return true;
            }

            var accessibleArgs = HPAAccessible.GetArgs(entity);
            
            if (!TryGetCache(accessibleArgs, entityRegion))
            {
                BuildVisionAccessible(accessibleArgs, entityRegion);
            }
            
            return _cachedAccessible[entityRegion][accessibleArgs].Contains(targetRegion);
        }

        private bool TryGetCache(HPAAccessible accessible, HPARegion region)
        {
            if (!_cachedAccessible.TryGetValue(region, out var regionCache))
            {
                return false;
            }

            if (!regionCache.ContainsKey(accessible))
            {
                return false;
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
        private void BuildVisionAccessible(HPAAccessible accessibleArgs, HPARegion entityRegion)
        {
            var openSet = new Queue<HPARegion>();
            openSet.Enqueue(entityRegion);
            var closedSet = new HashSet<HPARegion>();
            var accessible = new HashSet<HPARegion> {entityRegion};

            while (openSet.Count > 0)
            {
                var region = openSet.Dequeue();
                closedSet.Add(region);

                foreach (var (_, neighbor) in region.Neighbors)
                {
                    // Technically it'll also cache outside of visionradius because it's only checking origin but ehh it's fine I think
                    if (!neighbor.RegionTraversable(accessibleArgs) || neighbor.Distance(entityRegion) > accessibleArgs.VisionRadius || closedSet.Contains(neighbor)) continue;
                    openSet.Enqueue(neighbor);
                    accessible.Add(neighbor);
                }
            }

            _cachedAccessible[entityRegion][accessibleArgs] = accessible;
        }
        
        // TODO: Build args for entity here

        /// <summary>
        /// Grab the left and bottom nodes and if they're in different regions then add to our edge and their edge
        /// </summary>
        /// Implicitly they would've already been merged if possible
        /// <param name="region"></param>
        /// <param name="node"></param>
        private void UpdateRegionEdge(HPARegion region, PathfindingNode node)
        {
            // TODO: Look at hashing instead maybe... to try and lower memory usage
            DebugTools.Assert(region.Nodes.Contains(node));
            // TODO: Get the node to the left and check if it's a different region, if so then hash
            var leftNode = node.GetNeighbor(Direction.West);
            if (leftNode != null)
            {
                var leftRegion = GetRegion(leftNode);
                if (leftRegion != null && leftRegion != region)
                {
                    // TODO: Update our left edge
                    region.UpdateNeighbor(Direction.West, leftRegion);
                    leftRegion.UpdateNeighbor(Direction.East, region);
                }
            }

            // TODO: Get the node to the bottom and check if it's a different region, if so then hash
            var bottomNode = node.GetNeighbor(Direction.South);
            if (bottomNode != null)
            {
                var bottomRegion = GetRegion(bottomNode);
                if (bottomRegion != null && bottomRegion != region)
                {
                    // TODO
                    region.UpdateNeighbor(Direction.South, bottomRegion);
                    bottomRegion.UpdateNeighbor(Direction.North, region);
                }
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
                var region = new HPARegion(node.ParentChunk, node, new HashSet<PathfindingNode>(1) {node}, true);
                _cachedAccessible.Add(region, new Dictionary<HPAAccessible, HashSet<HPARegion>>());
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
            var newRegion = new HPARegion(node.ParentChunk, node, new HashSet<PathfindingNode> {node}, node.AccessReaders.Count > 0);
            _regions[parentChunk.GridId][parentChunk].Add(newRegion);
            existingRegions.Add(node, newRegion);
            _cachedAccessible.Add(newRegion, new Dictionary<HPAAccessible, HashSet<HPARegion>>());
            UpdateRegionEdge(newRegion, node);
            
            return newRegion;
        }

        private void MergeInto(HPARegion source, HPARegion target)
        {
            DebugTools.AssertNotNull(source);
            DebugTools.AssertNotNull(target);
            foreach (var node in source.Nodes)
            {
                target.Add(node);
            }
            
            _regions[source.ParentChunk.GridId][source.ParentChunk].Remove(source);
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
            
            if (_regions[chunk.GridId].ContainsKey(chunk))
            {
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

    public class HPARegion
    {
        /// <summary>
        /// Bottom-left node of the region
        /// </summary>
        public PathfindingNode OriginNode { get; }
        public PathfindingChunk ParentChunk { get; }
        public Dictionary<Direction, HPARegion> Neighbors { get; private set; } = new Dictionary<Direction, HPARegion>();
        
        public bool IsDoor { get; }
        public HashSet<PathfindingNode> Nodes => _nodes;
        private HashSet<PathfindingNode> _nodes;

        public HPARegion(PathfindingChunk parentChunk, PathfindingNode originNode, HashSet<PathfindingNode> nodes, bool isDoor = false)
        {
            ParentChunk = parentChunk;
            OriginNode = originNode;
            _nodes = nodes;
            IsDoor = isDoor;
        }

        public void UpdateNeighbor(Direction direction, HPARegion region)
        {
            Neighbors[direction] = region;
        }

        public float Distance(HPARegion otherRegion)
        {
            return PathfindingHelpers.OctileDistance(otherRegion.OriginNode, OriginNode);
        }

        public bool RegionTraversable(HPAAccessible accessibleArgs)
        {
            return false;
        }

        /// <summary>
        /// Only matters for door-regions
        /// </summary>
        /// <returns></returns>
        public bool RegionTraversable(IEntity entity)
        {
            if (_nodes.Count > 1)
            {
                return true;
            }
            
            var node = _nodes.First();

            // Don't need to lookup entity details if it's just a blank tile
            if (node.BlockedCollisionMask == 0x0)
            {
                return true;
            }

            var access = AccessReader.FindAccessTags(entity);
            var collisionMask = 0;
            if (entity.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            return PathfindingHelpers.Traversable(collisionMask, access, node);
        }

        public void Add(PathfindingNode node)
        {
            _nodes.Add(node);
        }
    }
}