// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Factories and extensions for actor work schedulers.
    /// </summary>
    public static class ActorSchedulingExtensions
    {
        /// <summary>
        /// Create a work scheduler for an actor.
        /// </summary>
        /// <param name="actor">The actor to target.</param>
        /// <returns>A work scheduler instance.</returns>
        public static IActorWorkScheduler CreateWorkScheduler(this IActor actor) => new ActorWorkScheduler(actor);

        /// <summary>
        /// A wrapper round IActorWorkScheduler.Schedule() that reschedules work after an unxexpected error has occurred in that work.
        /// </summary>
        /// <param name="self">The schedule.</param>
        /// <param name="work">The work to perform repeatedly.</param>
        /// <param name="interval">Interval at which the work is to be repeated.</param>
        /// <param name="options">Options.  See <see cref="ActorScheduleOptions"/> for details.</param>
        /// <param name="unexpectedErrorHandler">Handler for any unexpected exception.</param>
        /// <returns>A task: the only possible completion is due to cancellation.</returns>
        public static Task Schedule(this IActorWorkScheduler self, Action work, TimeSpan interval, ActorScheduleOptions options, Action<Exception> unexpectedErrorHandler)
        {
            return ScheduleImpl(self.Schedule(work, interval, options), () => self.Schedule(work, interval, RemoveFlag(options, ActorScheduleOptions.NoInitialDelay)), unexpectedErrorHandler);
        }

        /// <summary>
        /// A wrapper round IActorWorkScheduler.Schedule() that reschedules work after an unxexpected error has occurred in that work.
        /// </summary>
        /// <param name="self">The schedule.</param>
        /// <param name="work">The work to perform repeatedly.</param>
        /// <param name="interval">Interval at which the work is to be repeated.</param>
        /// <param name="options">Options.  See <see cref="ActorScheduleOptions"/> for details.</param>
        /// <param name="unexpectedErrorHandler">Handler for any unexpected exception.</param>
        /// <returns>A task: the only possible completion is due to cancellation.</returns>
        public static Task Schedule(this IActorWorkScheduler self, Func<Task> work, TimeSpan interval, ActorScheduleOptions options, Action<Exception> unexpectedErrorHandler)
        {
            return ScheduleImpl(self.Schedule(work, interval, options), () => self.Schedule(work, interval, RemoveFlag(options, ActorScheduleOptions.NoInitialDelay)), unexpectedErrorHandler);
        }

        private static async Task ScheduleImpl(Task task, Func<Task> rescheduler, Action<Exception> unexpectedErrorHandler)
        {
            while (true)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (AggregateException exception)
                {
                    unexpectedErrorHandler(exception.InnerExceptions.First());
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    unexpectedErrorHandler(exception);
                }

                task = rescheduler();
            }
        }

        private static ActorScheduleOptions RemoveFlag(ActorScheduleOptions options, ActorScheduleOptions toRemove)
        {
            if (options.HasFlag(toRemove))
            {
                return options ^ toRemove;
            }
            else
            {
                return options;
            }
        }
    }
}
