using Content.Shared.GameObjects.Components.Projectiles;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Projectiles
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedProjectileComponent))]
    public class ProjectileComponent : SharedProjectileComponent
    {
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (curState is not ProjectileComponentState compState) return;
            Shooter = compState.Shooter;
            IgnoreShooter = compState.IgnoreShooter;
            Dirty();
        }
    }
}
