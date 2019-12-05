using System.Collections.Generic;
using System.Linq;
using Content.Server.AI.Actions;
using Robust.Shared.Interfaces.GameObjects;

// This planner was derived from https://github.com/sploreg/goap/tree/d1cea0728fb4733266affea8049da1e373d618f7
/*
 The MIT License (MIT)

Copyright (c) 2015 Brent Anthony Owens

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

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
        /// <param name="entity"></param>
        /// <param name="availableActions"></param>
        /// <param name="worldState"></param>
        /// <param name="goal"></param>
        /// <returns>null if no solution found, otherwise a queue of actions to do</returns>
        public Queue<GoapAction> Plan(IEntity entity, HashSet<GoapAction> availableActions, IDictionary<string, bool> worldState,
            IDictionary<string, bool> goal)
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

            var start = new GoapNode(null, 0, worldState, null);
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
        private bool BuildGraph(GoapNode parent, List<GoapNode> leaves, HashSet<GoapAction> usableActions, IDictionary<string, bool> goal)
        {
            foreach (var action in usableActions)
            {
                if (!InState(action.PreConditions, parent.State))
                {
                    continue;
                }

                var currentState = PopulateState(parent.State, action.Effects);
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

        // TODO: Built-in way of this + PopulateState?
        /// <summary>
        /// Checks if all items in the test state are in the target state
        /// </summary>
        /// <param name="test"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        private bool InState(IDictionary<string, bool> test, IDictionary<string, bool> state)
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
        private IDictionary<string, bool> PopulateState(IDictionary<string, bool> currentState,
            IDictionary<string, bool> stateChange)
        {

            var state = new Dictionary<string, bool>();
            // Copy the KVPs over as new objects
            foreach (var s in currentState)
            {
                state.Add(s.Key, s.Value);
            }

            foreach (var change in stateChange)
            {
                if (state.ContainsKey(change.Key))
                {
                    state[change.Key] = change.Value;
                }
                else
                {
                    state.Add(change.Key, change.Value);
                }
            }

            return state;
        }
    }

    internal class GoapNode
    {
        public GoapNode Parent;
        public float RunningCost;
        public IDictionary<string, bool> State;
        public GoapAction Action;

        public GoapNode(GoapNode parent, float runningCost, IDictionary<string, bool> state,
            GoapAction action)
        {
            Parent = parent;
            RunningCost = runningCost;
            State = state;
            Action = action;
        }
    }
}
