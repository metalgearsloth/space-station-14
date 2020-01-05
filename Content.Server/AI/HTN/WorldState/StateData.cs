using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState
{
    public interface IAiState
    {
        object Value { get; set; }
        void Setup(IEntity owner);
    }

    public interface IAiEnumerableState
    {
        IEnumerable<object> Value { get; set; }
        void Setup(IEntity owner);
    }

    public interface IRRetarded
    {
        Type NotAType { get; }
    }

    public abstract class StateData<T> : IAiState
    {
        public abstract string Name { get; }
        protected IEntity Owner { get; private set; }

        public object Value
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

        private object _value;

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

        public IEnumerable<object> Value
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
            set => _value = value;
        }

        private IEnumerable<object> _value;

        protected void Reset()
        {
            Value = default;
        }

        public abstract IEnumerable<T> GetValue();
    }
}
