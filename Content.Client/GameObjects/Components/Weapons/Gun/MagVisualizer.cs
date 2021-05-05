using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    public sealed class MagVisualizer : AppearanceVisualizer
    {
        [DataField("magState")]
        private string? _magState;

        [DataField("steps")]
        private int _magSteps;

        [DataField("zeroVisible")]
        private bool _zeroVisible;

        public override void InitializeEntity(IEntity entity)
        {
            base.InitializeEntity(entity);
            if (!entity.TryGetComponent(out ISpriteComponent? sprite)) return;

            if (sprite.LayerMapTryGet(GunVisualLayers.Mag, out _))
            {
                sprite.LayerSetState(GunVisualLayers.Mag, $"{_magState}-{_magSteps-1}");
                sprite.LayerSetVisible(GunVisualLayers.Mag, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
            {
                sprite.LayerSetState(GunVisualLayers.MagUnshaded, $"{_magState}-unshaded-{_magSteps-1}");
                sprite.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
            }
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);

            // tl;dr
            // 1.If no mag then hide it OR
            // 2. If step 0 isn't visible then hide it (mag or unshaded)
            // 3. Otherwise just do mag / unshaded as is
            if (!component.Owner.TryGetComponent(out ISpriteComponent? spriteComponent)) return;

            if (!component.TryGetData(GunVisuals.MagLoaded, out bool magLoaded))
            {
                magLoaded = false;
            }

            if (magLoaded)
            {
                if (!component.TryGetData(GunVisuals.AmmoMax, out int capacity))
                {
                    return;
                }
                if (!component.TryGetData(GunVisuals.AmmoCount, out int current))
                {
                    return;
                }

                var step = ContentHelpers.RoundToLevels(current, capacity, _magSteps);

                if (step == 0 && !_zeroVisible)
                {
                    if (spriteComponent.LayerMapTryGet(GunVisualLayers.Mag, out _))
                    {
                        spriteComponent.LayerSetVisible(GunVisualLayers.Mag, false);
                    }

                    if (spriteComponent.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
                    {
                        spriteComponent.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
                    }

                    return;
                }

                if (spriteComponent.LayerMapTryGet(GunVisualLayers.Mag, out _))
                {
                    spriteComponent.LayerSetVisible(GunVisualLayers.Mag, true);
                    spriteComponent.LayerSetState(GunVisualLayers.Mag, $"{_magState}-{step}");
                }

                if (spriteComponent.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
                {
                    spriteComponent.LayerSetVisible(GunVisualLayers.MagUnshaded, true);
                    spriteComponent.LayerSetState(GunVisualLayers.MagUnshaded, $"{_magState}-unshaded-{step}");
                }
            }
            else
            {
                if (spriteComponent.LayerMapTryGet(GunVisualLayers.Mag, out _))
                {
                    spriteComponent.LayerSetVisible(GunVisualLayers.Mag, false);
                }

                if (spriteComponent.LayerMapTryGet(GunVisualLayers.MagUnshaded, out _))
                {
                    spriteComponent.LayerSetVisible(GunVisualLayers.MagUnshaded, false);
                }
            }
        }

        private enum GunVisualLayers : byte
        {
            Mag,
            MagUnshaded,
        }
    }
}
