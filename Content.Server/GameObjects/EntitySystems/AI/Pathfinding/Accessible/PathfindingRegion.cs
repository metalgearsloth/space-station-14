using System;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Accessible
{
    /// <summary>
    /// A group of homogenous PathfindingNodes inside a single chunk
    /// </summary>
    /// Makes the graph smaller and quicker to traverse
    public class PathfindingRegion : IEquatable<PathfindingRegion>
    {
        // Originally these could be made up of any shape but it's more memory efficient to just use rectangles again like chunks
        // And then we don't need to store the references our individual nodes, we just check the arrays

        /// <summary>
        ///     Bottom-left reference node of the region
        /// </summary>
        public PathfindingNode OriginNode { get; }

        /// <summary>
        ///     Width of the nodes
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Width of the nodes
        /// </summary>
        public int Width { get; }

        public PathfindingChunk ParentChunk => OriginNode.ParentChunk;

        public bool Deleted { get; private set; }

        public PathfindingRegion(PathfindingNode originNode, int height, int width)
        {
            OriginNode = originNode;
            Height = height;
            Width = width;
        }

        public void Shutdown()
        {
            Deleted = true;
        }

        public void BuildEdges()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PathfindingRegion> GetNeighborRegions()
        {
            yield break;
        }

        public IEnumerable<PathfindingNode> GetNodes()
        {
            var (offsetX, offsetY) = (OriginNode.TileRef.X - ParentChunk.Indices.X,
                OriginNode.TileRef.Y - ParentChunk.Indices.Y);
            
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    yield return ParentChunk.Nodes[offsetX + x, offsetY + y];
                }
            }
        }

        public IEnumerable<PathfindingNode> GetBorderNodes()
        {
            var (offsetX, offsetY) = (OriginNode.TileRef.X - ParentChunk.Indices.X,
                OriginNode.TileRef.Y - ParentChunk.Indices.Y);
            
            foreach (var x in new[] {0, Width - 1})
            {
                yield return ParentChunk.Nodes[offsetX + x, 0];
                yield return ParentChunk.Nodes[offsetX + x, Height - 1];
            }

            foreach (var y in new[] {0, Height - 1})
            {
                if (y == 0 || y == Height - 1) continue;
                
                yield return ParentChunk.Nodes[0, offsetY + y];
                yield return ParentChunk.Nodes[Width - 1, offsetY + y];
            }
        }

        public IEnumerable<PathfindingNode> GetInteriorNodes()
        {
            var (offsetX, offsetY) = (OriginNode.TileRef.X - ParentChunk.Indices.X,
                OriginNode.TileRef.Y - ParentChunk.Indices.Y);

            for (var x = 1; x < Width - 1; x++)
            {
                for (var y = 1; y < Height - 1; y++)
                {
                    yield return ParentChunk.Nodes[offsetX + x, offsetY + y];
                }
            }
        }

        public bool InBounds(PathfindingNode node)
        {
            if (node.TileRef.X < OriginNode.TileRef.X || node.TileRef.Y < OriginNode.TileRef.Y)
            {
                return false;
            }

            if (node.TileRef.X >= OriginNode.TileRef.X + Width ||
                node.TileRef.Y >= OriginNode.TileRef.Y + Height)
            {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Roughly how far away another region is by nearest node
        /// </summary>
        /// <param name="otherRegion"></param>
        /// <returns></returns>
        public float Distance(PathfindingRegion otherRegion)
        {
            // JANK
            var xDistance = otherRegion.OriginNode.TileRef.X - OriginNode.TileRef.X;
            var yDistance = otherRegion.OriginNode.TileRef.Y - OriginNode.TileRef.Y;

            if (xDistance > 0)
            {
                xDistance -= Width;
            }
            else if (xDistance < 0)
            {
                xDistance = Math.Abs(xDistance + otherRegion.Width);
            }
            
            if (yDistance > 0)
            {
                yDistance -= Height;
            }
            else if (yDistance < 0)
            {
                yDistance = Math.Abs(yDistance + otherRegion.Height);
            }
            
            return PathfindingHelpers.OctileDistance(xDistance, yDistance);
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

        // HashSet wasn't working correctly so uhh we got this.
        public bool Equals(PathfindingRegion other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Height != other.Height || Width != other.Width) return false;
            return GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return OriginNode.GetHashCode();
        }
    }
}