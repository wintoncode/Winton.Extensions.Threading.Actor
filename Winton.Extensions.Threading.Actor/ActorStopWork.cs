// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Specifies work to perform when the actor stops.
    /// </summary>
    public sealed class ActorStopWork : ActorStateChangeWork
    {
        /// <summary>
        /// Specify (uncancellable) work to perform when stopping.
        /// </summary>
        /// <param name="work">
        /// Work to do.  Must not be
        /// <value>null</value>
        /// .
        /// </param>
        public ActorStopWork(Action work)
            : this(work, CancellationToken.None)
        {
        }

        /// <summary>
        /// Specify cancellable work to perform when stopping.
        /// </summary>
        /// <remarks>
        /// The actor will be stopped if the start-up work is cancelled: i.e. if the cancellation token is provided and cancel
        /// is requested though.
        /// </remarks>
        /// <param name="work">
        /// Work to do.  Must not be
        /// <value>null</value>
        /// .
        /// </param>
        /// <param name="cancellationToken">Cancellation token to use.</param>
        public ActorStopWork(Action work, CancellationToken cancellationToken)
            : base(work, null, cancellationToken, WorkType.Synchronous)
        {
        }

        /// <summary>
        /// For when nothing special should be done when stopping.
        /// </summary>
        public static ActorStopWork None { get; } = new ActorStopWork(() => { });

    }
}
