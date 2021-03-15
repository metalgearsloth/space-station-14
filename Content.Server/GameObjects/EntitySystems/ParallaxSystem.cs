#nullable enable
using Content.Shared.GameTicking;
using Content.Shared.Parallax;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Collections.Generic;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class ParallaxSystem : EntitySystem, IResettingEntitySystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public string RoundParallax { get; private set; }

        private List<string> _parallax = new();

        public override void Initialize()
        {
            base.Initialize();
            var protoManager = IoCManager.Resolve<IPrototypeManager>();

            foreach (var proto in protoManager.EnumeratePrototypes<ParallaxPrototype>())
            {
                _parallax.Add(proto.ID);
            }

            ChooseParallax();

            _playerManager.PlayerStatusChanged += HandlePlayer;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _playerManager.PlayerStatusChanged -= HandlePlayer;
        }

        // TODO: Look at how gastileoverlays do this probably?
        private void HandlePlayer(object? sender, SessionStatusEventArgs eventArgs)
        {
            switch (eventArgs.NewStatus)
            {
                case Robust.Shared.Enums.SessionStatus.Connected:
                    RaiseNetworkEvent(new ParallaxSystemMessage(RoundParallax), eventArgs.Session.ConnectedClient);
                    break;
                default:
                    break;
            }
        }

        private void ChooseParallax()
        {
            var robustRandom = IoCManager.Resolve<IRobustRandom>();

            RoundParallax = robustRandom.Pick(_parallax);
        }

        public void Reset()
        {
            ChooseParallax();
            RaiseNetworkEvent(new ParallaxSystemMessage(RoundParallax));
        }
    }
}
