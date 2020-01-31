using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Combat
{
    public class AttackTarget : StateData<IEntity>
    {
        public override string Name => "AttackTarget";

        // This is only used for planning and isn't actively retrieved
        public override IEntity GetValue()
        {
            return null;
        }
    }
}
