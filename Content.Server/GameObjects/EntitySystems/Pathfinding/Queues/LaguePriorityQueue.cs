using System;
using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.PathfindingQueue
{
    // Grabbed from https://github.com/SebLague/Pathfinding/blob/master/Episode%2004%20-%20heap/Assets/Scripts/Heap.cs
    // TODO

    public class LaguePriorityQueue<T> : IPathfindingPriorityQueue<T>
    {
        private List<Tuple<T, float>> _elements;

        public LaguePriorityQueue()
        {
            _elements = new List<Tuple<T, float>>();
        }

        public LaguePriorityQueue(int maxSize)
        {
            _elements = new List<Tuple<T, float>>(maxSize);
        }

        public bool Contains(T key)
        {
            lock (_elements)
            {
                foreach (var element in _elements)
                {
                    if (element.Item1.Equals(key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int Count
        {
            get
            {
                lock (_elements)
                {
                    return _elements.Count;
                }
            }
        }

        public void Enqueue(T item, float priority)
        {
            throw new System.NotImplementedException();
        }

        public T Dequeue()
        {
            throw new System.NotImplementedException();
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }
    }
}
