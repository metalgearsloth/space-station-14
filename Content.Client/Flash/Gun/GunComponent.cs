using Content.Shared.Flash.Guns;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedGunComponent))]
    internal sealed class GunComponent : SharedGunComponent
    {
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not GunComponentState state) return;
            NextFire = state.NextFire;
            Dirty();
        }
    }
}
