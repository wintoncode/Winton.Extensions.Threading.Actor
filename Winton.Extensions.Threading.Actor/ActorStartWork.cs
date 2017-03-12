// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Specifies work to perform when the actor starts. The work specified here is guaranteed to be run before anything
    /// else. This applies even if the work specified is async and uses await.
    /// </summary>
    public sealed class ActorStartWork : ActorStateChangeWork
    {
        /// <summary>
        /// Specify (uncancellable) work to perform at start-up.
        /// </summary>
        /// <param name="syncWork">
        /// Work to do. Must not be <value>null</value>.
        /// </param>
        public ActorStartWork(Action syncWork)
            : this(syncWork, CancellationToken.None)
        {
        }

        /// <summary>
        /// Specify cancellable work to perform at start-up.
        /// </summary>
        /// <remarks>
        /// The actor will be stopped if the start-up work is cancelled: i.e. if the cancellation token is provided and cancel
        /// is requested though.
        /// </remarks>
        /// <param name="syncWork">
        /// Work to do. Must not be <value>null</value>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        public ActorStartWork(Action syncWork, CancellationToken cancellationToken)
            : this(syncWork, null, cancellationToken, WorkType.Synchronous)
        {
        }

        /// <summary>
        /// Specify (uncancellable) asynchronous work to perform at start-up.
        /// </summary>
        /// <param name="asyncWork">
        /// Work to do. Must not be <value>null</value>.
        /// </param>
        public ActorStartWork(Func<Task> asyncWork)
            : this(asyncWork, CancellationToken.None)
        {
        }

        /// <summary>
        /// Specify cancellable asynchronous work to perform at start-up.
        /// </summary>
        /// <remarks>
        /// The actor will be stopped if the start-up work is cancelled: i.e. if the cancellation token is provided and cancel
        /// is requested though.
        /// </remarks>
        /// <param name="asyncWork">
        /// Work to do. Must not be <value>null</value>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        public ActorStartWork(Func<Task> asyncWork, CancellationToken cancellationToken)
            : this(null, asyncWork, cancellationToken, WorkType.Asynchronous)
        {
        }

        private ActorStartWork(Action syncWork, Func<Task> asyncWork, CancellationToken cancellationToken, WorkType type)
            : base(syncWork, asyncWork, cancellationToken, type)
        {
        }

        /// <summary>
        /// For when nothing special should be done at start-up.
        /// </summary>
        public static ActorStartWork None { get; } = new ActorStartWork(null, null, CancellationToken.None, WorkType.NoOp);

    }
}
