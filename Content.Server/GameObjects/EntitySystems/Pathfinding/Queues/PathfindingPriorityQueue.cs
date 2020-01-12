using System;
using System.Collections.Generic;

namespace Content.Server.GameObjects.Components.Pathfinding.PathfindingQueue
{
    public interface IPathfindingPriorityQueue<T>
    {
        bool Contains(T key);
        int Count { get; }
        void Enqueue(T item, float priority);
        T Dequeue();
        void Clear();
    }

    public class PathfindingPriorityQueue<T> : IPathfindingPriorityQueue<T>
    {
        private readonly List<Tuple<T, float>> _elements = new List<Tuple<T, float>>();

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
            lock (_elements)
            {
                _elements.Add(Tuple.Create(item, priority));
            }
        }

        public T Dequeue()
        {
            lock (_elements)
            {
                int bestIndex = 0;

                for (int i = 0; i < _elements.Count; i++)
                {
                    if (_elements[i].Item2 < _elements[bestIndex].Item2)
                    {
                        bestIndex = i;
                    }
                }

                T bestItem = _elements[bestIndex].Item1;
                _elements.RemoveAt(bestIndex);
                return bestItem;
            }
        }

        public void Clear()
        {
            lock (_elements)
            {
                _elements.Clear();
            }
        }
    }
}
