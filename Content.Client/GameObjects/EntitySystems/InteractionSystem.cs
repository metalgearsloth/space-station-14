#nullable enable
using Content.Shared.GameObjects.EntitySystemMessages;
using Content.Shared.GameObjects.EntitySystems;
using JetBrains.Annotations;

namespace Content.Client.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal class InteractionSystem : SharedInteractionSystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DragDropMessage>(HandleDragDropMessage);
        }
    }
}
