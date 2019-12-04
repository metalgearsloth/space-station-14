using System;
using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.AI.Preconditions;
using Content.Shared.GameObjects.Components.Power;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI
{
    /// <summary>
    /// This will build an action plan from the available actions and states to accomplish a goal
    /// </summary>
    public class GoalPlanner
    {
        // You shouldn't need to edit this to write an AI.

        /// <summary>
        /// Works through the available actions in a goal and comes up with a plan to complete it
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="availableActions"></param>
        /// <param name="worldState"></param>
        /// <param name="goal"></param>
        /// <returns>null if no solution found, otherwise a queue of actions to do</returns>
        public Queue<GoapAction> Plan(GoapAgent agent, HashSet<GoapAction> availableActions, HashSet<KeyValuePair<AiState, bool>> worldState,
            HashSet<KeyValuePair<AiState, bool>> goal)
        {
            foreach (var action in availableActions)
            {
                action.Reset();
            }

            var usableActions = new HashSet<GoapAction>();
            foreach (var action in availableActions)
            {
                if (action.CheckProceduralPreconditions(agent))
                {
                    usableActions.Add(action);
                }
            }

            var leaves = new List<GoapNode>();

            GoapNode start = new GoapNode(null, 0, worldState, null);
            bool success = BuildGraph(start, leaves, usableActions, goal);

            if (!success)
            {
                // No plan
                return null;
            }

            // Get cheapest left
            GoapNode cheapest = null;
            foreach (var leaf in leaves)
            {
                if (cheapest == null)
                {
                    cheapest = leaf;
                }
                else if(leaf.RunningCost < cheapest.RunningCost)
                {
                    cheapest = leaf;
                }
            }

            var result = new List<GoapAction>();
            GoapNode n = cheapest;
            while (n != null)
            {
                if (n.Action != null)
                {
                    result.Insert(0, n.Action);
                }

                n = n.Parent;
            }

            var queue = new Queue<GoapAction>();
            foreach (var action in result)
            {
                queue.Enqueue(action);
            }

            // Plan found
            return queue;

        }

        /// <summary>
        /// Returns true if solution to goal found
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="leaves"></param>
        /// <param name="usableActions"></param>
        /// <param name="goal"></param>
        /// <returns></returns>
        private bool BuildGraph(GoapNode parent, List<GoapNode> leaves, HashSet<GoapAction> usableActions, HashSet<KeyValuePair<AiState, bool>> goal)
        {

            foreach (var action in usableActions)
            {
                if (!InState(action.PreConditions, parent.State))
                {
                    continue;
                }

                HashSet<KeyValuePair<AiState, bool>> currentState = PopulateState(parent.State, action.Effects);
                var node = new GoapNode(parent, parent.RunningCost + action.Cost(), currentState, action);

                if (InState(goal, currentState))
                {
                    // Solution found
                    leaves.Add(node);
                    return true;
                }

                HashSet<GoapAction> subset = ActionSubset(usableActions, action);
                var found = BuildGraph(node, leaves, subset, goal);
                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a subset of actions excluding the specified action
        /// </summary>
        /// <param name="actions"></param>
        /// <param name="remove"></param>
        /// <returns></returns>
        private HashSet<GoapAction> ActionSubset(HashSet<GoapAction> actions, GoapAction remove)
        {
            HashSet<GoapAction> subset = new HashSet<GoapAction>();

            foreach (var action in actions)
            {
                if (action.Equals(remove))
                {
                    subset.Add(action);
                }
            }

            return subset;
        }

        /// <summary>
        /// Checks if all items in the test state are in the target state
        /// </summary>
        /// <param name="test"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        private bool InState(HashSet<KeyValuePair<AiState, bool>> test, HashSet<KeyValuePair<AiState, bool>> state)
        {
            foreach (var t in test)
            {
                bool match = false;

                foreach (var s in state)
                {
                    if (s.Equals(t))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the current state again with the given state changes
        /// </summary>
        /// <param name="currentState"></param>
        /// <param name="stateChange"></param>
        /// <returns></returns>
        private HashSet<KeyValuePair<AiState, bool>> PopulateState(HashSet<KeyValuePair<AiState, bool>> currentState,
            HashSet<KeyValuePair<AiState, bool>> stateChange)
        {
            HashSet<KeyValuePair<AiState, bool>> state = new HashSet<KeyValuePair<AiState, bool>>();
            // Copy the KVPs over as new objects
            foreach (var s in currentState)
            {
                state.Add(new KeyValuePair<AiState, bool>(s.Key, s.Value));
            }

            foreach (var change in stateChange)
            {
                bool exists = false;

                foreach (var s in state)
                {
                    if (s.Equals(change))
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    state.RemoveWhere(kvp => { return kvp.Key.Equals(change.Key);});
                    var updated = new KeyValuePair<AiState, bool>(change.Key, change.Value);
                    state.Add(updated);
                }
                else
                {
                    state.Add(new KeyValuePair<AiState, bool>(change.Key, change.Value));
                }
            }

            return state;
        }

        private void MoveTo(GridCoordinates gridCoordinates)
        {
            // Mover move
            return;
        }
    }

    internal class GoapNode
    {
        public GoapNode Parent;
        public float RunningCost;
        public HashSet<KeyValuePair<AiState, bool>> State;
        public GoapAction Action;

        public GoapNode(GoapNode parent, float runningCost, HashSet<KeyValuePair<AiState, bool>> state,
            GoapAction action)
        {
            Parent = parent;
            RunningCost = runningCost;
            State = state;
            Action = action;
        }
    }

    public enum GoapState
    {
        Idle,
        MoveTo,
        PerformAction,
    }
}
