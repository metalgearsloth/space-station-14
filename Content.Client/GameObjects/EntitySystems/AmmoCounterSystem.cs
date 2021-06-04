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
            SubscribeLocalEvent<AmmoCounterComponent, AmmoUpdateEvent>(HandleAmmoUpdate);
            SubscribeLocalEvent<AmmoCounterComponent, EquippedEvent>(HandleEquipped);
            SubscribeLocalEvent<AmmoCounterComponent, UnequippedEvent>(HandleUnequipped);
            // TODO: Equip and un-equip
        }

        private void HandleUnequipped(EntityUid uid, AmmoCounterComponent component, UnequippedEvent args)
        {
            return;
        }

        private void HandleEquipped(EntityUid uid, AmmoCounterComponent component, EquippedEvent args)
        {
            return;
        }

        private void HandleAmmoUpdate(EntityUid uid, AmmoCounterComponent component, AmmoUpdateEvent args)
        {
            var player = _playerManager.LocalPlayer?.ControlledEntity;

            if (player == null ||
                !component.Owner.TryGetContainerMan(out var conMan) ||
                conMan.Owner != player) return;

            // TODO: UI Shiznit
        }

        private readonly Animation AlarmAnimationSmg = new()
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

        private readonly Animation AlarmAnimationLmg = new()
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
