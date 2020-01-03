using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Nutrition
{
    public sealed class NearbyFood : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyFood";

        public override IEnumerable<IEntity> GetValue()
        {
            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(FoodComponent), Controller.VisionRadius))
            {
                yield return result;
            }

        }
    }
}
