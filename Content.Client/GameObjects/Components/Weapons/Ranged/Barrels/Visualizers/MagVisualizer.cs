using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.Utility;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels.Visualizers
{
    [UsedImplicitly]
    public sealed class MagVisualizer : AppearanceVisualizer
    {
        private bool _magLoaded;
        [DataField("magState")]
        private string? _magState;
        [DataField("steps")]
        private int _magSteps;
        [DataField("zeroVisible")]
        private bool _zeroVisible;

        public override void InitializeEntity(IEntity entity)
        {
            base.InitializeEntity(entity);
            var sprite = entity.GetComponent<ISpriteComponent>();

            if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.Mag, out _))
            {
                sprite.LayerSetState(RangedBarrelVisualLayers.Mag, $"{_magState}-{_magSteps-1}");
                sprite.LayerSetVisible(RangedBarrelVisualLayers.Mag, false);
            }

            if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.MagUnshaded, out _))
            {
                sprite.LayerSetState(RangedBarrelVisualLayers.MagUnshaded, $"{_magState}-unshaded-{_magSteps-1}");
                sprite.LayerSetVisible(RangedBarrelVisualLayers.MagUnshaded, false);
            }

            entity.GetComponent<AppearanceComponent>().SetData(MagazineBarrelVisuals.MagLoaded, true);
        }

        public override void OnChangeData(AppearanceComponent component)
        {
            base.OnChangeData(component);

            // tl;dr
            // 1.If no mag then hide it OR
            // 2. If step 0 isn't visible then hide it (mag or unshaded)
            // 3. Otherwise just do mag / unshaded as is
            var sprite = component.Owner.GetComponent<ISpriteComponent>();

            component.TryGetData(MagazineBarrelVisuals.MagLoaded, out _magLoaded);

            if (_magLoaded)
            {
                if (!component.TryGetData(AmmoVisuals.AmmoMax, out int capacity))
                {
                    return;
                }
                if (!component.TryGetData(AmmoVisuals.AmmoCount, out int current))
                {
                    return;
                }

                var step = ContentHelpers.RoundToLevels(current, capacity, _magSteps);

                if (step == 0 && !_zeroVisible)
                {
                    if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.Mag, out _))
                    {
                        sprite.LayerSetVisible(RangedBarrelVisualLayers.Mag, false);
                    }

                    if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.MagUnshaded, out _))
                    {
                        sprite.LayerSetVisible(RangedBarrelVisualLayers.MagUnshaded, false);
                    }

                    return;
                }

                if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.Mag, out _))
                {
                    sprite.LayerSetVisible(RangedBarrelVisualLayers.Mag, true);
                    sprite.LayerSetState(RangedBarrelVisualLayers.Mag, $"{_magState}-{step}");
                }

                if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.MagUnshaded, out _))
                {
                    sprite.LayerSetVisible(RangedBarrelVisualLayers.MagUnshaded, true);
                    sprite.LayerSetState(RangedBarrelVisualLayers.MagUnshaded, $"{_magState}-unshaded-{step}");
                }
            }
            else
            {
                if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.Mag, out _))
                {
                    sprite.LayerSetVisible(RangedBarrelVisualLayers.Mag, false);
                }

                if (sprite.LayerMapTryGet(RangedBarrelVisualLayers.MagUnshaded, out _))
                {
                    sprite.LayerSetVisible(RangedBarrelVisualLayers.MagUnshaded, false);
                }
            }
        }
    }
}
