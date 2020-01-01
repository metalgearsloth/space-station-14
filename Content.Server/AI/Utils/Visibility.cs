using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.AI.Utils
{
    public static class Visibility
    {
        // TODO: Should this be in robust or something?
        public static IEnumerable<IEntity> GetNearestEntities(GridCoordinates grid, Type component, float range)
        {
            var inRange = GetEntitiesInRange(grid, component, range).ToList();

            var sortedInRange = inRange.OrderBy(o => (o.Transform.GridPosition.Position - grid.Position).Length);

            return sortedInRange;
        }

        public static IEnumerable<IEntity> GetEntitiesInRange(GridCoordinates grid, Type component, float range)
        {
            var compManager = IoCManager.Resolve<IComponentManager>();
            foreach (var comp in compManager.GetAllComponents(component))
            {
                if (comp.Owner.Transform.GridPosition.GridID != grid.GridID || grid == GridCoordinates.Nullspace)
                {
                    continue;
                }

                if ((comp.Owner.Transform.GridPosition.Position - grid.Position).Length <= range)
                {
                    yield return comp.Owner;
                }
            }
        }

        public static IEnumerable<IEntity> GetNearestClothing(EquipmentSlotDefines.Slots slot, GridCoordinates grid, float range)
        {
            var inRange = GetClothingInRange(slot, grid, range).ToList();

            var sortedInRange = inRange.OrderBy(o => (o.Transform.GridPosition.Position - grid.Position).Length);

            return sortedInRange;
        }

        public static IEnumerable<IEntity> GetClothingInRange(EquipmentSlotDefines.Slots slot, GridCoordinates grid, float range)
        {
            foreach (var entity in GetNearestEntities(grid, typeof(ClothingComponent), range))
            {
                entity.TryGetComponent(out ClothingComponent clothingComponent);
                if ((clothingComponent.SlotFlags & EquipmentSlotDefines.SlotMasks[slot]) == 0)
                {
                    yield return entity;
                }
            }
        }
    }
}
