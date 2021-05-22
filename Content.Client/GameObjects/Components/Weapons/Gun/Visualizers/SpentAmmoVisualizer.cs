using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Client.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Content.Client.GameObjects.Components.Weapons.Gun.Visualizers
{
    public sealed class SpentAmmoVisualizer : AppearanceVisualizer
    {
        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);
            if (!component.Owner.TryGetComponent(out ISpriteComponent? spriteComponent)) return;
            if (!component.TryGetData(GunVisuals.AmmoSpent, out bool spent))
            {
                return;
            }

            spriteComponent.LayerSetState(AmmoVisualLayers.Base, spent ? "spent" : "base");
        }

        private enum AmmoVisualLayers
        {
            Base = 0,
        }
    }
}
