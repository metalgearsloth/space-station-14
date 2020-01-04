using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Movement;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.WorldState.States
{
    [AiEnumerableState]
    public class NearbyPlayers : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyPlayers";

        public override IEnumerable<IEntity> GetValue()
        {
            if (!Owner.TryGetComponent(out AiControllerComponent controller))
            {
                yield break;
            }

            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var nearbyPlayers = playerManager.GetPlayersInRange(Owner.Transform.GridPosition, (int) controller.VisionRadius);

            foreach (var player in nearbyPlayers)
            {
                if (player.AttachedEntity.HasComponent<SpeciesComponent>())
                {
                    yield return player.AttachedEntity;
                }
            }
        }
    }
}
