using System;
using System.Collections.Generic;
using Content.Shared.Pathfinding;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders
{
    public class JpsPathfinder : IPathfinder
    {
        public event Action<AStarRouteMessage> DebugRoute;

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public Queue<TileRef> FindPath(List<PathfindingChunk> graph, PathfindingArgs pathfindingArgs)
        {
            throw new NotImplementedException();
        }

        private PathfindingNode Search(int collisionMask, PathfindingNode initial, Direction direction, PathfindingNode start,
            PathfindingNode end)
        {
            initial.Neighbors.TryGetValue(direction, out var newNode);
            if (newNode == null || !Utils.Traversable(collisionMask, newNode.CollisionMask))
            {
                return null;
            }

            if (newNode == end)
            {
                return newNode;
            }

            return null;

        }
/*
        private Queue<PathfindingNode> SearchHorizontal(int collisionMask, PathfindingNode position, PathfindingNode end, Direction direction, float distance)
        {
            var result = new Queue<PathfindingNode>();

            while (true)
            {
                position.Neighbors.TryGetValue(direction, out var newPosition);
                if (newPosition == null || !Utils.Traversable(collisionMask, newPosition.CollisionMask))
                {
                    return null;
                }

                if (newPosition == end)
                {
                    result.Enqueue(newPosition);
                    return result;
                }

                distance += 1.0f;
                newPosition.Neighbors.TryGetValue(direction, out var x2);
                newPosition.Neighbors.TryGetValue(Direction.SouthWest, out var sw);
                newPosition.Neighbors.TryGetValue(Direction.NorthWest, out var nw);

                switch (direction)
                {
                    case Direction.East:
                        newPosition.Neighbors.TryGetValue(Direction.NorthEast, out var ne);
                        newPosition.Neighbors.TryGetValue(Direction.SouthEast, out var se);
                        if (!Utils.Traversable(se) && Utils.Traversable())
                        break;
                    case Direction.West:
                        break;
                }

                newPosition.Neighbors.TryGetValue()

                if (!Utils.Traversable())
            }
        }*/
    }
}
