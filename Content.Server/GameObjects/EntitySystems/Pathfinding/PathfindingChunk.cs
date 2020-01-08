using System;
using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    // TODO: have something that puts in a tileref and gets or creates a chunk for it as required

    public class PathfindingChunk
    {
        public GridId GridId { get; }

        public MapIndices Indices => _indices;
        private readonly MapIndices _indices;

        // Nodes per chunk
        public static int ChunkSize => 8;
        public IReadOnlyCollection<PathfindingNode> Nodes => _nodes;
        private List<PathfindingNode> _nodes = new List<PathfindingNode>(ChunkSize * ChunkSize);
        public List<PathfindingChunk> Neighbors { get; } = new List<PathfindingChunk>(8);

        public PathfindingChunk(GridId gridId, MapIndices indices)
        {
            GridId = gridId;
            _indices = indices;
        }

        public void AddTile(TileRef tile)
        {
            var node = CreateNode(tile);
            _nodes.Add(node);
        }

        public void AddNeighbor(PathfindingChunk chunk)
        {
            if (chunk == this) return;
            if (Neighbors.Contains(chunk))
            {
                return;
            }
            Neighbors.Add(chunk);
            if (Neighbors.Count > 8)
            {
                throw new InvalidOperationException();
            }
        }

        public bool InBounds(TileRef tile)
        {
            if (tile.X < _indices.X || tile.Y < _indices.Y) return false;
            if (tile.X >= _indices.X + ChunkSize || tile.Y >= _indices.Y + ChunkSize) return false;
            return true;
        }

        /// <summary>
        /// Returns true if the tile is on the outer edge
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public bool OnEdge(PathfindingNode node)
        {
            if (node.TileRef.X == _indices.X) return true;
            if (node.TileRef.Y == _indices.Y) return true;
            if (node.TileRef.X == _indices.X  + ChunkSize - 1) return true;
            if (node.TileRef.Y == _indices.Y  + ChunkSize - 1) return true;
            return false;
        }

        public bool TryGetNode(TileRef tile, out PathfindingNode outNode)
        {
            outNode = null;
            if (!InBounds(tile)) return false;
            foreach (var node in _nodes)
            {
                if (node.TileRef.GridIndices == tile.GridIndices)
                {
                    outNode = node;
                    return true;
                }
            }

            throw new InvalidOperationException();
        }

        private PathfindingNode CreateNode(TileRef tile, PathfindingChunk parent = null)
        {
            if (parent == null)
            {
                parent = this;
            }

            var node = new PathfindingNode(parent, tile);
            return node;
        }

        /// <summary>
        /// Updates the node's tileref if it exists, otherwise will create it.
        /// </summary>
        /// <param name="tile"></param>
        /// <returns>true if the tile is in this chunk</returns>
        public bool TryUpdateNode(TileRef tile)
        {
            if (!InBounds(tile)) return false;
            foreach (var node in _nodes)
            {
                if (node.TileRef.GridIndices == tile.GridIndices)
                {
                    node.UpdateTile(tile);
                    return true;
                }
            }

            var updated = CreateNode(tile);

            _nodes.Add(updated);
            return true;
        }
    }
}
