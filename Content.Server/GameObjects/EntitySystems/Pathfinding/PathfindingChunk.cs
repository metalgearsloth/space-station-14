using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    // TODO: Look at using more indexing like the snapgrids
    // TODO: Use Dictionary in the graph to store the MapIndices

    public class PathfindingChunk
    {
        public GridId GridId { get; }

        public MapIndices Indices => _indices;
        private readonly MapIndices _indices;

        // Nodes per chunk row
        public static int ChunkSize => 16;
        private PathfindingNode[,] _nodes = new PathfindingNode[ChunkSize,ChunkSize];
        public Dictionary<Direction, PathfindingChunk> Neighbors { get; } = new Dictionary<Direction, PathfindingChunk>(8);

        public PathfindingChunk(GridId gridId, MapIndices indices)
        {
            GridId = gridId;
            _indices = indices;
        }

        // TODO: Neighbors should use array offsets.
        // TODO: Graph should use array of chunks instead

        public IEnumerable<PathfindingNode> GetNodes()
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    yield return _nodes[x, y];
                }
            }
        }

        public void Initialize()
        {
            var grid = IoCManager.Resolve<IMapManager>().GetGrid(GridId);
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var tileRef = grid.GetTileRef(new MapIndices(x + _indices.X, y + _indices.Y));
                    CreateNode(tileRef);
                }
            }

            RefreshNodeNeighbors();
        }

        /// <summary>
        /// Updates all internal nodes with references to every other internal node
        /// </summary>
        private void RefreshNodeNeighbors()
        {
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    var node = _nodes[x, y];
                    // West
                    if (x != 0)
                    {
                        if (y != ChunkSize - 1)
                        {
                            node.AddNeighbor(Direction.NorthWest, _nodes[x - 1, y + 1]);
                        }
                        node.AddNeighbor(Direction.West, _nodes[x - 1, y]);
                        if (y != 0)
                        {
                            node.AddNeighbor(Direction.SouthWest, _nodes[x - 1, y - 1]);
                        }
                    }

                    // Same column
                    if (y != ChunkSize - 1)
                    {
                        node.AddNeighbor(Direction.North, _nodes[x, y + 1]);
                    }

                    if (y != 0)
                    {
                        node.AddNeighbor(Direction.South, _nodes[x, y - 1]);
                    }

                    // East
                    if (x != ChunkSize - 1)
                    {
                        if (y != ChunkSize - 1)
                        {
                            node.AddNeighbor(Direction.NorthEast, _nodes[x + 1, y + 1]);
                        }
                        node.AddNeighbor(Direction.East, _nodes[x + 1, y]);
                        if (y != 0)
                        {
                            node.AddNeighbor(Direction.SouthEast, _nodes[x + 1, y - 1]);
                        }
                    }
                }
            }
        }

        public bool AreNeighbors(PathfindingChunk otherChunk)
        {
            if (GridId != otherChunk.GridId) return false;
            if (Math.Abs(Indices.X - otherChunk.Indices.X) > ChunkSize) return false;
            if (Math.Abs(Indices.Y - otherChunk.Indices.Y) > ChunkSize) return false;
            return true;
        }

        public bool TryUpdateTile(TileRef tile)
        {
            if (!InBounds(tile)) return false;
            // TODO: Need to get the offset
            var offsetX = tile.X - Indices.X;
            var offsetY = tile.Y - Indices.Y;
            var node = _nodes[offsetX, offsetY];
            node.UpdateTile(tile);
            return true;
        }

        /// <summary>
        /// This will work both ways
        /// </summary>
        /// <param name="chunk"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddNeighbor(PathfindingChunk chunk)
        {
            if (chunk == this) return;
            if (Neighbors.ContainsValue(chunk))
            {
                return;
            }

            Direction direction;
            if (chunk.Indices.X < _indices.X)
            {
                if (chunk.Indices.Y > _indices.Y)
                {
                    direction = Direction.NorthWest;
                } else if (chunk.Indices.Y < _indices.Y)
                {
                    direction = Direction.SouthWest;
                }
                else
                {
                    direction = Direction.West;
                }
            }
            else if (chunk.Indices.X > _indices.X)
            {
                if (chunk.Indices.Y > _indices.Y)
                {
                    direction = Direction.NorthEast;
                } else if (chunk.Indices.Y < _indices.Y)
                {
                    direction = Direction.East;
                }
                else
                {
                    direction = Direction.SouthEast;
                }
            }
            else
            {
                if (chunk.Indices.Y > _indices.Y)
                {
                    direction = Direction.North;
                } else if (chunk.Indices.Y < _indices.Y)
                {
                    direction = Direction.South;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            Neighbors.TryAdd(direction, chunk);

            //foreach (var node in GetBorderNodes(direction))
            foreach (var node in GetBorderNodes(direction))
            {
                foreach (var counter in chunk.GetCounterpartNodes(direction))
                {
                    var xDiff = node.TileRef.X - counter.TileRef.X;
                    var yDiff = node.TileRef.Y - counter.TileRef.Y;

                    if (Math.Abs(xDiff) <= 1 && Math.Abs(yDiff) <= 1)
                    {
                        node.AddNeighbor(counter);
                        counter.AddNeighbor(node);
                    }
                }
            }

            chunk.Neighbors.TryAdd(OppositeDirection(direction), this);

            if (Neighbors.Count > 8)
            {
                throw new InvalidOperationException();
            }
        }

        // TODO Ya dumbshit
        private Direction OppositeDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.East:
                    return Direction.West;
                case Direction.NorthEast:
                    return Direction.SouthWest;
                case Direction.North:
                    return Direction.South;
                case Direction.NorthWest:
                    return Direction.SouthEast;
                case Direction.West:
                    return Direction.East;
                case Direction.SouthWest:
                    return Direction.NorthEast;
                case Direction.South:
                    return Direction.North;
                case Direction.SouthEast:
                    return Direction.NorthWest;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        // TODO I was too tired to think of an easier system. Could probably just google an array wraparound
        private IEnumerable<PathfindingNode> GetCounterpartNodes(Direction direction)
        {
            switch (direction)
            {
                case Direction.West:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[ChunkSize - 1, i];
                    }
                    break;
                case Direction.SouthWest:
                    yield return _nodes[ChunkSize - 1, ChunkSize - 1];
                    break;
                case Direction.South:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[i, ChunkSize - 1];
                    }
                    break;
                case Direction.SouthEast:
                    yield return _nodes[0, ChunkSize - 1];
                    break;
                case Direction.East:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[0, i];
                    }
                    break;
                case Direction.NorthEast:
                    yield return _nodes[0, 0];
                    break;
                case Direction.North:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[i, 0];
                    }
                    break;
                case Direction.NorthWest:
                    yield return _nodes[ChunkSize - 1, 0];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public IEnumerable<PathfindingNode> GetBorderNodes(Direction direction)
        {
            switch (direction)
            {
                case Direction.East:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[ChunkSize - 1, i];
                    }
                    break;
                case Direction.NorthEast:
                    yield return _nodes[ChunkSize - 1, ChunkSize - 1];
                    break;
                case Direction.North:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[i, ChunkSize - 1];
                    }
                    break;
                case Direction.NorthWest:
                    yield return _nodes[0, ChunkSize - 1];
                    break;
                case Direction.West:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[0, i];
                    }
                    break;
                case Direction.SouthWest:
                    yield return _nodes[0, 0];
                    break;
                case Direction.South:
                    for (var i = 0; i < ChunkSize; i++)
                    {
                        yield return _nodes[i, 0];
                    }
                    break;
                case Direction.SouthEast:
                    yield return _nodes[ChunkSize - 1, 0];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public bool InBounds(TileRef tile)
        {
            if (tile.X < _indices.X || tile.Y < _indices.Y) return false;
            if (tile.X >= _indices.X + ChunkSize || tile.Y >= _indices.Y + ChunkSize) return false;
            return true;
        }

        // TODO: Add which edge it's on

        /// <summary>
        /// Returns true if the tile is on the outer edge
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

        private void CreateNode(TileRef tile, PathfindingChunk parent = null)
        {
            if (parent == null)
            {
                parent = this;
            }

            var node = new PathfindingNode(parent, tile);
            var offsetX = tile.X - Indices.X;
            var offsetY = tile.Y - Indices.Y;
            _nodes[offsetX, offsetY] = node;
        }

        /// <summary>
        /// Updates the node's tileref if it exists, otherwise will create it.
        /// TODO: If we use indices on the main graph we probably don't need this
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

            throw new InvalidOperationException();
        }
    }
}
