using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public sealed class NearbyLaserWeapons : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyLaserWeapons";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(HitscanWeaponComponent), controller.VisionRadius))
            {
                yield return result;
            }

        }
    }
}
