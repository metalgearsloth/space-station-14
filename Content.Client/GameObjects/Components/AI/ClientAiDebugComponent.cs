using System.Collections.Generic;
using Content.Shared.GameObjects.Components.AI;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Client.Interfaces.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.GameObjects.Components.AI
{
    [RegisterComponent]
    public sealed class ClientAiDebugComponent : SharedAiDebugComponent
    {
        // Ideally you'd persist the label until the entity dies / is deleted but for first-draft this is fine
        private float _labelDuration = 10.0f;
        private AiDebugOverlay _overlay;

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);
            switch (message)
            {
                case AiPlanMessage msg:
                    _overlay.UpdatePlan(msg);
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
            overlaymanager.RemoveOverlay(nameof(_overlay));
        }
    }

    public sealed class AiDebugOverlay : Overlay
    {

        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        private Dictionary<IEntity, PanelContainer> _aiBoxes = new Dictionary<IEntity, PanelContainer>();

        public AiDebugOverlay() : base(nameof(AiDebugOverlay))
        {
            Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance();
        }

        public void UpdatePlan(AiPlanMessage message)
        {
            var entity = IoCManager.Resolve<IEntityManager>().GetEntity(message.EntityUid);
            if (!_aiBoxes.ContainsKey(entity))
            {
                var userInterfaceManager = IoCManager.Resolve<IUserInterfaceManager>();
                var newLabel = new Label
                {
                    MouseFilter = Control.MouseFilterMode.Ignore,
                };

                var panel = new PanelContainer
                {
                    StyleClasses = { "tooltipBox" },
                    Children = { newLabel },
                    MouseFilter = Control.MouseFilterMode.Ignore,
                    ModulateSelfOverride = Color.White.WithAlpha(0.75f),
                };


                userInterfaceManager.StateRoot.AddChild(panel);

                _aiBoxes[entity] = panel;
            }

            // Probably shouldn't access by index but it's a debugging tool so eh
            var label = (Label) _aiBoxes[entity].GetChild(0);
            var time = message.PlanningTime * 1000;
            label.Text = $"Root Task: {message.RootTask}\nPlanning time (ms): {time:0.0000}\nPlan: ";
            foreach (var task in message.PrimitiveTaskNames)
            {
                label.Text += $"\n- {task}";
            }
        }

        protected override void Draw(DrawingHandleBase handle)
        {
            var eyeManager = IoCManager.Resolve<IEyeManager>();
            foreach (var (entity, panel) in _aiBoxes)
            {
                if (!eyeManager.GetWorldViewport().Contains(entity.Transform.WorldPosition))
                {
                    panel.Visible = false;
                    continue;
                }

                var screenPosition = eyeManager.WorldToScreen(entity.Transform.GridPosition).Position;
                var offsetPosition = new Vector2(screenPosition.X - panel.Width / 2, screenPosition.Y - panel.Height - 50f);
                panel.Visible = true;

                LayoutContainer.SetPosition(panel, offsetPosition);
            }
        }
    }
}
