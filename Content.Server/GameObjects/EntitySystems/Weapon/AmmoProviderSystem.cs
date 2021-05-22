using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Weapon.Gun;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.EntitySystems.Weapon
{
    internal sealed class AmmoProviderSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BallisticMagazineComponent, UseInHandMessage>(HandleUseEntity);
        }

        private void HandleUseEntity(EntityUid uid, BallisticMagazineComponent component, UseInHandMessage args)
        {
            if (!component.TryGetAmmo(out var ammo)) return;

            args.Handled = true;

            if (args.User.TryGetComponent(out HandsComponent? handsComponent) &&
                ammo.Owner.TryGetComponent(out ItemComponent? itemComponent))
            {
                handsComponent.PutInHandOrDrop(itemComponent);
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
    }
}
