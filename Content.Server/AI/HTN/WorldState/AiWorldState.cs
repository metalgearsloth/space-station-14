using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Content.Server.AI.HTN.WorldState
{
    public sealed class AiWorldState
    {
        // Cache the known types
        private static List<Type> _aiStates;
        private static List<Type> _aiEnumerableStates;

        private List<IAiState> _states = new List<IAiState>();
        private List<IAiEnumerableState> _enumerableStates = new List<IAiEnumerableState>();

        public AiWorldState(IEntity owner)
        {
            Setup(owner);
        }

        private void GetStates()
        {
            _aiStates = new List<Type>();
            _aiEnumerableStates = new List<Type>();
            var reflectionManager = IoCManager.Resolve<IReflectionManager>();

            var states = reflectionManager.GetAllChildren(typeof(IAiState));

            foreach (var state in states)
            {
                _aiStates.Add(state);
            }

            var enumerableStates = reflectionManager.GetAllChildren(typeof(IAiEnumerableState));

            foreach (var state in enumerableStates)
            {
                _aiEnumerableStates.Add(state);
            }
        }

        private void Setup(IEntity owner)
        {
            if (_aiStates == null || _enumerableStates == null)
            {
                GetStates();
            }

            DebugTools.AssertNotNull(_aiStates);
            var typeFactory = IoCManager.Resolve<IDynamicTypeFactory>();

            foreach (var state in _aiStates)
            {
                var newState = (IAiState) typeFactory.CreateInstance(state);
                newState.Setup(owner);
                _states.Add(newState);
            }

            foreach (var state in _aiEnumerableStates)
            {
                var newState = (IAiEnumerableState) typeFactory.CreateInstance(state);
                newState.Setup(owner);
                _enumerableStates.Add(newState);
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
