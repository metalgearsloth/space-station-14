using Content.Shared.GameObjects.Components.Weapons.Ranged;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    public class ClientRangedBarrelComponent : SharedRangedBarrelComponent
    {
        public override string Name { get; }
        public override FireRateSelector FireRateSelector { get; }
        public override FireRateSelector AllRateSelectors { get; }
        public override float FireRate { get; }
        public override int ShotsLeft { get; }
        public override int Capacity { get; }
    }
}