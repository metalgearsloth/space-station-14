using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines
{
    public static class Utils
    {
        // These are just general behaviors that are useful, e.g. nearby mobs

        /// <summary>
        /// Get nearby humans etc.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proximity"></param>
        /// <returns></returns>
        public static IEnumerable<IEntity> NearbySpecies(IEntity target, float proximity)
        {
            var serverEntityManager = IoCManager.Resolve<IServerEntityManager>();
            foreach (var entity in serverEntityManager.GetEntitiesInRange(target, proximity))
            {
                if (entity.HasComponent<SpeciesComponent>())
                {
                    yield return entity;
                }
            }
        }

        /// <summary>
        /// Get nearby humans etc that also have a player attached
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proximity"></param>
        /// <returns></returns>
        public static IEnumerable<IEntity> NearbyPlayerSpecies(IEntity target, float proximity)
        {
            var serverEntityManager = IoCManager.Resolve<IServerEntityManager>();
            foreach (var entity in serverEntityManager.GetEntitiesInRange(target, proximity))
            {
                if (entity.HasComponent<SpeciesComponent>() &&
                    entity.HasComponent<BasicActorComponent>())
                {
                    yield return entity;
                }
            }
        }

        /// <summary>
        /// Get any nearby human. If you want this to be randomised then use RandomSpecies
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proximity"></param>
        /// <returns></returns>
        [CanBeNull]
        public static IEntity AnySpecies(IEntity target, float proximity)
        {
            var serverEntityManager = IoCManager.Resolve<IServerEntityManager>();
            foreach (var entity in serverEntityManager.GetEntitiesInRange(target, proximity))
            {
                if (entity.HasComponent<SpeciesComponent>())
                {
                    return entity;
                }
            }

            return null;
        }

        /// <summary>
        /// Get a nearby human etc that also have a player attached
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proximity"></param>
        /// <returns></returns>
        [CanBeNull]
        public static IEntity AnyPlayerSpecies(IEntity target, float proximity)
        {
            var serverEntityManager = IoCManager.Resolve<IServerEntityManager>();
            foreach (var entity in serverEntityManager.GetEntitiesInRange(target, proximity))
            {
                if (entity.HasComponent<SpeciesComponent>() &&
                    entity.HasComponent<BasicActorComponent>())
                {
                    return entity;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the nearest human etc
        /// </summary>
        /// <param name="target"></param>
        /// <param name="proximity"></param>
        /// <returns></returns>
        [CanBeNull]
        public static IEntity NearestSpecies(IEntity target, float proximity)
        {
            IEntity nearest = null;
            var potentialTargets = NearbySpecies(target, proximity).ToList();
            if (potentialTargets.Count > 0)
            {
                nearest = potentialTargets[0];
                var nearestDistance = (nearest.Transform.GridPosition.Position - target.Transform.GridPosition.Position)
                    .Length;
                foreach (var entity in potentialTargets)
                {
                    var entityDistance = (entity.Transform.GridPosition.Position - target.Transform.GridPosition.Position)
                        .Length;
                    if (entityDistance < nearestDistance)
                    {
                        nearestDistance = entityDistance;
                        nearest = entity;
                    }
                }
            }

            return nearest;
        }

        [CanBeNull]
        public static IEntity NearestPlayerSpecies(IEntity target, float proximity)
        {
            IEntity nearest = null;
            var potentialTargets = NearbyPlayerSpecies(target, proximity).ToList();
            if (potentialTargets.Count > 0)
            {
                nearest = potentialTargets[0];
                var nearestDistance = (nearest.Transform.GridPosition.Position - target.Transform.GridPosition.Position)
                    .Length;
                foreach (var entity in potentialTargets)
                {
                    var entityDistance = (entity.Transform.GridPosition.Position - target.Transform.GridPosition.Position)
                        .Length;
                    if (entityDistance < nearestDistance)
                    {
                        nearestDistance = entityDistance;
                        nearest = entity;
                    }
                }
            }

            return nearest;
        }

        [CanBeNull]
        public static IEntity RandomSpecies(IEntity target, float proximity)
        {
            var potentialTargets = NearbySpecies(target, proximity).ToList();
            if (potentialTargets.Count <= 0)
            {
                return null;
            }
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            return potentialTargets[robustRandom.Next(potentialTargets.Count - 1)];

        }

        [CanBeNull]
        public static IEntity RandomPlayerSpecies(IEntity target, float proximity)
        {
            var potentialTargets = NearbyPlayerSpecies(target, proximity).ToList();
            if (potentialTargets.Count <= 0)
            {
                return null;
            }
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            return potentialTargets[robustRandom.Next(potentialTargets.Count - 1)];

        }
    }
}
