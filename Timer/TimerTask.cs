using System.Runtime.CompilerServices;

namespace Timer
{

    public interface IExcuteAble
    {
        ulong Index { get; set; }
        void Excute();
    }

    internal class TimerTask
    {
        internal IExcuteAble task;

        internal TimerTaskEntry TimerTaskEntry;

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Cancel()
        {
            if (TimerTaskEntry != null)
                TimerTaskEntry.Remove();
            TimerTaskEntry = null;
            task = null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void SetTimerTaskEntry(TimerTaskEntry entry)
        {
            if (TimerTaskEntry != null && TimerTaskEntry != entry)
                TimerTaskEntry.Remove();
            TimerTaskEntry = entry;
        }
    }
}
