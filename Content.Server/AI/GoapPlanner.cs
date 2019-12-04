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
        public Queue<GoapAction> Plan(IEntity entity, HashSet<GoapAction> availableActions, HashSet<KeyValuePair<string, bool>> worldState,
            HashSet<KeyValuePair<string, bool>> goal)
        {
            foreach (var action in availableActions)
            {
                action.Reset();
            }

            var usableActions = new HashSet<GoapAction>();
            foreach (var action in availableActions)
            {
                // If there's no procedural it should just be true
                if (action.CheckProceduralPreconditions(entity))
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
        private bool BuildGraph(GoapNode parent, List<GoapNode> leaves, HashSet<GoapAction> usableActions, HashSet<KeyValuePair<string, bool>> goal)
        {
            foreach (var action in usableActions)
            {
                if (!InState(action.PreConditions, parent.State))
                {
                    continue;
                }

                HashSet<KeyValuePair<string, bool>> currentState = PopulateState(parent.State, action.Effects);
                var node = new GoapNode(parent, parent.RunningCost + action.Cost(), currentState, action);

                if (InState(goal, currentState))
                {
                    // Solution found
                    leaves.Add(node);
                    return true;
                }

                var subset = new HashSet<GoapAction>(usableActions);
                subset.Remove(action);

                var found = BuildGraph(node, leaves, subset, goal);
                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        // TODO: Same with this one, there's simpler ways
        /// <summary>
        /// Checks if all items in the test state are in the target state
        /// </summary>
        /// <param name="test"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        private bool InState(HashSet<KeyValuePair<string, bool>> test, HashSet<KeyValuePair<string, bool>> state)
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

        // TODO wtf the original author do this, fix this
        /// <summary>
        /// Gets the current state again with the given state changes
        /// </summary>
        /// <param name="currentState"></param>
        /// <param name="stateChange"></param>
        /// <returns></returns>
        private HashSet<KeyValuePair<string, bool>> PopulateState(HashSet<KeyValuePair<string, bool>> currentState,
            HashSet<KeyValuePair<string, bool>> stateChange)
        {
            var state = new HashSet<KeyValuePair<string, bool>>();
            // Copy the KVPs over as new objects
            foreach (var s in currentState)
            {
                state.Add(new KeyValuePair<string, bool>(s.Key, s.Value));
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

                // If an existing, known state being updated or not
                if (exists)
                {
                    state.RemoveWhere(kvp => kvp.Key.Equals(change.Key));
                    var updated = new KeyValuePair<string, bool>(change.Key, change.Value);
                    state.Add(updated);
                }
                else
                {
                    state.Add(new KeyValuePair<string, bool>(change.Key, change.Value));
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
        public HashSet<KeyValuePair<string, bool>> State;
        public GoapAction Action;

        public GoapNode(GoapNode parent, float runningCost, HashSet<KeyValuePair<string, bool>> state,
            GoapAction action)
        {
            Parent = parent;
            RunningCost = runningCost;
            State = state;
            Action = action;
        }
    }
}
