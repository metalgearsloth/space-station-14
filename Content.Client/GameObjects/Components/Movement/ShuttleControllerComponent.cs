using Content.Shared.GameObjects.Components.Movement;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Movement
{
    [RegisterComponent]
    [ComponentReference(typeof(IMoverComponent))]
    [ComponentReference(typeof(SharedShuttleControllerComponent))]
    internal sealed class ShuttleControllerComponent : SharedShuttleControllerComponent, IMoverComponent
    {
    }
}
