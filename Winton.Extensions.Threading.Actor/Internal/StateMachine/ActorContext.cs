using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class ActorContext
    {
        private readonly object _currentStateLockObject = new object();

        private bool _changingState = false;
        private ActorState _currentState;
        private ActorStartWork _startWork = ActorStartWork.None;
        private ActorStopWork _stopWork = ActorStopWork.None;

        public ActorContext(IActorTaskFactory actorTaskFactory)
        {
            ActorTaskFactory = actorTaskFactory;
            TaskScheduler = new ActorTaskScheduler(Id, new WorkItemQueuer(), actorTaskFactory);
            SetState<InitialActorState>();
        }

        public ActorId Id { get; } = ActorId.NewId();

        public List<Task> InitialWorkQueue { get; } = new List<Task>();

        public List<Task> InitialWorkToBeCancelledQueue { get; } = new List<Task>();

        public IActorTaskFactory ActorTaskFactory { get; }

        public ActorStartWork StartWork
        {
            get { return _startWork; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Start work should not be null.");
                }

                if (!InInitialState)
                {
                    throw new InvalidOperationException("Start work cannot be specified after starting an actor.");
                }

                if (!ReferenceEquals(_startWork, ActorStartWork.None))
                {
                    throw new InvalidOperationException("Start work already specified.");
                }

                _startWork = value;
            }
        }

        public TaskCompletionSource<bool> StartCompletionSource { get; } = new TaskCompletionSource<bool>();

        public ActorStopWork StopWork
        {
            get { return _stopWork; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Stop work should not be null.");
                }

                if (!InInitialState)
                {
                    throw new InvalidOperationException("Stop work cannot be specified after starting an actor.");
                }

                if (!ReferenceEquals(_stopWork, ActorStopWork.None))
                {
                    throw new InvalidOperationException("Stop work already specified.");
                }

                _stopWork = value;
            }
        }

        public TaskCompletionSource<bool> StopCompletionSource { get; } = new TaskCompletionSource<bool>();

        public void SetState<T>()
            where T : ActorState
        {
            SetState(typeof(T));
        }

        public Task Start()
        {
            lock (_currentStateLockObject)
            {
                return _currentState.Start();
            }
        }

        public Task Stop()
        {
            lock (_currentStateLockObject)
            {
                return _currentState.Stop();
            }
        }

        public void Schedule(Task task)
        {
            lock (_currentStateLockObject)
            {
                _currentState.Schedule(task);
            }
        }

        public void StartTask(Task task)
        {
            try
            {
                task.Start(TaskScheduler);
            }
            catch (InvalidOperationException)
                when (task.IsCanceled)
            {
                // There's a potential race whereby a task can be scheduled just after it's been cancelled
                // and this is the nicest way to intercept that: we don't want to propagate the exception as
                // a client will see the task is cancelled anyway.
            }
        }

        public void TerminateTaskScheduler()
        {
            TaskScheduler.Terminate();
        }

        private bool InInitialState => _currentState is InitialActorState;

        private ActorTaskScheduler TaskScheduler { get; }

        private void SetState(Type stateType)
        {
            Monitor.Enter(_currentStateLockObject);

            if (_changingState)
            {
                throw new InvalidOperationException("Currently mid-state change.");
            }

            _changingState = true;

            try
            {
                _currentState?.Exit();
                _currentState = (ActorState)Activator.CreateInstance(stateType, this);
                _currentState.Enter();
            }
            finally
            {
                _changingState = false;
                Monitor.Exit(_currentStateLockObject);
            }
        }
    }
}
