using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBallisticMagazineComponent))]
    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    [ComponentReference(typeof(SharedBallisticsAmmoProvider))]
    internal sealed class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public override int AmmoCount => _ammoCount;

        private int _ammoCount;

        public override void Initialize()
        {
            base.Initialize();
            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            if (FillPrototype != null)
            {
                _ammoCount = AmmoMax - 1;
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not BallisticMagazineComponentState state || nextState != null) return;

            if (_ammoCount != state.AmmoCount)
            {
                _ammoCount = state.AmmoCount;
                Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new AmmoUpdateEvent());
            }

            AmmoMax = state.AmmoMax;

            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            Dirty();
        }

        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            throw new System.NotImplementedException();
        }

        public override bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            throw new System.NotImplementedException();
        }
    }

    public sealed class AmmoUpdateEvent : EntityEventArgs {}
}
