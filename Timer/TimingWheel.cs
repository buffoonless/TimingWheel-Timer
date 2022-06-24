using System.Runtime.CompilerServices;
using Timer.Colletcions;

namespace Timer
{
    internal class TimingWheel
    {
        //Use bit operation to speed up the insertion time
        const int TW_BYTES = 6;
        //Each time wheel has 64 slots
        const int TW_SIZE =  1 << TW_BYTES;

        /// <summary>
        /// Maximum time interval of the current time wheel
        /// </summary>
        readonly long interval;
        /// <summary>
        /// Time interval between slots
        /// </summary>
        readonly long tickMs;
        /// <summary>
        /// Time wheel level (Larger means this wheel in the more outer layer)
        /// </summary>
        readonly int wheelLevel;
        readonly long mask;
        readonly int levelMask;
        readonly TimerTaskList[] buckets;

        long currentTime;

        volatile TimingWheel overflowWheel;

        internal DelayQueue<TimerTaskList> queue;
        readonly AtomicInt32 taskCounter;

        private TimingWheel(long tickMs,int wheelLevel, 
            long startMs, AtomicInt32 atomicInteger, DelayQueue<TimerTaskList> delayQueue)
        {
            this.tickMs = tickMs;
            this.wheelLevel = wheelLevel;
            taskCounter = atomicInteger;
            queue = delayQueue;
            interval = TW_SIZE << (wheelLevel * TW_BYTES);

            //Cache partial results of optimization bit operation
            levelMask = wheelLevel * TW_BYTES;
            mask = interval - 1;

            buckets = new TimerTaskList[TW_SIZE];
            for (int i = 1; i < TW_SIZE; i++)
            {
                buckets[i] = new TimerTaskList(atomicInteger);
            }
            currentTime = startMs - (startMs % tickMs);
        }

        internal TimingWheel(long startMs, AtomicInt32 atomicInteger, DelayQueue<TimerTaskList> delayQueue)
        {
            tickMs = 1;
            wheelLevel = 0;
            taskCounter = atomicInteger;
            queue = delayQueue;
            mask = TW_SIZE - 1;
            levelMask = 0;
            interval = TW_SIZE;
            buckets = new TimerTaskList[TW_SIZE];
            for (int i = 1; i < TW_SIZE; i++)
            {
                buckets[i] = new TimerTaskList(atomicInteger);
            }
            currentTime = startMs;
        }

        /// <summary>
        /// Add parent time wheel
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        void AddOverflowWheel()
        {
            if(overflowWheel == null)
            {
                overflowWheel = new TimingWheel(interval, wheelLevel + 1,
                    currentTime, taskCounter, queue);
            }
        }

        /// <summary>
        /// Add to the slot corresponding to the time wheel
        /// </summary>
        /// <param name="timerTaskEntry">Item to be added</param>
        /// <returns>True is returned after adding successfully. False is returned after expiration or cancellation</returns>
        internal bool Add(TimerTaskEntry timerTaskEntry)
        {
            var delayTime = timerTaskEntry.expirationMs - currentTime;
            if (timerTaskEntry.Cancelled)
            {
                return false;
            }
            else if (delayTime < tickMs)
            {
                return false;
            }
            else if (delayTime < interval)
            {
                var virtualId = (delayTime >> levelMask) & mask;
                var bucket = buckets[virtualId];

                bucket.Add(timerTaskEntry);

                if (bucket.SetExpiration(virtualId * tickMs + currentTime))
                {
                    queue.Offer(bucket);
                }
            }
            else
            {
                AddToOverflowWheel(timerTaskEntry);
            }
            return true;
        }

        /// <summary>
        /// Advance time wheel
        /// </summary>
        /// <param name="timeMs">Current timestamp</param>
        internal void AdvanceClock(long timeMs)
        {
            if(timeMs >= currentTime + tickMs)
            {
                currentTime = timeMs - (timeMs % tickMs);
                if (overflowWheel != null)
                    overflowWheel.AdvanceClock(currentTime);
            }
        }

        /// <summary>
        /// Add to the slot corresponding to the time wheel
        /// Functions added internally to the parent time wheel do not need to judge whether they are expired
        /// </summary>
        /// <param name="timerTaskEntry"></param>
        private void AddInternal(TimerTaskEntry timerTaskEntry)
        {
            var delayTime = timerTaskEntry.expirationMs - currentTime;
            if (delayTime < interval)
            {
                var virtualId = (delayTime >> levelMask) & mask;
                var bucket = buckets[virtualId];

                bucket.Add(timerTaskEntry);

                if (bucket.SetExpiration(virtualId * tickMs + currentTime))
                {
                    queue.Offer(bucket);
                }
            }
            else
            {
                AddToOverflowWheel(timerTaskEntry);
            }
        }

        /// <summary>
        /// Add to the slot corresponding to the parent time wheel
        /// </summary>
        /// <param name="timerTaskEntry">Item to be added</param>
        private void AddToOverflowWheel(TimerTaskEntry timerTaskEntry)
        {
            if (overflowWheel == null)
                AddOverflowWheel();
            overflowWheel.AddInternal(timerTaskEntry);
        }
    }
}
