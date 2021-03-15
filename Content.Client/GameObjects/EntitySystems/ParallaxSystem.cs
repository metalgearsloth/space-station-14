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
        // TODO for features
        // Have client-request parallax instead of having the server blindly send it.
        // Have options for parallax (None / Server / Random / Specific ones)
        // Needs to be cvar + in options menu + console command
        // Have parallax blown up to screen size? Maybe have this settable in prototype i.e. stretch / tile
        // Planet would stretch but gas owuld tile?
        // Tests to make sure everything's valid
        // Support for generated stuff too (config should be their own prototypes).
        // Command so admins can set parallax for a round.

        public RoundParallax? Parallax { get; private set; }

        private Dictionary<string, ParallaxPrototype> _parallaxPrototypes = new();

        public string Mode { get; set; } = "None";

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
            BuildParallax(Mode);

            // TODO: Listen to parallax value and change 
        }

        private void HandleParallax(ParallaxSystemMessage message)
        {
            BuildParallax(message.ID);
        }

        private void BuildParallax(string mode)
        {
            switch (mode)
            {
                // TODO: Request from server when cvar changed
                // When other values change update parallax immediately but if it's server wait to receive the message
                case "Server":
                    RaiseNetworkEvent(new RequestParallaxMessage());
                    break;
                case "None":
                    SetParallax(null);
                    break;
                case "Random":
                    var robustRandom = IoCManager.Resolve<IRobustRandom>();
                    SetParallax(_parallaxPrototypes[robustRandom.Pick(_parallaxPrototypes.Keys)]);
                    break;
                default:
                    if (!_parallaxPrototypes.TryGetValue(mode, out var prototype))
                    {
                        Logger.ErrorS("parallax", $"Tried to get parallax prototype {mode} which doesn't exist!");
                        break;
                    }
                    else
                    {
                        SetParallax(prototype);
                        break;
                    }
                    break;
            }
        }

        private void SetParallax(ParallaxPrototype? prototype)
        {
            if (prototype == null)
            {
                if (Parallax != null)
                {
                    Logger.InfoS("parallax", "Disabled parallax");
                    Parallax = null;
                }
                
                return;
            }

            var parallax = new RoundParallax();

            // TODO: Copy from ParallaxManager but also texture loading and shiznit.

            Parallax = parallax;
            Logger.InfoS("parallax", $"Set parallax to {prototype.ID}");
        }
    }
}
