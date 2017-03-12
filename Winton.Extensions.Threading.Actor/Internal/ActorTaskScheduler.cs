using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskScheduler : TaskScheduler
    {
        private readonly ActorId _actorId;
        private readonly object _lockObject = new object();
        private readonly ActorTaskSchedulerState _state = new ActorTaskSchedulerState();
        private readonly ActorSynchronizationContext _synchronizationContext;
        private readonly Queue<Task> _taskQueue = new Queue<Task>();
        private readonly IWorkItemQueuer _workItemQueuer;

        public ActorTaskScheduler(ActorId actorId, IWorkItemQueuer workItemQueuer, IActorTaskFactory actorTaskFactory)
        {
            _actorId = actorId;
            _workItemQueuer = workItemQueuer;
            _synchronizationContext = new ActorSynchronizationContext(this, actorTaskFactory);
        }

        public override int MaximumConcurrencyLevel => 1;

        public void Terminate()
        {
            lock (_lockObject)
            {
                _state.MarkTerminated();
            }
        }

        protected override void QueueTask(Task task)
        {
            if (task.IsActorTask())
            {
                lock (_lockObject)
                {
                    if (_state.IsTerminated)
                    {
                        task.Cancel();
                        return;
                    }

                    if (!_state.IsActive)
                    {
                        _state.MarkActive();
                        LaunchNew(task);
                    }
                    else
                    {
                        _taskQueue.Enqueue(task);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Task is not an actor task.");
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
                                case YieldReason.Terminated:
                                    ClearTaskQueue();
                                    break;
                                case YieldReason.QueueEmpty:
                                    _state.MarkInactive();
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
                                    case YieldReason.Terminated:
                                        CleanUpAfterExecute();
                                        ClearTaskQueue();
                                        return;
                                    case YieldReason.QueueEmpty:
                                        CleanUpAfterExecute();
                                        _state.MarkInactive();
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
            SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
            Actor.CurrentId = _actorId;
        }

        private void CleanUpAfterExecute()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            Actor.CurrentId = ActorId.None;
        }

        private YieldReason CheckForReasonToYield()
        {
            if (_state.IsTerminated)
            {
                return YieldReason.Terminated;
            }

            if (_taskQueue.Count == 0)
            {
                return YieldReason.QueueEmpty;
            }

            return YieldReason.None;
        }
    }
}
