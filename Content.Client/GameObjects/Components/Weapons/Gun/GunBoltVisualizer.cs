using Robust.Client.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    public sealed class GunBoltVisualizer : AppearanceVisualizer
    {
        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);
            if (!component.Owner.TryGetComponent(out ISpriteComponent? spriteComponent)) return;
            // TODO: Visuals here default to closed if (component.TryGetData())
        }
    }
}
