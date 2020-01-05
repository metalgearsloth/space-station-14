using Content.Shared.GameObjects.Components.AI;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client.GameObjects.Components.AI
{
    [RegisterComponent]
    public sealed class ClientAiDebugComponent : SharedAiDebugComponent
    {
        private AiDebugOverlay _overlay;

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);
            switch (message)
            {
                case AiPlanMessage msg:
                    ReceivedPlan(msg);
                    break;
            }
        }

        public override void OnAdd()
        {
            base.OnAdd();
            var overlayManager = IoCManager.Resolve<IOverlayManager>();
            _overlay = new AiDebugOverlay();
            overlayManager.AddOverlay(_overlay);
        }

        public override void OnRemove()
        {
            base.OnRemove();
            var overlaymanager = IoCManager.Resolve<IOverlayManager>();
            overlaymanager.RemoveOverlay(nameof(AiDebugOverlay));
        }

        private void ReceivedPlan(AiPlanMessage message)
        {
            // TODO
        }
    }

    public sealed class AiDebugOverlay : Overlay
    {
        public AiDebugOverlay() : base(nameof(AiDebugOverlay))
        {
            Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance();
        }

        protected override void Draw(DrawingHandleBase handle)
        {
            var componentManager = IoCManager.Resolve<IComponentManager>();
            // TODO: Show a tooltip box above each AI's head
        }
    }
}
