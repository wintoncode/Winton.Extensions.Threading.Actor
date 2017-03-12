// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Factories and extensions for actors.
    /// </summary>
    public sealed class Actor : IActor
    {
        [ThreadStatic]
        private static ActorId _currentId;

        private readonly IActor _impl;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Actor()
            : this(new ActorImpl(new ActorTaskFactory()))
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="actorTaskFactory">Actor task factory implementation to use. If <value>null</value> then use default.</param>
        internal Actor(IActorTaskFactory actorTaskFactory)
            : this(new ActorImpl(actorTaskFactory ?? new ActorTaskFactory()))
        {
        }

        private Actor(ActorImpl impl)
        {
            _impl = impl;
        }

        /// <summary>
        /// Get the ID of the actor on the current thread. Returns default <see cref="ActorId"/> if the current thread is
        /// not an actor thread.
        /// </summary>
        public static ActorId CurrentId
        {
            get { return _currentId; }
            internal set { _currentId = value; }
        }

        /// <inheritdoc cref="IActor.Id"/>
        public ActorId Id => _impl.Id;

        /// <inheritdoc cref="IActor.StartWork"/>
        public ActorStartWork StartWork
        {
            set { _impl.StartWork = value; }
        }

        /// <inheritdoc cref="IActor.StopWork"/>
        public ActorStopWork StopWork
        {
            set { _impl.StopWork = value; }
        }

        /// <inheritdoc cref="IActor.Start"/>
        public Task Start()
        {
            return _impl.Start();
        }

        /// <inheritdoc cref="IActor.Stop"/>
        public Task Stop()
        {
            return _impl.Stop();
        }

        /// <inheritdoc cref="IActor.Enqueue(Action,CancellationToken,ActorEnqueueOptions)"/>
        public Task Enqueue(Action action, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            return _impl.Enqueue(action, cancellationToken, options);
        }

        /// <inheritdoc cref="IActor.Enqueue{T}(Func{T},CancellationToken,ActorEnqueueOptions)"/>
        public Task<T> Enqueue<T>(Func<T> function, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            return _impl.Enqueue(function, cancellationToken, options);
        }

        /// <inheritdoc cref="IActor.Enqueue(Func{Task},CancellationToken,ActorEnqueueOptions)"/>
        public Task Enqueue(Func<Task> asyncAction, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            return _impl.Enqueue(asyncAction, cancellationToken, options);
        }

        /// <inheritdoc cref="IActor.Enqueue{T}(Func{Task{T}},CancellationToken,ActorEnqueueOptions)"/>
        public Task<T> Enqueue<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            return _impl.Enqueue(asyncFunction, cancellationToken, options);
        }
    }
}
