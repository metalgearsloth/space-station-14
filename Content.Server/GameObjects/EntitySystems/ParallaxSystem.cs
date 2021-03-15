#nullable enable
using Content.Shared.GameTicking;
using Content.Shared.Parallax;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Collections.Generic;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class ParallaxSystem : EntitySystem, IResettingEntitySystem
    {
        // So parallax is R O B U S t so client can get it in multiple ways
        // One way is that the server has a specific parallax that's in use so client can just request that.
        public string RoundParallax { get; private set; } = default!;

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
            SubscribeNetworkEvent<RequestParallaxMessage>(HandleRequest);
        }

        private void HandleRequest(RequestParallaxMessage message, EntitySessionEventArgs eventArgs)
        {
            RaiseNetworkEvent(new ParallaxSystemMessage(RoundParallax), eventArgs.SenderSession.ConnectedClient);
        }

        private void ChooseParallax()
        {
            var robustRandom = IoCManager.Resolve<IRobustRandom>();

            RoundParallax = robustRandom.Pick(_parallax);
            Logger.InfoS("parallax", $"Set round parallax to {_parallax}");
        }

        public void Reset()
        {
            ChooseParallax();
        }
    }
}
