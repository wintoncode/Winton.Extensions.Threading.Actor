// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    /// <inheritdoc />
    public sealed class Actor : IActor
    {
        private static readonly Action _noOp = () => { };

        [ThreadStatic]
        private static ActorId _currentId;

        private readonly ActorTaskFactory _actorTaskFactory;
        private readonly ActorTaskScheduler _taskScheduler;
        private readonly object _stateChangeLockObject = new object();
        private readonly TaskCompletionSource<object> _startTaskCompletionSource = new TaskCompletionSource<object>();
        private readonly Task _startTask;

        private ActorStartWork _startWork = ActorStartWork.None;
        private ActorStopWork _stopWork = ActorStopWork.None;
        private StateName _currentState = StateName.Initial;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Actor()
        {
            _taskScheduler = new ActorTaskScheduler(Id);
            _actorTaskFactory = new ActorTaskFactory(_taskScheduler);
            _startTask = _startTaskCompletionSource.Task;
        }

        /// <summary>
        /// Get the ID of the actor on the current thread. Returns default <see cref="ActorId"/> if the current thread is
        /// not an actor thread.
        /// </summary>
        public static ActorId CurrentId
        {
            get => _currentId;
            internal set => _currentId = value;
        }

        /// <inheritdoc />
        public ActorId Id { get; } = ActorId.NewId();

        /// <inheritdoc />
        public ActorStartWork StartWork
        {
            get => _startWork;
            set
            {
                lock (_stateChangeLockObject)
                {
                    if (_currentState != StateName.Initial)
                    {
                        throw new InvalidOperationException("Start work cannot be specified after starting an actor.");
                    }

                    if (!ReferenceEquals(_startWork, ActorStartWork.None))
                    {
                        throw new InvalidOperationException("Start work already specified.");
                    }

                    _startWork = value ?? throw new ArgumentNullException(nameof(value), "Start work should not be null.");
                }
            }
        }

        /// <inheritdoc />
        public ActorStopWork StopWork
        {
            set
            {
                lock (_stateChangeLockObject)
                {
                    if (_currentState != StateName.Initial)
                    {
                        throw new InvalidOperationException("Stop work cannot be specified after starting an actor.");
                    }

                    if (!ReferenceEquals(_stopWork, ActorStopWork.None))
                    {
                        throw new InvalidOperationException("Stop work already specified.");
                    }

                    _stopWork = value ?? throw new ArgumentNullException(nameof(value), "Stop work should not be null.");
                }
            }
        }

        /// <inheritdoc />
        public Task StoppedTask => _taskScheduler.TerminatedTask;

        /// <inheritdoc cref="IActor.Start"/>
        public async Task Start()
        {
            Monitor.Enter(_stateChangeLockObject);

            if (_currentState == StateName.Initial)
            {
                _currentState = StateName.Active;
                Monitor.Exit(_stateChangeLockObject);

                try
                {
                    switch (_startWork.Type)
                    {
                        case ActorStateChangeWork.WorkType.NoOp:
                            await Enqueue(_noOp, CancellationToken.None, ActorEnqueueOptions.Default, ActorTaskTraits.CriticalResumer);
                            break;
                        case ActorStateChangeWork.WorkType.Synchronous:
                            await Enqueue(_startWork.SyncWork, _startWork.CancellationToken, _startWork.Options, ActorTaskTraits.CriticalResumer);
                            break;
                        case ActorStateChangeWork.WorkType.Asynchronous:
                            try
                            {
                                // Running the start work on the default scheduler is fine as the actor itself is currently paused. Doing so
                                // makes it trivial to ensure async start work is complete before unpausing the actor.
                                await await Task.Factory.StartNew(_startWork.AsyncWork, _startWork.CancellationToken, ActorTaskOptions.GetTaskCreationOptions(_startWork.Options), TaskScheduler.Default);
                                await Enqueue(_noOp, CancellationToken.None, ActorEnqueueOptions.Default, ActorTaskTraits.CriticalResumer);
                            }
                            catch
                            {
                                await Enqueue(_noOp, CancellationToken.None, ActorEnqueueOptions.Default, ActorTaskTraits.Resuming | ActorTaskTraits.Terminal);
                                throw;
                            }

                            break;
                    }

                    _startTaskCompletionSource.SetResult(null);
                }
                catch (Exception exception)
                {
                    if (exception is OperationCanceledException)
                    {
                        _startTaskCompletionSource.SetCanceled();
                    }
                    else
                    {
                        _startTaskCompletionSource.SetException(exception);
                    }

                    lock (_stateChangeLockObject)
                    {
                        _currentState = StateName.Stopped;
                    }

                    throw;
                }
            }
            else
            {
                Monitor.Exit(_stateChangeLockObject);

                await _startTask.ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public Task Stop()
        {
            lock (_stateChangeLockObject)
            {
                switch (_currentState)
                {
                    case StateName.Initial:
                        _startTaskCompletionSource.SetResult(null);
                        Enqueue(_noOp, CancellationToken.None, ActorEnqueueOptions.Default, ActorTaskTraits.Resuming | ActorTaskTraits.Terminal);
                        break;
                    case StateName.Active:
                        switch (_stopWork.Type)
                        {
                            case ActorStateChangeWork.WorkType.NoOp:
                                Enqueue(_noOp, CancellationToken.None, ActorEnqueueOptions.Default, ActorTaskTraits.Terminal);
                                break;
                            case ActorStateChangeWork.WorkType.Synchronous:
                                Enqueue(_stopWork.SyncWork, _stopWork.CancellationToken, _stopWork.Options, ActorTaskTraits.Terminal);
                                break;
                        }
                        break;
                }

                _currentState = StateName.Stopped;
            }

            return StoppedTask;
        }

        /// <inheritdoc />
        public Task Enqueue(Action action, CancellationToken cancellationToken, ActorEnqueueOptions options) => Enqueue(action, cancellationToken, options, ActorTaskTraits.None);

        /// <inheritdoc />
        public Task<T> Enqueue<T>(Func<T> function, CancellationToken cancellationToken, ActorEnqueueOptions options) => Enqueue(function, cancellationToken, options, ActorTaskTraits.None);

        /// <inheritdoc />
        public Task Enqueue(Func<Task> asyncAction, CancellationToken cancellationToken, ActorEnqueueOptions options) => Enqueue(asyncAction, cancellationToken, options, ActorTaskTraits.None);

        /// <inheritdoc />
        public Task<T> Enqueue<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, ActorEnqueueOptions options) => Enqueue(asyncFunction, cancellationToken, options, ActorTaskTraits.None);

        private Task Enqueue(Action action, CancellationToken cancellationToken, ActorEnqueueOptions options, ActorTaskTraits taskTraits) => _actorTaskFactory.StartNew(action, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options), options, taskTraits);

        private Task<T> Enqueue<T>(Func<T> function, CancellationToken cancellationToken, ActorEnqueueOptions options, ActorTaskTraits taskTraits) => _actorTaskFactory.StartNew(function, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options), options, taskTraits);

        private async Task Enqueue(Func<Task> asyncAction, CancellationToken cancellationToken, ActorEnqueueOptions options, ActorTaskTraits taskTraits)
        {
            var task = _actorTaskFactory.StartNew(asyncAction, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options), options, taskTraits);
            var nestedTask = await task.ConfigureAwait(false);
            await nestedTask.ConfigureAwait(false);
        }

        private async Task<T> Enqueue<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, ActorEnqueueOptions options, ActorTaskTraits taskTraits)
        {
            var task = _actorTaskFactory.StartNew(asyncFunction, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options), options, taskTraits);
            var nestedTask = await task.ConfigureAwait(false);
            return await nestedTask.ConfigureAwait(false);
        }

        private enum StateName
        {
            Initial,
            Active,
            Stopped
        }
   }
}
