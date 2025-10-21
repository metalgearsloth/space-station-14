using Content.Server.DeviceNetwork.Components;
using Content.Server.NodeContainer.EntitySystems;
using JetBrains.Annotations;
using Content.Server.Power.EntitySystems;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.NodeContainer;

namespace Content.Server.DeviceNetwork.Systems
{
    [UsedImplicitly]
    public sealed class ApcNetworkSystem : EntitySystem
    {
        [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ApcNetworkComponent, BeforePacketSentEvent>(OnBeforePacketSent);

            SubscribeLocalEvent<ApcNetworkComponent, ExtensionCableSystem.ProviderConnectedEvent>(OnProviderConnected);
            SubscribeLocalEvent<ApcNetworkComponent, ExtensionCableSystem.ProviderDisconnectedEvent>(OnProviderDisconnected);
        }

        /// <summary>
        /// Checks if both devices are connected to the same apc
        /// </summary>
        private void OnBeforePacketSent(EntityUid uid, ApcNetworkComponent receiver, BeforePacketSentEvent args)
        {
            if (!TryComp(args.Sender, out ApcNetworkComponent? sender)) return;

            if (sender.ConnectedNode?.NodeGroup == null || !sender.ConnectedNode.NodeGroup.Equals(receiver.ConnectedNode?.NodeGroup))
            {
                args.Cancel();
            }
        }

        private void OnProviderConnected(EntityUid uid, ApcNetworkComponent component, ExtensionCableSystem.ProviderConnectedEvent args)
        {
            if (!TryComp(args.Provider.Owner, out NodeContainerComponent? nodeContainer)) return;

            if (_nodeContainer.TryGetNode(nodeContainer, "power", out Shared.Power.Nodes.CableNode? node))
            {
                component.ConnectedNode = node;
            }
            else if (_nodeContainer.TryGetNode(nodeContainer, "output", out Shared.Power.Nodes.CableDeviceNode? deviceNode))
            {
                component.ConnectedNode = deviceNode;
            }

        }

        private void OnProviderDisconnected(EntityUid uid, ApcNetworkComponent component, ExtensionCableSystem.ProviderDisconnectedEvent args)
        {
            component.ConnectedNode = null;
        }
    }
}
