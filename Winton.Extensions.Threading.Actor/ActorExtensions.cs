// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Factories and extensions for actors.
    /// </summary>
    public static class ActorExtensions
    {
        /// <summary>
        /// Specify work for actor start-up.
        /// </summary>
        /// <param name="self">Actor</param>
        /// <param name="work">Work to do.</param>
        /// <returns>Actor</returns>
        public static IActor WithStartWork(this IActor self, ActorStartWork work)
        {
            self.StartWork = work;
            return self;
        }

        /// <summary>
        /// Specify work for actor start-up.
        /// </summary>
        /// <param name="self">Actor</param>
        /// <param name="work">Work to do.</param>
        /// <returns>Actor</returns>
        public static IActor WithStartWork(this IActor self, Action work) => self.WithStartWork(new ActorStartWork(SuppressTransactionScopeWrapper(work)));

        /// <summary>
        /// Specify work for actor start-up.
        /// </summary>
        /// <param name="self">Actor</param>
        /// <param name="work">Async work to do.</param>
        /// <returns>Actor</returns>
        public static IActor WithStartWork(this IActor self, Func<Task> work) => self.WithStartWork(new ActorStartWork(SuppressTransactionScopeWrapper(work)));

        /// <summary>
        /// Specify work for actor start-up.
        /// </summary>
        /// <param name="self">Actor</param>
        /// <param name="work">Work to do.</param>
        /// <returns>Actor</returns>
        public static IActor WithStopWork(this IActor self, ActorStopWork work)
        {
            self.StopWork = work;
            return self;
        }

        public static IActor WithStopWork(this IActor self, Action work) => self.WithStopWork(new ActorStopWork(SuppressTransactionScopeWrapper(work)));

        /// <summary>
        /// Enqueue a procedure.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Action work) => self.Enqueue(work, CancellationToken.None, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue a function.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<T> work) => self.Enqueue(work, CancellationToken.None, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue an async procedure.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Func<Task> work) => self.Enqueue(work, CancellationToken.None, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue an async function.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<Task<T>> work) => self.Enqueue(work, CancellationToken.None, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue a procedure with special options.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="options">Enqueue options.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Action work, ActorEnqueueOptions options) => self.Enqueue(work, CancellationToken.None, options);

        /// <summary>
        /// Enqueue a function with special options.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="options">Enqueue options.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<T> work, ActorEnqueueOptions options) => self.Enqueue(work, CancellationToken.None, options);

        /// <summary>
        /// Enqueue an async procedure with special options.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="options">Enqueue options.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Func<Task> work, ActorEnqueueOptions options) => self.Enqueue(work, CancellationToken.None, options);

        /// <summary>
        /// Enqueue an async function with special options.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="options">Enqueue options.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<Task<T>> work, ActorEnqueueOptions options) => self.Enqueue(work, CancellationToken.None, options);

        /// <summary>
        /// Enqueue a cancellable procedure.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Action work, CancellationToken cancellationToken) => self.Enqueue(work, cancellationToken, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue a cancellable function.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<T> work, CancellationToken cancellationToken) => self.Enqueue(work, cancellationToken, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue a cancellable async procedure.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Work completion task.</returns>
        public static Task Enqueue(this IActor self, Func<Task> work, CancellationToken cancellationToken) => self.Enqueue(work, cancellationToken, ActorEnqueueOptions.Default);

        /// <summary>
        /// Enqueue a cancellable async function.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <param name="work">The work.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Function result task.</returns>
        public static Task<T> Enqueue<T>(this IActor self, Func<Task<T>> work, CancellationToken cancellationToken) => self.Enqueue(work, cancellationToken, ActorEnqueueOptions.Default);

        /// <summary>
        /// Returns a <see cref="CancellationToken"/> that is cancelled when the given actor stops.
        /// </summary>
        /// <param name="self">The actor.</param>
        /// <returns></returns>
        public static CancellationToken StoppedToken(this IActor self)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Task.Run(
                async () =>
                {
                    try
                    {
                        await self.StoppedTask;
                    }
                    catch
                    {
                    }

                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                });

            return cancellationTokenSource.Token;
        }

        internal static Func<Task<T>> SuppressTransactionScopeWrapper<T>(Func<Task<T>> function)
        {
            return async () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Suppress,
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        return await function().ConfigureAwait(false);
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            };
        }

        internal static Func<Task> SuppressTransactionScopeWrapper(Func<Task> function)
        {
            return async () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Suppress,
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        await function().ConfigureAwait(false);
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            };
        }

        internal static Func<T> SuppressTransactionScopeWrapper<T>(Func<T> function)
        {
            return () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Suppress,
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        return function();
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            };
        }

        internal static Action SuppressTransactionScopeWrapper(Action action)
        {
            return () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Suppress,
                    TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            };
        }
    }
}