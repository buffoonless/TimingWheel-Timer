using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Timer.Colletcions;

namespace Timer
{
    internal class TimerTaskList : IDelayItem, IComparable<TimerTaskList>
    {
        internal long expiration = -1L;
        //Use bidirectional linked list
        readonly TimerTaskEntry root = new TimerTaskEntry();
        readonly AtomicInt32 taskCounter;

        internal TimerTaskList(AtomicInt32 atomicInt)
        {
            root.Next = root;
            root.Prev = root;
            taskCounter = atomicInt;
        }

        internal bool SetExpiration(long expirationMs)
        {
            return Interlocked.Exchange(ref expiration, expirationMs) != expirationMs;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Foreach(Action<TimerTask> func)
        {
            var entry = root.Next;
            while(entry != root)
            {
                var next = entry.Next;
                if (!entry.Cancelled)
                    func(entry.timerTask);
                entry = next;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Add(TimerTaskEntry timerTaskEntry)
        {
            var done = false;
            while(!done)
            {
                timerTaskEntry.Remove();

                if(timerTaskEntry.List == null)
                {
                    var tail = root.Prev;
                    timerTaskEntry.Next = root;
                    timerTaskEntry.Prev = tail;
                    timerTaskEntry.List = this;
                    tail.Next = timerTaskEntry;
                    root.Prev = timerTaskEntry;
                    taskCounter.IncrementAndGet();
                    done = true;
                }
            }
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Remove(TimerTaskEntry timerTaskEntry)
        {
            if(timerTaskEntry.List == this)
            {
                timerTaskEntry.Next.Prev = timerTaskEntry.Prev;
                timerTaskEntry.Prev.Next = timerTaskEntry.Next;
                timerTaskEntry.Next = null;
                timerTaskEntry.Prev = null;
                timerTaskEntry.List = null;
                taskCounter.DecrementAndGet();
            }    
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Flush(Action<TimerTaskEntry> func)
        {
            var head = root.Next;
            while (head != root)
            {
                Remove(head);
                func(head);
                head = root.Next;
            }
            Interlocked.Exchange(ref expiration, -1L);
        }

        public long GetDelay()
        {
            return expiration;
        }

        public int CompareTo(TimerTaskList other)
        {
            return expiration.CompareTo(other.expiration);
        }
    }

    internal class TimerTaskEntry : IComparable<TimerTaskEntry>
    {
        internal volatile TimerTaskList List;

        internal TimerTaskEntry Next;

        internal TimerTaskEntry Prev;

        internal TimerTask timerTask;

        internal long expirationMs;

        internal TimerTaskEntry()
        {
            Init(null, -1L);
        }

        internal void Init(TimerTask initTimerTask, long initExpirationMs)
        {
            expirationMs = initExpirationMs;
            timerTask = initTimerTask;
            if (timerTask != null)
                timerTask.SetTimerTaskEntry(this);
        }

        internal bool Cancelled { 
            get 
            {
                return timerTask.TimerTaskEntry != this && timerTask.task == null;
            } 
        }

        public int CompareTo(TimerTaskEntry other)
        {
            return expirationMs.CompareTo(other.expirationMs);
        }

        internal void Remove()
        {
            var currentList = List;

            while(currentList != null)
            {
                currentList.Remove(this);
                currentList = List;
            }
        }
    }
}
