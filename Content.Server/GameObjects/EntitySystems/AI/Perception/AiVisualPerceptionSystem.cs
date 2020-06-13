using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems.AI.Pathfinding;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Server.GameObjects.EntitySystems.AI.Perception
{
    /// <summary>
    /// Where and when the AI last saw a particular entity
    /// </summary>
    public sealed class RememberedPosition
    {
        public GridCoordinates GridCoordinates { get; private set; }
        public TimeSpan LastSeen { get; private set; }

        public RememberedPosition(GridCoordinates gridCoordinates, TimeSpan lastSeen)
        {
            GridCoordinates = gridCoordinates;
            LastSeen = lastSeen;
        }

        public void Update(GridCoordinates gridCoordinates)
        {
            GridCoordinates = gridCoordinates;
            LastSeen = IoCManager.Resolve<IGameTiming>().CurTime;
        }
    }

    /// <summary>
    /// Cache visibility calls for each entity because they're probably expensive
    /// </summary>
    public sealed class CachedVisibility
    {
        public TimeSpan LastRun { get; set; }
        public List<IEntity> Entities { get; set; }

        public CachedVisibility(TimeSpan lastRun, List<IEntity> entities)
        {
            LastRun = lastRun;
            Entities = entities;
        }
    }
    
    /// <summary>
    /// Two parts to this:
    /// 1. getting point-in-time visibility (which is cached for a bit)
    /// 2. Storing the position of when the AI last saw particular entities
    /// e.g. if it wants to try and find Joe Smith and saw him 30 seconds ago it goes to his last known position
    /// </summary>
    public sealed class AiVisualPerceptionSystem : EntitySystem
    {
        [Dependency] private IEntityManager _entityManager;
        [Dependency] private PathfindingSystem _pathfindingSystem;

        // In seconds
        private const double CacheTime = 1.0;
        /// <summary>
        /// GetNearby result gets stored for a short time
        /// </summary>
        private readonly Dictionary<IEntity, Dictionary<Type, CachedVisibility>> _cachedNearby = new Dictionary<IEntity, Dictionary<Type, CachedVisibility>>();
        // TODO: Probably need a max-size dictionary for this
        /// <summary>
        /// If we remember where we last saw a particular entity to find it later
        /// </summary>
        private readonly Dictionary<IEntity, Dictionary<IEntity, RememberedPosition>> _rememberedPositions = new Dictionary<IEntity, Dictionary<IEntity, RememberedPosition>>();

        /// <summary>
        /// If we've seen the entity previously get its last known position
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public RememberedPosition LastSeen(IEntity owner, IEntity target)
        {
            if (!_rememberedPositions.TryGetValue(owner, out var knownEntities))
            {
                return null;
            }

            return knownEntities.TryGetValue(target, out var remembered) ? remembered : null;
        }
        
        // TODO: Need an x / y converter to direction
        private Direction GetDirection(int x, int y)
        {
            if (x == -1)
            {
                
            }
        }

        /// <summary>
        /// AI LOS check
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool CanSee(IEntity owner, IEntity target)
        {
            // TODO: Need to handle diagonals I reckon
            // If we're calling this we should already know they're in range
            // Visibility doesn't need to be exact like physics does, tile-based is good enough
            // Speed > precision (for the most part)
            var mapManager = IoCManager.Resolve<IMapManager>();
            var grid = mapManager.GetGrid(owner.Transform.GridID);
            var startTile = grid.GetTileRef(owner.Transform.GridPosition);
            var endTile = grid.GetTileRef(target.Transform.GridPosition);
            var startNode = _pathfindingSystem.GetNode(startTile);
            var endNode = _pathfindingSystem.GetNode(endTile);

            // Bresenham's line algo
            // https://en.wikipedia.org/wiki/Bresenham%27s_line_algorithm
            
            // TODO: Look at https://steemit.com/programming/@woz.software/roguelike-line-of-sight-calculation
            // TODO: If horizontal blocked try vertical and vice versa where both y and x are incrementing
            var deltaX = (endTile.X - startTile.X);
            var deltaY = (endTile.Y - startTile.Y);
            var N = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
            var divN = N == 0 ? 0.0 : 1.0 / N;
            var xStep = deltaX * divN;
            var yStep = deltaY * divN;
            var (x, y) = (startTile.X, startTile.Y);
            var (lastX, lastY) = (x, y);

            for (var step = 0; step <= N; step++)
            {
                // TODO: Move me up
                x += (int) xStep;
                y += (int) yStep;
                var tile = new MapIndices(x, y);
                var node = _pathfindingSystem.GetNode(grid.GetTileRef(tile));
                // If we move diagonally then check that we can see the horizontal / vertical first
                if (Math.Abs(x) == 1 && Math.Abs(y) == 1)
                {
                    // We can see so keep going
                    if ((CollisionGroup.Opaque & (CollisionGroup) node.CollisionMask) != 0x0)
                    {
                        
                    }
                    // If going NW for example get West and North (maybe to bitshift, look at how JPS does it).
                    // And check if either is visible. Should actually be similar to the pathfinder now that I think about it...
                    // A* should have it
                    else
                    {
                        
                    }
                }
                // TODO: Get tile
                // TODO: If both x and y increment then we need to check either (x == 0 y + 1) or (x + 1 y == 0) for visibility
            }

            if (Math.Abs(endTile.Y - startTile.Y) < Math.Abs(endTile.X - startTile.X))
            {
                if (startTile.X > endTile.X)
                {
                    return PlotLineLow(grid, endTile, startTile);
                }
                else
                {
                    return PlotLineLow(grid, startTile, endTile);
                }
            }
            else
            {
                if (startTile.Y > endTile.Y)
                {
                    return PlotLineHigh(grid, endTile, startTile);
                }
                else
                {
                    return PlotLineHigh(grid, startTile, endTile);
                }
            }
        }

        private bool PlotLineLow(IMapGrid grid, TileRef startTile, TileRef endTile)
        {
            // See CanSee
            var deltaX = (endTile.X - startTile.X);
            var deltaY = (endTile.Y - startTile.Y);
            var yi = 1;
            if (deltaY < 0)
            {
                yi = -1;
                deltaY = -deltaY;
            }

            var diff = 2 * deltaY - deltaX;
            var y = startTile.Y;

            for (var x = 0; x <= endTile.X; x++)
            {
                var tile = grid.GetTileRef(new MapIndices(x, y));
                var node = _pathfindingSystem.GetNode(tile);
                if ((CollisionGroup.Opaque & (CollisionGroup) node.CollisionMask) == 0x0)
                {
                    return false;
                }

                if (diff > 0)
                {
                    y += yi;
                    diff -= 2 * deltaX;
                }

                diff += 2 * deltaY;
            }
            return true;
        }

        private bool PlotLineHigh(IMapGrid grid, TileRef startTile, TileRef endTile)
        {
            // See CanSee
            var deltaX = (endTile.X - startTile.X);
            var deltaY = (endTile.Y - startTile.Y);
            var xi = 1;
            if (deltaX < 0)
            {
                xi = -1;
                deltaX = -deltaX;
            }

            var diff = 2 * deltaX - deltaY;
            var x = startTile.X;

            for (var y = 0; y <= endTile.Y; y++)
            {
                var tile = grid.GetTileRef(new MapIndices(x, y));
                var node = _pathfindingSystem.GetNode(tile);
                if ((CollisionGroup.Opaque & (CollisionGroup) node.CollisionMask) == 0x0)
                {
                    return false;
                }

                if (diff > 0)
                {
                    x += xi;
                    diff -= 2 * deltaY;
                }

                diff += 2 * deltaX;
            }
            return true;
        }

        /// <summary>
        /// Gets entities near the AI with the matching component. Also does LOS checks and remembers the position for future reference.
        /// TODO: Should this also pull in rememberedPositions?
        /// </summary>
        /// <param name="owner">Caller entity</param>
        /// <param name="type">Component to get</param>
        /// <param name="lineOfSight">Whether we need LOS on the item. If it's not important we don't need it (e.g. vending machines)</param>
        /// <param name="remember">Whether we should store the perceived coordinates for future</param>
        /// <returns></returns>
        public List<IEntity> GetNearby(IEntity owner, Type type, bool lineOfSight = true, bool remember = false)
        {
            if (!_cachedNearby.ContainsKey(owner))
            {
                _cachedNearby[owner] = new Dictionary<Type, CachedVisibility>();
            }
            
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            if (_cachedNearby[owner].TryGetValue(type, out var last))
            {
                if (currentTime.TotalSeconds - last.LastRun.TotalSeconds < CacheTime)
                {
                    return last.Entities;
                }
            }
            
            var controller = owner.GetComponent<AiControllerComponent>();
            var results = new List<IEntity>();

            foreach (var entity in _entityManager.GetEntities(new TypeEntityQuery(type)))
            {
                if (entity.Transform.MapID != owner.Transform.MapID)
                {
                    continue;
                }

                // Out of range
                var distance = (entity.Transform.MapPosition.Position - owner.Transform.MapPosition.Position).Length;
                if (distance >
                    controller.VisionRadius)
                {
                    continue;
                }

                if (ContainerHelpers.IsInContainer(entity))
                {
                    continue;
                }
                
                // If they're within a tile don't need to check
                if (lineOfSight && distance >= 1.5f && !CanSee(owner, entity))
                {
                    continue;
                }

                // At this point it's confirmed we know about it
                // Should really only be used for stuff like mob entities; don't need to remember stuff like food or puddles or w/e
                if (remember)
                {
                    if (_rememberedPositions[owner].TryGetValue(entity, out var remembered))
                    {
                        remembered.Update(entity.Transform.GridPosition);
                    }
                    else
                    {
                        _rememberedPositions[owner][entity] = new RememberedPosition(entity.Transform.GridPosition, currentTime);
                    }
                }

                results.Add(entity);
            }
            
            // Cache the call because if the AI is planning 4 times a second this is gonna add up
            if (_cachedNearby[owner].TryGetValue(type, out var existing))
            {
                existing.LastRun = currentTime;
                existing.Entities = results;
            }
            else
            {
                _cachedNearby[owner][type] = new CachedVisibility(currentTime, results);
            }
            
            return results;
        }
    }
}