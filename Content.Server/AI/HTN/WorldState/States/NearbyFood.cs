using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States
{
    public class NearbyFood : IStateData
    {
        public string Name => "NearbyFood";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public IEnumerable<IEntity> GetValue()
        {
            if (!_owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield return null;
            }

            foreach (var result in Visibility
                .GetNearestEntities(_owner.Transform.GridPosition, typeof(FoodComponent), controller.VisionRadius)
                .ToList())
            {
                yield return result;
            }

        }
    }
}
