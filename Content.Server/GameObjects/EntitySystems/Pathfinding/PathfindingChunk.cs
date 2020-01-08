using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
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

        /// <summary>
        /// Get all nodes in this chunk
        /// </summary>
        /// <param name="collisionLayer"></param>
        /// <returns></returns>
        public IEnumerable<PathfindingNode> GetNodes()
        {
            foreach (var node in _nodes)
            {
                yield return node;
            }
        }

        /// <summary>
        /// Get all nodes in this chunk that this layer can traverse
        /// </summary>
        /// <param name="collisionLayer"></param>
        /// <returns></returns>
        public IEnumerable<PathfindingNode> GetNodes(int collisionLayer)
        {
            foreach (var node in _nodes)
            {
                foreach (var layer in node.CollisionLayers)
                {
                    if ((collisionLayer & layer) != 0)
                    {
                        yield return node;
                    }
                }
            }
        }

        public bool InBounds(TileRef tile)
        {
            if (_indices.X > tile.X || tile.X >= _indices.X + ChunkSize) return false;
            if (_indices.Y > tile.Y || tile.Y >= _indices.Y + ChunkSize) return false;
            return true;
        }

        /// <summary>
        /// Returns true if the node is on the outer edge
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool OnEdge(PathfindingNode node)
        {
            if (node.TileRef.X == _indices.X) return true;
            if (node.TileRef.Y == _indices.Y) return true;
            if (node.TileRef.X == _indices.X  + ChunkSize - 1) return true;
            if (node.TileRef.Y == _indices.Y  + ChunkSize - 1) return true;
            return false;
        }

        /// <summary>
        /// Returns true if the tile is on the outer edge
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public bool OnEdge(TileRef tile)
        {
            if (tile.X == _indices.X) return true;
            if (tile.Y == _indices.Y) return true;
            if (tile.X == _indices.X  + ChunkSize - 1) return true;
            if (tile.Y == _indices.Y  + ChunkSize - 1) return true;
            return false;
        }

        public void RefreshNodeNeighbors()
        {
            foreach (var node in _nodes)
            {
                var neighbors = GetTileNeighbors(node.TileRef);
                node.Neighbors = neighbors;
            }
        }

        private List<PathfindingNode> GetTileNeighbors(TileRef currentTile, bool allowDiagonals = true)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(GridId);
            var results = new List<PathfindingNode>();
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    if (x == 0 && y == 0) continue;
                    if (!allowDiagonals && Math.Abs(x) == 1 && Math.Abs(y) == 1) continue;

                    var tile = grid.GetTileRef(new MapIndices(currentTile.X + x, currentTile.Y + y));
                    if (!OnEdge(tile))
                    {
                        TryGetNode(tile, out var node);
                        results.Add(node);
                        continue;
                    }

                    foreach (var chunk in Neighbors)
                    {
                        if (chunk.TryGetNode(tile, out var node))
                        {
                            results.Add(node);
                            break;
                        }
                    }
                }
            }

            return results;
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

        /// <summary>
        ///  Checks whether the tile is instantiated in this chunk. Doesn't check whether it's a valid tile.
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public bool HasNode(TileRef tile)
        {
            foreach (var node in _nodes)
            {
                if (node.TileRef == tile)
                {
                    return true;
                }
            }

            return false;
        }

        private PathfindingNode CreateNode(TileRef tile, PathfindingChunk parent = null)
        {
            if (parent == null)
            {
                parent = this;
            }

            var node = new PathfindingNode(parent, tile, new List<PathfindingNode>());
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
