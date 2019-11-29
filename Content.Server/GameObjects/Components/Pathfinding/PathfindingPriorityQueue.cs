using System;
using System.Collections.Generic;

namespace Content.Server.Pathfinding
{
    public interface IPathfindingPriorityQueue<T>
    {
        int Count { get; }
        void Enqueue(T item, double priority);
        T Dequeue();
        void Clear();
    }

    public class PathfindingPriorityQueue<T> : IPathfindingPriorityQueue<T>
    {
        private readonly List<Tuple<T, double>> _elements = new List<Tuple<T, double>>();

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

        public void Enqueue(T item, double priority)
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
