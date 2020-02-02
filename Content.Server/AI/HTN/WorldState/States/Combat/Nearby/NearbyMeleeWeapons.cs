using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Utils;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public sealed class NearbyMeleeWeapons : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyMeleeWeapons";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            foreach (var result in Visibility
                .GetNearestEntities(Owner.Transform.GridPosition, typeof(MeleeWeaponComponent), controller.VisionRadius))
            {
                yield return result;
            }

        }
    }
}
