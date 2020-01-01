using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States
{
    public class MeleeWeaponInInventory : IStateData
    {
        public string Name => "MeleeWeaponInInventory";
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public bool GetValue()
        {
            // TODO: Implement
            return false;
        }
    }
}
