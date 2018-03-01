using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        private static ActorTaskScheduler _currentActorScheduler;

        [ThreadStatic]
        private static bool _shouldPause;

        private readonly ActorId _actorId;
        private readonly object _lockObject = new object();
        private readonly ActorSynchronizationContext _synchronizationContext;
        private readonly TaskCompletionSource<object> _terminatedTaskCompletionSource = new TaskCompletionSource<object>();

        private ActorTaskSchedulerStatus _status = ActorTaskSchedulerStatus.Paused;
        private ActorSynchronizationContext _resumingSynchronizationContext;
        private ActorTask _front;
        private ActorTask _back;
        private Thread _thread;

        public ActorTaskScheduler(ActorId actorId)
        {
            _actorId = actorId;
            _synchronizationContext = new ActorSynchronizationContext(new ActorTaskFactory(this), ActorTaskTraits.None);
        }

        public static ActorTaskScheduler CurrentActorScheduler => _currentActorScheduler;

        public override int MaximumConcurrencyLevel => 1;

        public Task TerminatedTask => _terminatedTaskCompletionSource.Task;

        public ActorPauseAwaitable WhileActorPaused(Task task)
        {
            Pause();
            return new ActorPauseAwaitable(task);
        }

        public ActorPauseAwaitable<T> WhileActorPaused<T>(Task<T> task)
        {
            Pause();
            return new ActorPauseAwaitable<T>(task);
        }

        private void Pause()
        {
            _resumingSynchronizationContext = _resumingSynchronizationContext ?? _synchronizationContext.ChangeActorTaskKind(ActorTaskTraits.Resuming);
            SynchronizationContext.SetSynchronizationContext(_resumingSynchronizationContext);
            _shouldPause = true;
        }

        protected override void QueueTask(Task task)
        {
            QueueTask(new ActorTask(task));
        }

        private void QueueTask(ActorTask actorTask)
        {
            TakeLock();

            switch (_status)
            {
                case ActorTaskSchedulerStatus.Terminated:
                    ReleaseLock();
                    CancelActorTask(actorTask);
                    break;
                case ActorTaskSchedulerStatus.Inactive:
                    _status = ActorTaskSchedulerStatus.Active;
                    ReleaseLock();
                    QueueForExecution(actorTask);
                    break;
                case ActorTaskSchedulerStatus.Active:
                    if ((actorTask.Traits & ActorTaskTraits.Resuming) == ActorTaskTraits.Resuming)
                    {
                        actorTask.Next = _front;
                        _front = actorTask;

                        if (_back is null)
                        {
                            _back = _front;
                        }
                    }
                    else
                    {
                        if (_front is null)
                        {
                            _front = actorTask;
                        }
                        else
                        {
                            _back.Next = actorTask;
                        }

                        _back = actorTask;
                    }

                    ReleaseLock();

                    break;
                case ActorTaskSchedulerStatus.Paused:
                    if ((actorTask.Traits & ActorTaskTraits.Resuming) == ActorTaskTraits.Resuming)
                    {
                        _status = ActorTaskSchedulerStatus.Active;
                        ReleaseLock();
                        QueueForExecution(actorTask);
                    }
                    else
                    {
                        if (_front is null)
                        {
                            _front = actorTask;
                        }
                        else
                        {
                            _back.Next = actorTask;
                        }

                        _back = actorTask;
                        ReleaseLock();
                    }

                    break;
            }
        }

        private void TakeLock()
        {
            Monitor.Enter(_lockObject);
        }

        private void ReleaseLock()
        {
            Monitor.Exit(_lockObject);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks() => throw new NotSupportedException();

        private void QueueForExecution(ActorTask actorTask)
        {
            if ((actorTask.Task.CreationOptions & TaskCreationOptions.LongRunning) == 0)
            {
                QueueOnThreadPool(actorTask);
            }
            else
            {
                QueueForExecutionOffThreadPool(actorTask);
            }
        }

        private void QueueForExecutionOffThreadPool(ActorTask actorTask)
        {
            _thread?.Join();
            _thread =
                new Thread(Execute)
                {
                    IsBackground = true,
                    Name = "ActorWorker"
                };
            _thread.Start(actorTask);
        }

        private void QueueOnThreadPool(ActorTask actorTask) =>
#if NETSTANDARD1_3
            ThreadPool.QueueUserWorkItem(Execute, actorTask);
#else
            ThreadPool.UnsafeQueueUserWorkItem(Execute, actorTask);
#endif

        private void Execute(object state)
        {
            var previousSynchronizationContext = PrepareThreadForExecute();
            var actorTask = (ActorTask)state;
            var onLongRunningThread = (actorTask.Task.CreationOptions & TaskCreationOptions.LongRunning) == TaskCreationOptions.LongRunning;
            var switchThreadType = false;

            while (!(actorTask is null) && !switchThreadType)
            {
                var task = actorTask.Task;
                var isTerminalTask = (actorTask.Traits & ActorTaskTraits.Terminal) != 0;

                TryExecuteTask(task);

                actorTask.CleanUpPostExecute();

                var shouldTerminate = isTerminalTask || (actorTask.Traits & ActorTaskTraits.Critical) != 0 && task.Status != TaskStatus.RanToCompletion;

                actorTask = null;

                TakeLock();

                if (shouldTerminate)
                {
                    _status = ActorTaskSchedulerStatus.Terminated;
                    ReleaseLock();
                    TerminationCleanUp(isTerminalTask ? task : null);
                }
                else if (_shouldPause && (_front is null || (_front.Traits & ActorTaskTraits.Resuming) == 0))
                {
                    _status = ActorTaskSchedulerStatus.Paused;
                    ReleaseLock();
                }
                else
                {
                    if (_front is null)
                    {
                        _status = ActorTaskSchedulerStatus.Inactive;
                    }
                    else
                    {
                        _shouldPause = false;
                        actorTask = _front;
                        _front = _front.Next;

                        if (_front is null)
                        {
                            _back = null;
                        }

                        if (!onLongRunningThread)
                        {
                            switchThreadType = (actorTask.Task.CreationOptions & TaskCreationOptions.LongRunning) == TaskCreationOptions.LongRunning;
                        }
                    }

                    ReleaseLock();
                }
            }

            CleanUpThreadAfterExecute(previousSynchronizationContext);

            if (switchThreadType)
            {
                QueueForExecution(actorTask);
            }
        }

        private void TerminationCleanUp(Task terminalTask)
        {
            var task = _front;
            _front = null;
            _back = null;

            while (!(task is null))
            {
                CancelActorTask(task);
                task = task.Next;
            }

            Task.Run(
                () =>
                {
                    _thread?.Join();

                    switch (terminalTask?.Status ?? TaskStatus.RanToCompletion)
                    {
                        case TaskStatus.Faulted:
                            _terminatedTaskCompletionSource.SetException(terminalTask.Exception.InnerExceptions);
                            break;
                        case TaskStatus.Canceled:
                            _terminatedTaskCompletionSource.SetCanceled();
                            break;
                        default:
                            _terminatedTaskCompletionSource.SetResult(null);
                            break;
                    }
                });
        }

        private void CancelActorTask(ActorTask task)
        {
            task.Cancel();
            TryExecuteTask(task.Task);
        }

        private SynchronizationContext PrepareThreadForExecute()
        {
            _shouldPause = false;
            _currentActorScheduler = this;
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
            Actor.CurrentId = _actorId;
            return previous;
        }

        private void CleanUpThreadAfterExecute(SynchronizationContext previousSynchronizationContext)
        {
            _currentActorScheduler = null;
            SynchronizationContext.SetSynchronizationContext(previousSynchronizationContext);
            Actor.CurrentId = ActorId.None;
        }
    }
}
