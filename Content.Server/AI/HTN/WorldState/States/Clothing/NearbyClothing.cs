using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Clothing
{
    public class NearbyClothing : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyClothing";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(ClothingComponent), controller.VisionRadius))
            {
                var itemComponent = result.GetComponent<ItemComponent>();
                if (itemComponent.IsEquipped) continue;
                yield return result;
            }
        }
    }
}
