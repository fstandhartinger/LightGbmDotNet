using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LightGbmDotNet
{
    public class ReadAheadEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> sourcEnumerable;
        private readonly Action whenFinishedReadingAhedAction;
        private readonly int maxAhead;

        public ReadAheadEnumerable(IEnumerable<T> enumerable, TaskScheduler taskScheduler = null, Action whenFinishedReadingAhedAction = null, int maxAhead = int.MaxValue)
        {
            scheduler = taskScheduler;
            sourcEnumerable = enumerable;
            this.whenFinishedReadingAhedAction = whenFinishedReadingAhedAction;
            this.maxAhead = maxAhead;
            BeginReadingAhead();
        }

        public int ReadAheadCount
        {
            get
            {
                lock (queue)
                {
                    return queue.Count;
                }
            }
        }

        private void BeginReadingAhead()
        {
            var taskFactory = scheduler != null ? new TaskFactory(scheduler) : Task.Factory;
            taskFactory.StartNew(ReadIntoQueue);
        }

        private readonly Queue<T> queue = new Queue<T>();
        private bool isDoneEnumeratingSource;

        public event EventHandler IsDoneEnumeratingInBackground;

        protected virtual void OnIsDoneEnumeratingInBackground()
        {
            whenFinishedReadingAhedAction?.Invoke();
            var handler = IsDoneEnumeratingInBackground;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private static readonly T defaultInstance = default(T);

        private Exception exceptionInBackgroundTask;

        private void ReadIntoQueue()
        {
            try
            {
                foreach (var e in sourcEnumerable)
                {
                    OnRecordReceived();
                    if (maxAhead != int.MaxValue)
                    {
                        while (true)
                        {
                            int count;
                            lock (queue)
                                count = queue.Count;
                            if (count < maxAhead)
                                break;
                            Thread.Sleep(50);
                        }
                    }

                    lock (queue)
                    {
                        queue.Enqueue(e);
                    }
                    var waitHandle = waitingBecauseQueueIsEmpty;
                    waitHandle?.Set();
                }
                isDoneEnumeratingSource = true;
                OnIsDoneEnumeratingInBackground();
            }
            catch (Exception exc)
            {
                exceptionInBackgroundTask = exc;
            }
        }

        private IEnumerable<T> GetTargetEnumerable()
        {
            while (true)
            {
                if (exceptionInBackgroundTask != null)
                    throw new Exception("Error in background task", exceptionInBackgroundTask);
                var obj = defaultInstance;
                var isEmpty = false;
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        obj = queue.Dequeue();
                    }
                    else
                    {
                        if (isDoneEnumeratingSource)
                            yield break;
                        isEmpty = true;
                        waitingBecauseQueueIsEmpty = new AutoResetEvent(false);
                    }
                }

                if (isEmpty)
                {
                    OnQueueRanEmpty();
                    while (ReadAheadCount == 0)
                    {
                        waitingBecauseQueueIsEmpty.WaitOne(50);
                        if (ReadAheadCount == 0 && isDoneEnumeratingSource)
                            yield break;
                    }
                    waitingBecauseQueueIsEmpty = null;
                    OnFirstRecordReceivedAfterQueueWasEmpty();
                    continue;
                }

                yield return obj;
            }
        }

        public event EventHandler QueueRanEmpty;

        protected virtual void OnQueueRanEmpty()
        {
            var handler = QueueRanEmpty;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler FirstRecordReceivedAfterQueueWasEmpty;

        protected virtual void OnFirstRecordReceivedAfterQueueWasEmpty()
        {
            var handler = FirstRecordReceivedAfterQueueWasEmpty;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler RecordReceived;

        protected virtual void OnRecordReceived()
        {
            var handler = RecordReceived;
            handler?.Invoke(this, EventArgs.Empty);
        }

        private AutoResetEvent waitingBecauseQueueIsEmpty;
        private readonly TaskScheduler scheduler;

        public IEnumerator<T> GetEnumerator()
        {
            return GetTargetEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}