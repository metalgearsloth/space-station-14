using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.WorldState
{
    public sealed class AiWorldState
    {
        private List<dynamic> _enumerableStates = new List<dynamic>();
        private List<dynamic> _states = new List<dynamic>();

        public AiWorldState(IEntity owner)
        {
            Setup(owner);
        }

        private void Setup(IEntity owner)
        {
            var typeFactory = IoCManager.Resolve<IDynamicTypeFactory>();
            var states = IoCManager.Resolve<IReflectionManager>().FindTypesWithAttribute<AiStateAttribute>();

            foreach (var state in states)
            {
                _states.Add(typeFactory.CreateInstance(state));
            }

            var enumerableStates = IoCManager.Resolve<IReflectionManager>().FindTypesWithAttribute<AiEnumerableStateAttribute>();

            foreach (var state in enumerableStates)
            {
                _enumerableStates.Add(typeFactory.CreateInstance(state));
            }

        }

        public U GetStateValue<T, U>() where T : StateData<U>
        {
            foreach (var knownState in _states)
            {
                if (knownState.GetType() == typeof(T))
                {
                    return (U) knownState.Value;
                }
            }

            throw new InvalidOperationException();
        }

        public IEnumerable<U> GetEnumerableStateValue<T, U>() where T : EnumerableStateData<U>
        {
            foreach (var knownState in _enumerableStates)
            {
                if (knownState.GetType() == typeof(T))
                {
                    foreach (var result in knownState.Value)
                    {
                        yield return (U) result;
                    }

                    yield break;
                }
            }

            throw new InvalidOperationException();
        }
    }
}
