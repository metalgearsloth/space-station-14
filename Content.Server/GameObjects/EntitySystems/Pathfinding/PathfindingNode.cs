using System;
using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    public class PathfindingNode
    {
        // TODO: Add access ID here
        public PathfindingChunk ParentChunk => _parentChunk;
        private readonly PathfindingChunk _parentChunk;
        public TileRef TileRef { get; private set; }
        public List<int> CollisionLayers { get; }
        public int CollisionMask { get; private set; }

        public PathfindingNode(PathfindingChunk parent, TileRef tileRef, List<int> collisionLayers = null)
        {
            _parentChunk = parent;
            TileRef = tileRef;
            if (collisionLayers == null)
            {
                CollisionLayers = new List<int>();
            }
            else
            {
                CollisionLayers = collisionLayers;
            }
        }

        public void UpdateTile(TileRef newTile)
        {
            TileRef = newTile;
        }

        public void AddCollisionLayer(int layer)
        {
            CollisionLayers.Add(layer);
            GenerateMask();
        }

        public void RemoveCollisionLayer(int layer)
        {
            CollisionLayers.Remove(layer);
            GenerateMask();
        }

        private void GenerateMask()
        {
            // TODO
            CollisionMask = 0;
        }
    }
}
