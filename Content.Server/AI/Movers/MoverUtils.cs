using System.Collections.Generic;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.Routines.Movers
{
    public static class MoverUtils
    {
        /// <summary>
        /// Checks whether the entity can reach the desired target with the desired heuristic
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="target"></param>
        /// <param name="heuristic"></param>
        /// <returns>Route and result; returns route so you can save double-pathing</returns>
        public static CanReachResult CanReach(IEntity entity, GridCoordinates target, PathHeuristic? heuristic = null)
        {
            var pathfinder = IoCManager.Resolve<IPathfinder>();
            List<TileRef> route;
            if (heuristic != null)
            {
                route = pathfinder.FindPath(entity.Transform.GridPosition, target, 0.0f, heuristic.Value);
            }
            else
            {
                route = pathfinder.FindPath(entity.Transform.GridPosition, target, 0.0f);
            }

            if (route.Count > 1)
            {
                return new CanReachResult(true, route);
            }

            return new CanReachResult(false, route);
        }
    }

    public struct CanReachResult
    {
        public bool Result;
        public List<TileRef> Route;

        public CanReachResult(bool result, List<TileRef> route)
        {
            Result = result;
            Route = route;
        }
    }
}
