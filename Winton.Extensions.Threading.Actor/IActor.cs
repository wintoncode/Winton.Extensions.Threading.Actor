// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// An actor designed to work integrate with the TPL.
    /// </summary>
    public interface IActor
    {
        /// <summary>
        /// A unique identifier for this actor.
        /// </summary>
        ActorId Id { get; }

        /// <summary>
        /// Work to perform when starting.
        /// </summary>
        /// <remarks>
        /// This work is guaranteed to be completed prior to processing any other work on the actor's queue.
        /// If <see cref="IActor.Stop"/> is called before <see cref="IActor.Start"/> then any work specified using this will
        /// not be performed.
        /// </remarks>
        ActorStartWork StartWork { set; }

        /// <summary>
        /// Work to perform when stopping.
        /// </summary>
        /// <remarks>
        /// If <see cref="IActor.Stop"/> is called before <see cref="IActor.Start"/> then any work specified using this will
        /// not be performed.
        /// </remarks>
        ActorStopWork StopWork { set; }

        /// <summary>
        /// This task completes when the actor stops.
        /// </summary>
        Task StoppedTask { get; }

        /// <summary>
        /// Start the actor.
        /// </summary>
        /// <remarks>
        /// Multiple calls will be ignored.
        /// </remarks>
        /// <returns>Task: completion of this indicates the start-up has completed.</returns>
        Task Start();

        /// <summary>
        /// Stop the actor after processing any work remaining on the queue.  Should only be called once.
        /// </summary>
        /// <remarks>
        /// Multiple calls will be ignored.
        /// </remarks>
        /// <returns>Task: completion of this indicates the shutdown has completed.</returns>
        Task Stop();

        /// <summary>
        /// Enqueue an action.
        /// </summary>
        /// <param name="action">Action.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="options">Enqueue options.  See <see cref="ActorEnqueueOptions"/> for details.</param>
        /// <returns>Handle to enqueued work.</returns>
        Task Enqueue(Action action, CancellationToken cancellationToken, ActorEnqueueOptions options);

        /// <summary>
        /// Enqueue a function.
        /// </summary>
        /// <typeparam name="T">Type of return value.</typeparam>
        /// <param name="function">Function.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="options">Enqueue options.  See <see cref="ActorEnqueueOptions"/> for details.</param>
        /// <returns>Handle to enqueued work</returns>
        Task<T> Enqueue<T>(Func<T> function, CancellationToken cancellationToken, ActorEnqueueOptions options);

        /// <summary>
        /// Enqueue an asynchronous action.  Such a function will await the return of one or more calls into
        /// other actors (or anything else return a Task) before resuming work on the current actor's thread.
        /// </summary>
        /// <example>
        /// actor.Enqueue(async () =>
        ///                     {
        ///                         var data = await _internalActor(() => GetData());
        ///                         PostProcessData(data);
        ///                     });
        /// </example>
        /// <param name="asyncAction">Function.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="options">Enqueue options.  See <see cref="ActorEnqueueOptions"/> for details.</param>
        /// <returns>Handle to enqueued work</returns>
        Task Enqueue(Func<Task> asyncAction, CancellationToken cancellationToken, ActorEnqueueOptions options);

        /// <summary>
        /// Enqueue an asynchronous function.  Such a function will await the return of one or more calls into
        /// other actors (or anything else return a Task) before resuming work on the current actor's thread.
        /// </summary>
        /// <example>
        /// actor.Enqueue(async () =>
        ///                     {
        ///                         var data = await _internalActor(() => GetData());
        ///                         return PostProcessData(data);
        ///                     });
        /// </example>
        /// <typeparam name="T">Type of return value.</typeparam>
        /// <param name="asyncFunction">Function.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="options">Enqueue options.  See <see cref="ActorEnqueueOptions"/> for details.</param>
        /// <returns>Handle to enqueued work</returns>
        Task<T> Enqueue<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, ActorEnqueueOptions options);
    }
}
