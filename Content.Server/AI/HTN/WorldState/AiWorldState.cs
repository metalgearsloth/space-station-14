using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.WorldState
{
    public sealed class AiWorldState
    {
        public event Action<IStateData> StateUpdate;
        public IReadOnlyCollection<IStateData> States => _states;
        private List<IStateData> _states = new List<IStateData>();
        private HashSet<Type> _registeredStates = new HashSet<Type>();

        private IEntity _owner;

        public AiWorldState(IEntity owner)
        {
            Setup(owner);
        }

        public AiWorldState(IEntity owner, AiWorldState worldState)
        {
            foreach (var state in States)
            {
                _states.Add(state);
            }
            Setup(owner);
        }

        private void Setup(IEntity owner)
        {
            _owner = owner;
            var typeFactory = IoCManager.Resolve<IDynamicTypeFactory>();
            var states = IoCManager.Resolve<IReflectionManager>().GetAllChildren<IStateData>();
            foreach (var state in states)
            {
                _registeredStates.Add(state);
                _states.Add((IStateData)typeFactory.CreateInstance(state));
            }

            foreach (var state in _states)
            {
                state.Setup(_owner);
            }
            // TODO: Need to add a setup here
        }

        //  TODO: Have each update specify how frequently to update
        private void Update(float frameTime)
        {
            // TODO: Look at having the states as classes that can mark themselves as dirty etc.
            foreach (var state in _states)
            {
            }
        }

        public T GetState<T>() where T : IStateData
        {
            foreach (var knownState in _states)
            {
                if (knownState.GetType() == typeof(T))
                {
                    return (T) knownState;
                }
            }

            return default;
        }

        public void UpdateState(IStateData stateUpdate)
        {
            bool found = false;
            int idx = 0;
            foreach (var state in _states)
            {
                if (state.GetType() == stateUpdate.GetType())
                {
                    found = true;
                    break;
                }
                idx++;
            }

            if (found)
            {
                if (_states[idx] == stateUpdate)
                {
                    return;
                }
                _states[idx] = stateUpdate;
                StateUpdate?.Invoke(stateUpdate);
            }
            _states.Add(stateUpdate);
            StateUpdate?.Invoke(stateUpdate);
        }

        public static void UpdateState(IEntity entity, IStateData state)
        {
            if (!entity.TryGetComponent(out AiControllerComponent controller))
            {
                return;
            }
            controller.Processor.State.UpdateState(state);
        }
    }
}
