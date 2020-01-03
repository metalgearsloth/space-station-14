using System.Collections.Generic;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Hands
{
    public class HandItems : IStateData
    {
        public string Name => "HandItems";
        public IEnumerable<IEntity> Value { get; set; }
        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public IEnumerable<IEntity> GetValue()
        {
            if (!_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                yield break;
            }

            foreach (var item in handsComponent.GetAllHeldItems())
            {
                yield return item.Owner;
            }
        }
    }
}
