using System;
using Content.Client.GameObjects.Components.Weapons.Gun;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Client.Animations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Animations;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.EntitySystems
{
    internal sealed class AmmoCounterSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BallisticAmmoCounterComponent, AmmoUpdateEvent>(HandleAmmoUpdate);
        }

        private void HandleAmmoUpdate(EntityUid uid, BallisticAmmoCounterComponent component, AmmoUpdateEvent args)
        {
            var player = _playerManager.LocalPlayer?.ControlledEntity;

            if (player == null ||
                !component.Owner.TryGetContainerMan(out var conMan) ||
                conMan.Owner != player) return;

            component.Chambered = args.Chambered;
            if (args.MagazineAmmo == null)
            {
                component.MagazineCount = null;
            }
            else
            {
                component.MagazineCount = (args.MagazineAmmo.Value, args.MagazineMax);
            }

            component.UpdateControl();
        }

        internal static readonly Animation AlarmAnimationSmg = new()
        {
            Length = TimeSpan.FromSeconds(1.4),
            AnimationTracks =
            {
                new AnimationTrackControlProperty
                {
                    // These timings match the SMG audio file.
                    Property = nameof(Label.FontColorOverride),
                    InterpolationMode = AnimationInterpolationMode.Previous,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.1f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.3f),
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.2f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.3f),
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.2f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.3f),
                    }
                }
            }
        };

        internal static readonly Animation AlarmAnimationLmg = new()
        {
            Length = TimeSpan.FromSeconds(0.75),
            AnimationTracks =
            {
                new AnimationTrackControlProperty
                {
                    // These timings match the SMG audio file.
                    Property = nameof(Label.FontColorOverride),
                    InterpolationMode = AnimationInterpolationMode.Previous,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.0f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.15f),
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.15f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.15f),
                        new AnimationTrackProperty.KeyFrame(Color.Red, 0.15f),
                        new AnimationTrackProperty.KeyFrame(null!, 0.15f),
                    }
                }
            }
        };
    }
}
