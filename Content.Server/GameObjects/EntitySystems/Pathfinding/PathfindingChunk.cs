using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    // TODO: have something that puts in a tileref and gets or creates a chunk for it as required

    public class PathfindingChunk
    {
        private IMapGrid _grid;
        private MapIndices _indices;

        // Nodes per chunk
        public static int ChunkSize => 8;
        private List<PathfindingNode> _nodes = new List<PathfindingNode>();

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
    }
}
