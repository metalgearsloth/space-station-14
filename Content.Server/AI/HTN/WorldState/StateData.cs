using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState
{
    public interface IStateData<T>
    {
        T GetValue();
    }

    public abstract class StateData<T> : IStateData<T>
    {
        public abstract string Name { get; }
        public IEntity Owner { get; private set; }
        protected AiControllerComponent Controller;

        public void Setup(IEntity owner)
        {
            Owner = owner;
            if (!Owner.TryGetComponent(out AiControllerComponent controllerComponent))
            {
                throw new InvalidOperationException();
            }

            Controller = controllerComponent;
        }

        public abstract T GetValue();
    }

    public abstract class EnumerableStateData<T>
    {
        public abstract string Name { get; }
        public IEntity Owner { get; private set; }
        protected AiControllerComponent Controller;

        public void Setup(IEntity owner)
        {
            Owner = owner;
            if (!Owner.TryGetComponent(out AiControllerComponent controllerComponent))
            {
                throw new InvalidOperationException();
            }

            Controller = controllerComponent;
        }

        public abstract IEnumerable<T> GetValue();
    }
}
