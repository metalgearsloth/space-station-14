using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Access;
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
        
        private PathfindingSystem _pathfindingSystem;
        // Oh god the nesting. Shouldn't need to go beyond this
        private Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>> _regions = 
            new Dictionary<GridId, Dictionary<PathfindingChunk, HashSet<HPARegion>>>();

        public override void Initialize()
        {
            _pathfindingSystem = Get<PathfindingSystem>();
            SubscribeLocalEvent<PathfindingChunkUpdate>(RecalculateNodeRegions);
        }

        private void RecalculateNodeRegions(PathfindingChunkUpdate message)
        {
            GenerateRegions(message.Chunk);
        }

        /// <summary>
        /// Add this node to the relevant region
        /// </summary>
        /// <param name="node"></param>
        /// <param name="existingRegions"></param>
        /// <returns></returns>
        private HPARegion CalculateNode(PathfindingNode node, Dictionary<PathfindingNode, HPARegion> existingRegions)
        {
            if (node.BlockedCollisionMask != 0x0)
            {
                return null;
            }

            var parentChunk = node.ParentChunk;
            // Doors will be their own separate region
            // We won't store them in existingRegions so they don't show up
            if (node.AccessReaders.Count > 0)
            {
                var region = new HPARegion(node.ParentChunk, new HashSet<PathfindingNode>(1) {node}, true);
                _regions[parentChunk.GridId][parentChunk].Add(region);
                // TODO: Need to hash left / bottom side / top side for edges
                // Like rimjob
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
            var newRegion = new HPARegion(node.ParentChunk, new HashSet<PathfindingNode> {node}, node.AccessReaders.Count > 0);
            _regions[parentChunk.GridId][parentChunk].Add(newRegion);
            existingRegions.Add(node, newRegion);

            if (leftNeighbor != null && existingRegions.TryGetValue(leftNeighbor, out leftRegion) && leftRegion.IsDoor)
            {
                newRegion.Neighbors.Add(leftRegion);
                leftRegion.Neighbors.Add(newRegion);
            }

            if (bottomNeighbor != null && existingRegions.TryGetValue(bottomNeighbor, out bottomRegion) && bottomRegion.IsDoor)
            {
                newRegion.Neighbors.Add(bottomRegion);
                bottomRegion.Neighbors.Add(newRegion);
            }
            
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
                    // Currently we won't store a separate region for each mask because muh effort
                    // Long-term you'll want to account for it probably
                    if (region == null)
                    {
                        continue;
                    }
                    chunkRegions.Add(region);
                }
            }

            foreach (var region in chunkRegions)
            {
                RefreshRegionEdges(region);
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
            
            foreach (var (chunk, regions) in _regions[gridId])
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

        /// <summary>
        /// Hash our edges so we can lookup our neighbors more easily
        /// </summary>
        private void RefreshRegionEdges(HPARegion region)
        {
            // TODO: Region should cache edge nodes as it's doing it...
        }

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
        public PathfindingChunk ParentChunk { get; }
        public List<HPARegion> Neighbors { get; } = new List<HPARegion>();
        
        public bool IsDoor { get; }
        public HashSet<PathfindingNode> Nodes => _nodes;
        private HashSet<PathfindingNode> _nodes;

        public HPARegion(PathfindingChunk parentChunk, HashSet<PathfindingNode> nodes, bool isDoor = false)
        {
            ParentChunk = parentChunk;
            _nodes = nodes;
            IsDoor = isDoor;
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