using System.Collections.Generic;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Weapon.Ranged;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat.Nearby
{
    public sealed class NearbyRangedWeapons : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyRangedWeapons";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(RangedWeaponComponent), controller.VisionRadius))
            {
                yield return result;
            }

        }
    }
}
