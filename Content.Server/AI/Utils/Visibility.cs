using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace AIUtils
{
    public static class Visibility
    {
        // TODO: Should this be in robust or something?
        public static IEnumerable<IEntity> GetNearestEntities(GridCoordinates grid, Type component, float range)
        {
            var inRange = new List<IEntity>();
            var compManager = IoCManager.Resolve<IComponentManager>();
            foreach (var comp in compManager.GetAllComponents(component))
            {
                if (comp.Owner.Transform.GridPosition.GridID != grid.GridID)
                {
                    continue;
                }

                if ((comp.Owner.Transform.GridPosition.Position - grid.Position).Length <= range)
                {
                    inRange.Add(comp.Owner);
                }
            }

            if (inRange.Count == 0)
            {
                return null;
            }

            var sortedInRange = inRange.OrderBy(o => (o.Transform.GridPosition.Position - grid.Position).Length);

            return sortedInRange;
        }

        public static IEnumerable<IEntity> GetEntitiesInRange(GridCoordinates grid, Type component, float range)
        {
            var compManager = IoCManager.Resolve<IComponentManager>();
            foreach (var comp in compManager.GetAllComponents(component))
            {
                if (comp.Owner.Transform.GridPosition.GridID != grid.GridID)
                {
                    continue;
                }

                if ((comp.Owner.Transform.GridPosition.Position - grid.Position).Length <= range)
                {
                    yield return comp.Owner;
                }
            }
        }
    }
}
