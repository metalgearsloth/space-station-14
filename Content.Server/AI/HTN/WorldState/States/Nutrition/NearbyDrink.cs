using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Nutrition
{
    [AiEnumerableState]
    public sealed class NearbyDrink: EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyDrink";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(DrinkComponent), controller.VisionRadius))
            {
                yield return result;
            }

        }
    }
}
