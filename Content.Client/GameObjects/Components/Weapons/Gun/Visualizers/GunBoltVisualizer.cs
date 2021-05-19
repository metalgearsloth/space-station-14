using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    public sealed class GunBoltVisualizer : AppearanceVisualizer
    {
        public override void InitializeEntity(IEntity entity)
        {
            base.InitializeEntity(entity);
            if (!entity.TryGetComponent(out ISpriteComponent? spriteComponent)) return;

            spriteComponent.LayerSetState(GunVisuals.BoltClosed, "bolt-closed");
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);
            if (!component.Owner.TryGetComponent(out ISpriteComponent? spriteComponent)) return;
            if (!component.TryGetData(GunVisuals.BoltClosed, out bool boltClosed))
            {
                return;
            }

            spriteComponent.LayerSetState(GunVisuals.BoltClosed, boltClosed ? "bolt-closed" : "bolt-open");
        }
    }
}
