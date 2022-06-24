using System;
using System.Collections.Generic;

namespace Timer
{

    /// <summary>
    /// Delay queue item
    /// </summary>
    public interface IDelayItem
    {
        /// <summary>
        /// Get delay time
        /// </summary>
        /// <returns>time stamp</returns>
        long GetDelay();
    }

    /// <summary>
    /// Simple delay queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DelayQueue<T> where T : class, IDelayItem, IComparable<T>
    {
        private readonly object _lock = new object();

        private readonly PriorityQueue<T> priorityQueue;


        /// <summary>
        /// Current number of elements in the queue
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return priorityQueue.Count;
                }
            }
        }

        /// <summary>
        /// Whether the queue is empty
        /// </summary>
        public bool IsEmpty => Count == 0;

        public DelayQueue(int capacity = 0)
        {
            priorityQueue = new PriorityQueue<T>(capacity);
        }

        /// <summary>
        /// Inserts the specified element into this delay queue
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public void Offer(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            var delay = item.GetDelay();
            if(delay < 0)
                throw new ArgumentOutOfRangeException(nameof(item), "Delay time must be greater than or equal to 0");
            lock (_lock)
            {
                priorityQueue.Enqueue(item);
            }
        }

        /// <summary>
        /// Take the first item but do not remove it from the queue
        /// </summary>
        /// <returns>first item</returns>
        public T Peek()
        {
            lock (_lock)
            {
                return priorityQueue.Peek();
            }
        }

        /// <summary>
        /// Try take the first item but do not remove it from the queue
        /// </summary>
        /// <param name="item">first item</param>
        /// <returns>Success or not</returns>
        public bool TryPeek(out T item)
        {
            lock (_lock)
            {
                return priorityQueue.TryDequeue(out item);
            }
        }

        /// <summary>
        /// Get the first item in the timeout list according to the timestamp
        /// </summary>
        /// <param name="timeoutMs">timestamp</param>
        /// <returns>The time is not greater than the first of the timestamp</returns>
        public T Poll(in long timeoutMs)
        {
            lock(_lock)
            {
                if (priorityQueue.Count == 0)
                    return default;
                var first = priorityQueue.Peek();
                if (first.GetDelay() <= timeoutMs)
                    return priorityQueue.Dequeue();
                return default;
            }
        }

        /// <summary>
        /// Get the first item in the timeout list
        /// </summary>
        /// <returns>first item or default</returns>
        public T Poll()
        {
            lock (_lock)
            {
                if (priorityQueue.Count == 0)
                    return default;
                return priorityQueue.Dequeue();
            }
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                priorityQueue.Clear();
            }
        }
    }
}