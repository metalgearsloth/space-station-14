using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState
{
    public interface IAiState
    {
        Type FuckGenerics { get; }
        object Value { get; set; }
        void Setup(IEntity owner);
    }

    public interface IAiEnumerableState
    {
    }

    public interface IRRetarded
    {
        Type NotAType { get; }
    }

    public abstract class StateData<T>
    {
        public abstract string Name { get; }
        protected IEntity Owner { get; private set; }

        public T Value
        {
            get
            {
                if (_value == null)
                {
                    _value = GetValue();
                }

                return _value;
            }
            set => _value = value;
        }

        private T _value;

        public void Setup(IEntity owner)
        {
            Owner = owner;
        }

        protected void Reset()
        {
            Value = default;
        }

        public abstract T GetValue();
    }

    public abstract class EnumerableStateData<T> : IAiEnumerableState
    {
        public abstract string Name { get; }
        protected IEntity Owner { get; private set; }

        public virtual void Setup(IEntity owner)
        {
            Owner = owner;
        }

        public IEnumerable<T> Value
        {
            get
            {
                if (_value == null)
                {
                    foreach (var item in GetValue())
                    {
                        yield return item;
                    }
                    yield break;
                }

                foreach (var item in _value)
                {
                    yield return item;
                }
            }
            protected set => _value = value;
        }

        private IEnumerable<T> _value;

        protected void Reset()
        {
            Value = default;
        }

        public abstract IEnumerable<T> GetValue();
    }
}
