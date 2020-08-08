using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class PortalSystem : EntitySystem
    {
        /// <summary>
        ///     Portal connections are 1-way which means you can have circular loops with multiple portals.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void Connect(PortalComponent from, PortalComponent to)
        {
            from.Destination = to.Owner;
        }
    }
}