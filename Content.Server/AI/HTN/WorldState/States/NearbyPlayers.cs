using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Movement;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.WorldState.States
{
    public class NearbyPlayers : EnumerableStateData<IEntity>
    {
        public override string Name => "NearbyPlayers";

        public override IEnumerable<IEntity> GetValue()
        {
            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var nearbyPlayers = playerManager.GetPlayersInRange(Owner.Transform.GridPosition, (int) Controller.VisionRadius);

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
