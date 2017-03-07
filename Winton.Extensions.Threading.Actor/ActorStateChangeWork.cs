// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// A base class for work that is performed when an actor changes state (i.e. starts or stops).
    /// </summary>
    public abstract class ActorStateChangeWork
    {
        internal ActorStateChangeWork(Action syncWork, Func<Task> asyncWork, CancellationToken cancellationToken, WorkType workType)
        {
            if (workType == WorkType.Synchronous && syncWork == null)
            {
                throw new ArgumentNullException(nameof(syncWork));
            }

            if (workType == WorkType.Asynchronous && asyncWork == null)
            {
                throw new ArgumentNullException(nameof(asyncWork));
            }

            if (syncWork != null && asyncWork != null)
            {
                throw new ArgumentException("Only one of syncWork and asyncWork should be specified.");
            }

            Type = workType;
            SyncWork = syncWork;
            AsyncWork = asyncWork;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Work options. See <see cref="ActorEnqueueOptions"/> for details.
        /// </summary>
        public ActorEnqueueOptions Options { get; set; } = ActorEnqueueOptions.Default;

        internal WorkType Type { get; }

        internal Action SyncWork { get; }

        internal Func<Task> AsyncWork { get; }

        internal CancellationToken CancellationToken { get; }

        internal TaskCreationOptions TaskCreationOptions => ActorTaskOptions.GetTaskCreationOptions(Options);

        internal enum WorkType
        {
            Synchronous,
            Asynchronous,
            NoOp
        }
    }
}
