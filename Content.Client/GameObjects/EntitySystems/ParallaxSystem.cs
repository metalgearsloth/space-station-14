#nullable enable
using Content.Client.Parallax;
using Content.Shared.Parallax;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Collections.Generic;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class ParallaxSystem : EntitySystem
    {
        public bool Enabled { get; set; }

        public RoundParallax? Parallax { get; private set; }

        private ParallaxPrototype _prototype;

        private Dictionary<string, ParallaxPrototype> _parallaxPrototypes = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<ParallaxSystemMessage>(HandleParallax);
            var protoManager = IoCManager.Resolve<IPrototypeManager>();

            foreach (var proto in protoManager.EnumeratePrototypes<ParallaxPrototype>())
            {
                _parallaxPrototypes[proto.ID] = proto;
            }

            // Pick one for now
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            _prototype = _parallaxPrototypes[robustRandom.Pick(_parallaxPrototypes.Keys)];
            BuildParallax();

            // TODO: Listen to parallax value and change 
        }

        private void HandleParallax(ParallaxSystemMessage message)
        {
            if (!_parallaxPrototypes.TryGetValue(message.ID, out _prototype))
            {
                var robustRandom = IoCManager.Resolve<IRobustRandom>();
                var key = robustRandom.Pick(_parallaxPrototypes.Keys);
                _prototype = _parallaxPrototypes[robustRandom.Pick(_parallaxPrototypes.Keys)];
                Logger.WarningS("parallax", $"Received invalid parallax id of {message.ID} from server! Selected {key} randomly.");
            }
        }

        private void BuildParallax()
        {
            // TODO: Copy stuff from ParallaxManager
        }
    }
}
