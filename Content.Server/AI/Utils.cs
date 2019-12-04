using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI
{
    public static class Utils
    {
        // TODO: Should this be in robust or something?
        public static IEnumerable<IEntity> GetComponentOwnersInRange(GridCoordinates grid, Type component, float range)
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
