using System;
using System.Collections.Generic;
using Timer.Colletcions;

namespace Timer
{
    public class SystemTimer : IDisposable
    {
        public readonly string Name;

        public readonly AtomicInt32 taskCounter;

        readonly DelayQueue<TimerTaskList> delayQueue;

        readonly TimingWheel timingWheel;

        readonly object _lock = new object();

        readonly Stack<TimerTask> timerTasks;

        readonly Stack<TimerTaskEntry> timerTaskEntries;

        long currMs;

        /// <summary>
        /// The ID of the timer will increase automatically every time it is added. The maximum ID is the maximum value of ulong
        /// </summary>
        ulong index = 0L;

        readonly Dictionary<ulong, TimerTaskEntry> runningTimerTaskEntry;

        public SystemTimer(string executorName, long startMs)
        {
            currMs = startMs;
            Name = executorName;
            taskCounter = new AtomicInt32(0);
            delayQueue = new DelayQueue<TimerTaskList>();
            timingWheel = new TimingWheel(startMs, taskCounter, delayQueue);

            timerTasks = new Stack<TimerTask>();
            timerTaskEntries = new Stack<TimerTaskEntry>();
            runningTimerTaskEntry = new Dictionary<ulong, TimerTaskEntry>();
        }

        public void Add(IExcuteAble task, long delay)
        {
            if (task == null)
                return;
            lock (_lock)
            {
                TimerTask timerTask;
                TimerTaskEntry timerTaskEntry;
                index++;
                task.Index = index;
                if (timerTasks.Count > 0)
                    timerTask = timerTasks.Pop();
                else
                    timerTask = new TimerTask();
                timerTask.task = task;

                if (timerTaskEntries.Count > 0)
                    timerTaskEntry = timerTaskEntries.Pop();
                else
                    timerTaskEntry = new TimerTaskEntry();
                
                timerTaskEntry.Init(timerTask, currMs + delay);
                runningTimerTaskEntry.Add(index, timerTaskEntry);
                AddTimerTaskEntry(timerTaskEntry);
            }
        }

        public void Remove(IExcuteAble task)
        {
            if (task == null)
                return;
            lock (_lock)
            {
                if(runningTimerTaskEntry.TryGetValue(task.Index, out var taskEntry))
                {
                    var timerTask = taskEntry.timerTask;
                    timerTask.Cancel();
                    timerTasks.Push(timerTask);
                    timerTaskEntries.Push(taskEntry);
                    runningTimerTaskEntry.Remove(task.Index);
                }
            }
        }


        public bool AdvanceClock(long timeoutMs)
        {
            currMs = timeoutMs;
            var bucket = delayQueue.Poll(timeoutMs);
            if (bucket != null)
            {
                lock (_lock)
                {
                    while (bucket != null)
                    {
                        timingWheel.AdvanceClock(bucket.expiration);
                        bucket.Flush(AddTimerTaskEntry);
                        bucket = delayQueue.Poll(timeoutMs);
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Clean up external references
        /// </summary>
        public void Dispose()
        {
            var bucket = delayQueue.Poll();
            lock (_lock)
            {
                while (bucket != null)
                {
                    bucket.Flush(DisposeTimerTaskEntry);
                    bucket = delayQueue.Poll();
                }
            }
        }

        /// <summary>
        /// Add timer entry
        /// If the timeout and not cancelled, execute the timer task
        /// And recycle unnecessary TimerTaskEntry and TimerTask
        /// </summary>
        /// <param name="timerTaskEntry"></param>
        private void AddTimerTaskEntry(TimerTaskEntry timerTaskEntry)
        {
            if(!timingWheel.Add(timerTaskEntry))
            {
                var timerTask = timerTaskEntry.timerTask;
                if (!timerTaskEntry.Cancelled)
                {
                    IExcuteAble task = timerTask.task;
                    runningTimerTaskEntry.Remove(task.Index);
                    timerTask.Cancel();
                    task.Excute();
                }
                timerTasks.Push(timerTask);
                timerTaskEntries.Push(timerTaskEntry);
            }
        }

        private void DisposeTimerTaskEntry(TimerTaskEntry timerTaskEntry)
        {
            var timerTask = timerTaskEntry.timerTask;
            if (!timerTaskEntry.Cancelled)
            {
                timerTask.Cancel();
            }
        }
    }
}
