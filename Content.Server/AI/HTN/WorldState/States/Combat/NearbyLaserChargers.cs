using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Power.Chargers;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public sealed class NearbyLaserChargers : IStateData
    {
        public string Name => "NearbyLaserChargers";
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
                .GetNearestEntities(_owner.Transform.GridPosition, typeof(WeaponCapacitorChargerComponent), controller.VisionRadius)
                .ToList())
            {
                yield return result;
            }

        }
    }
}
