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

        private readonly ActorId _actorId;
        private readonly object _lockObject = new object();
        private readonly ActorTaskSchedulerStatusManager _statusManager = new ActorTaskSchedulerStatusManager();
        private readonly ActorSynchronizationContext _synchronizationContext;
        private readonly Queue<Task> _taskQueue = new Queue<Task>();
        private readonly IWorkItemQueuer _workItemQueuer;

        private ActorSynchronizationContext _resumingSynchronizationContext;

        public ActorTaskScheduler(ActorId actorId, IWorkItemQueuer workItemQueuer, IActorTaskFactory actorTaskFactory)
        {
            _actorId = actorId;
            _workItemQueuer = workItemQueuer;
            _synchronizationContext = new ActorSynchronizationContext(this, actorTaskFactory, ActorTaskKind.Standard);
        }

        public static ActorTaskScheduler CurrentActorScheduler => _currentActorScheduler;

        public override int MaximumConcurrencyLevel => 1;

        public void Terminate()
        {
            lock (_lockObject)
            {
                _statusManager.MarkTerminated();
            }
        }

        public void WhileActorPaused(Task task)
        {
            lock (_lockObject)
            {
                if (_statusManager.State == ActorTaskSchedulerStatus.Terminated)
                {
                    return;
                }

                _resumingSynchronizationContext = _resumingSynchronizationContext ?? _synchronizationContext.ChangeActorTaskKind(ActorTaskKind.Resumer);
                SynchronizationContext.SetSynchronizationContext(_resumingSynchronizationContext);
                _statusManager.MarkPaused();
            }
        }

        protected override void QueueTask(Task task)
        {
            var actorTaskContext = task.GetActorTaskContext();

            lock (_lockObject)
            {
                switch (_statusManager.State)
                {
                    case ActorTaskSchedulerStatus.Terminated:
                        actorTaskContext.Canceller.Cancel();
                        break;
                    case ActorTaskSchedulerStatus.Inactive:
                        _statusManager.MarkActive();
                        LaunchNew(task);
                        break;
                    case ActorTaskSchedulerStatus.Active:
                        _taskQueue.Enqueue(task);
                        break;
                    case ActorTaskSchedulerStatus.Paused:
                        if (actorTaskContext.Kind == ActorTaskKind.Resumer)
                        {
                            _statusManager.MarkActive();
                            LaunchNew(task);
                        }
                        else
                        {
                            _taskQueue.Enqueue(task);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotSupportedException();
        }

        private enum YieldReason
        {
            None = 0,
            Paused,
            Terminated,
            QueueEmpty
        }

        private void LaunchNew(Task task)
        {
            if (!task.CreationOptions.HasFlag(TaskCreationOptions.LongRunning))
            {
                _workItemQueuer.QueueOnThreadPoolThread(
                    () =>
                    {
                        PrepareForExecute();

                        TryExecuteTask(task);

                        CleanUpAfterExecute();

                        lock (_lockObject)
                        {
                            switch (CheckForReasonToYield())
                            {
                                case YieldReason.Paused:
                                    break;
                                case YieldReason.Terminated:
                                    ClearTaskQueue();
                                    break;
                                case YieldReason.QueueEmpty:
                                    _statusManager.MarkInactive();
                                    break;
                                case YieldReason.None:
                                    LaunchNew(_taskQueue.Dequeue());
                                    break;
                            }
                        }
                    });
            }
            else
            {
                _workItemQueuer.QueueOnNonThreadPoolThread(
                    () =>
                    {
                        PrepareForExecute();

                        while (true)
                        {
                            TryExecuteTask(task);

                            lock (_lockObject)
                            {
                                switch (CheckForReasonToYield())
                                {
                                    case YieldReason.Paused:
                                        CleanUpAfterExecute();
                                        return;
                                    case YieldReason.Terminated:
                                        CleanUpAfterExecute();
                                        ClearTaskQueue();
                                        return;
                                    case YieldReason.QueueEmpty:
                                        CleanUpAfterExecute();
                                        _statusManager.MarkInactive();
                                        return;
                                    case YieldReason.None:
                                        task = _taskQueue.Dequeue();
                                        break;
                                }
                            }
                        }
                    });
            }
        }

        private void ClearTaskQueue()
        {
            while (_taskQueue.Count > 0)
            {
                _taskQueue.Dequeue().Cancel();
            }
        }

        private void PrepareForExecute()
        {
            _currentActorScheduler = this;
            SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
            Actor.CurrentId = _actorId;
        }

        private void CleanUpAfterExecute()
        {
            _currentActorScheduler = null;
            SynchronizationContext.SetSynchronizationContext(null);
            Actor.CurrentId = ActorId.None;
        }

        private YieldReason CheckForReasonToYield()
        {
            if (_statusManager.State == ActorTaskSchedulerStatus.Terminated)
            {
                return YieldReason.Terminated;
            }

            if (_statusManager.State == ActorTaskSchedulerStatus.Paused)
            {
                return YieldReason.Paused;
            }

            if (_taskQueue.Count == 0)
            {
                return YieldReason.QueueEmpty;
            }

            return YieldReason.None;
        }
    }
}
