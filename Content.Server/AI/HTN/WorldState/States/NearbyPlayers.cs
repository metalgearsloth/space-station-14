using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Movement;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.WorldState.States
{
    public class NearbyPlayers : IStateData
    {
        public string Name => "NearbyPlayers";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public IEnumerable<IEntity> GetValue()
        {
            if (!_owner.TryGetComponent(out AiControllerComponent controller))
            {
                throw new InvalidOperationException();
            }

            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var nearbyPlayers = playerManager.GetPlayersInRange(_owner.Transform.GridPosition, (int) controller.VisionRadius);
            var validPlayers = new List<IEntity>();

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
