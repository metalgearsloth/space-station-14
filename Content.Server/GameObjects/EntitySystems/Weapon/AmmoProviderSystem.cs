using System;
using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Weapon.Gun;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.Weapon
{
    internal sealed class AmmoProviderSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _protoManager = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;

        private const float EjectVariation = 0.01f;
        private const float EjectVolume = -0.1f;

        private const float InsertVariation = 0.01f;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BallisticMagazineComponent, UseInHandEvent>(HandleUseEntity);
            SubscribeLocalEvent<BallisticMagazineComponent, InteractUsingEvent>(HandleInteractUsing);
        }

        private void HandleInteractUsing(EntityUid uid, BallisticMagazineComponent component, InteractUsingEvent args)
        {
            // If it's not ammo don't spam em
            if (!args.Used.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return;
            }

            if (ammoComponent.Caliber != component.Caliber)
            {
                // Loc popup wrong caliber args.User.PopupMessage();
                return;
            }

            if (component.AmmoCount >= component.AmmoCapacity)
            {
                // TODO: Popup mag full
                return;
            }

            DebugTools.Assert(component.AmmoContainer.CanInsert(ammoComponent.Owner));
            component.AmmoContainer.Insert(ammoComponent.Owner);

            if (component.Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                component.UpdateAppearance(appearanceComponent);
            }

            if (!string.IsNullOrEmpty(ammoComponent.SoundInsert))
                SoundSystem.Play(Filter.Pvs(component.Owner), ammoComponent.SoundInsert, AudioHelpers.WithVariation(InsertVariation));

        }

        private void HandleUseEntity(EntityUid uid, BallisticMagazineComponent component, UseInHandEvent args)
        {
            if (!component.TryGetAmmo(out var ammo)) return;

            if (component.Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                component.UpdateAppearance(appearanceComponent);
            }

            args.Handled = true;

            if (args.User.TryGetComponent(out HandsComponent? handsComponent) &&
                ammo.Owner.TryGetComponent(out ItemComponent? itemComponent))
            {
                if (!handsComponent.PutInHand(itemComponent))
                {
                    EjectCartridge(ammo);
                }
            }
            else if (ammo.Owner.TryGetContainer(out var container))
            {
                container.Insert(ammo.Owner);
            }
            else
            {
                ammo.Owner.Transform.WorldPosition = args.User.Transform.WorldPosition;
            }
        }

        private void EjectCartridge(SharedAmmoComponent ammoComponent, IEntity? user = null)
        {
            var transform = ammoComponent.Owner.Transform;

            if (user != null)
                transform.Coordinates = user.Transform.Coordinates;

            transform.LocalRotation = _robustRandom.NextFloat() * MathF.Tau;

            if (!string.IsNullOrEmpty(ammoComponent.SoundCollectionEject))
            {
                if (!_protoManager.TryIndex<SoundCollectionPrototype>(ammoComponent.SoundCollectionEject, out var sounds))
                {
                    Logger.ErrorS("gun", $"Tried to play sound collection {ammoComponent.SoundCollectionEject} which doesn't exist!");
                    return;
                }

                SoundSystem.Play(
                    Filter.Pvs(transform.Owner),
                    _robustRandom.Pick(sounds.PickFiles),
                    AudioHelpers
                        .WithVariation(EjectVariation)
                        .WithVolume(EjectVolume));
            }
        }
    }
}
