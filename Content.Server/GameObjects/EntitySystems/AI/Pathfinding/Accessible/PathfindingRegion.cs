using System;
using System.Collections.Generic;
using Content.Server.GameObjects.EntitySystems.Pathfinding;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Accessible
{
    /// <summary>
    /// A group of homogenous PathfindingNodes inside a single chunk
    /// </summary>
    /// Makes the graph smaller and quicker to traverse
    public class PathfindingRegion : IEquatable<PathfindingRegion>
    {
        /// <summary>
        /// Bottom-left reference node of the region
        /// </summary>
        public PathfindingNode OriginNode { get; }

        public PathfindingChunk ParentChunk => OriginNode.ParentChunk;
        public HashSet<PathfindingRegion> Neighbors { get; } = new HashSet<PathfindingRegion>();

        public bool IsDoor { get; }
        public HashSet<PathfindingNode> Nodes => _nodes;
        private HashSet<PathfindingNode> _nodes;

        public PathfindingRegion(PathfindingNode originNode, HashSet<PathfindingNode> nodes, bool isDoor = false)
        {
            OriginNode = originNode;
            _nodes = nodes;
            IsDoor = isDoor;
        }

        public void Shutdown()
        {
            // Tell our neighbors we no longer exist ;-/
            var neighbors = new List<PathfindingRegion>(Neighbors);
            
            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                neighbor.Neighbors.Remove(this);
            }
        }

        /// <summary>
        /// Roughly how far away another region is
        /// </summary>
        /// <param name="otherRegion"></param>
        /// <returns></returns>
        public float Distance(PathfindingRegion otherRegion)
        {
            return PathfindingHelpers.OctileDistance(otherRegion.OriginNode, OriginNode);
        }

        /// <summary>
        /// Can the given args can traverse this region?
        /// </summary>
        /// <param name="reachableArgs"></param>
        /// <returns></returns>
        public bool RegionTraversable(ReachableArgs reachableArgs)
        {
            // The assumption is that all nodes in a region have the same pathfinding traits
            // As such we can just use the origin node for checking.
            return PathfindingHelpers.Traversable(reachableArgs.CollisionMask, reachableArgs.Access,
                OriginNode);
        }

        public void Add(PathfindingNode node)
        {
            _nodes.Add(node);
        }
        
        // HashSet wasn't working correctly so uhh we got this.
        public bool Equals(PathfindingRegion other)
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