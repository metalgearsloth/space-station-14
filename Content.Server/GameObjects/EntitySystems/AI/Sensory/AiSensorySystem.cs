using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.AI.Sensory
{
    /// <summary>
    /// Handles AI perception for known entities
    /// </summary>
    /// TODO Vision, audio, known, etc.
    public sealed class AiSensorySystem : EntitySystem
    {
#pragma warning disable 649
        [Dependency] private IEntityManager _entityManager;
        [Dependency] private IGameTiming _gameTiming;
        [Dependency] private IMapManager _mapManager;
#pragma warning restore 649
        
        private const double MinCacheTime = 2.0;
        
        // Caches
        private readonly Dictionary<IEntity, Dictionary<Type, (TimeSpan CacheTime, List<IEntity> Results)>> _cachedNearest = 
                     new Dictionary<IEntity, Dictionary<Type, (TimeSpan, List<IEntity>)>>();

        public override void Shutdown()
        {
            base.Shutdown();
            _cachedNearest.Clear();
        }

        // TODO: Add a "GetKnownEntities" method that combines multiple sources (audio etc.) Will need a bunch of work
        // For now this is just a refactor of the existing stuff into 1 location
        
        /// <summary>
        /// Return the nearest entities to us with the relevant component.
        /// Sorted by distance
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="allowInContainer">Return entities in containers</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>Sorted in order of nearest first</returns>
        public IEnumerable<IEntity> GetNearestEntities<T>(IEntity entity, bool allowInContainer = false) where T : IComponent
        {
            var currentTime = _gameTiming.CurTime;
            
            // Check cache
            if (!_cachedNearest.TryGetValue(entity, out var cachedNearest))
            {
                _cachedNearest[entity] = new Dictionary<Type, (TimeSpan, List<IEntity>)>();
            } 
            // If cache and it isn't stale
            else if (cachedNearest != null && cachedNearest.TryGetValue(typeof(T), out var lastCache) && 
                       currentTime.TotalSeconds - lastCache.CacheTime.TotalSeconds <= MinCacheTime)
            {
                foreach (var result in lastCache.Results)
                {
                    if (result.Deleted) continue;
                    yield return result;
                }

                yield break;
            }
            
            var gridPosition = entity.Transform.GridPosition;
            var range = entity.GetComponent<AiControllerComponent>().VisionRadius;
            var inRange = GetEntitiesInRange<T>(gridPosition, range, allowInContainer).ToList();
            var sortedInRange = inRange.OrderBy(o => o.Transform.GridPosition.Distance(_mapManager, gridPosition)).ToList();
            _cachedNearest[entity][typeof(T)] = (currentTime, sortedInRange);
            
            foreach (var result in sortedInRange)
            {
                yield return result;
            }
        }

        private IEnumerable<IEntity> GetEntitiesInRange<T>(GridCoordinates gridCoordinates, float range, bool allowInContainer = false) where T : IComponent
        {
            foreach (var entity in _entityManager.GetEntities(new TypeEntityQuery(typeof(T))))
            {
                if (gridCoordinates.GridID != entity.Transform.GridID)
                {
                    continue;
                }
                
                if (entity.Transform.GridPosition.Distance(_mapManager, gridCoordinates) >
                    range)
                {
                    continue;
                }

                if (!allowInContainer && ContainerHelpers.IsInContainer(entity))
                {
                    continue;
                }

                yield return entity;
            }
        }
    }
}