using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    public sealed class RevolverBarrelComponent : ClientRangedBarrelComponent
    {
        public override void Initialize()
        {
            base.Initialize();
            Owner.GetComponent<ClientRangedWeaponComponent>().WeaponCanFireHandler += WeaponCanFire;
            Owner.GetComponent<ClientRangedWeaponComponent>().UserCanFireHandler += UserCanFire;
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            Owner.GetComponent<ClientRangedWeaponComponent>().WeaponCanFireHandler -= WeaponCanFire;
            Owner.GetComponent<ClientRangedWeaponComponent>().UserCanFireHandler -= UserCanFire;
        }

        private bool WeaponCanFire()
        {
            return false;
        }

        private bool UserCanFire(IEntity entity)
        {
            return false;
        }
    }
}