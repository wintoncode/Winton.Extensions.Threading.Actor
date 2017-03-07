// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// A means of scheduling repeated work on an actor.
    /// </summary>
    /// <remarks>
    /// There is no thread safety guarantee for instances of this.
    /// You can't schedule functions, just procedures (both synchronous and asynchronous).
    /// </remarks>
    public interface IActorWorkScheduler
    {
        /// <summary>
        /// Schedule or reschedule work.
        /// </summary>
        /// <remarks>
        /// Each call to this will cancel work scheduled during a previous call.  If you want to schedule multiple pieces of
        /// work
        /// then use multiple schedulers or put it all in one procedure.
        /// Scheduling work merely consists of enqueuing it on the actor - no guarantee is made that the work will actually be
        /// executed at
        /// fixed intervals.
        /// Work is rescheduled each time after completion of the previous execution unless it propagates an error - in which
        /// case the work must be rescheduled.
        /// </remarks>
        /// <param name="work">The work to perform repeatedly.</param>
        /// <param name="interval">Interval at which the work is to be repeated.</param>
        /// <param name="options">Options.  See <see cref="ActorScheduleOptions"/> for details.</param>
        /// <returns>
        /// A task: completion of this indicates that work will no longer be scheduled.  Completion will be due to
        /// cancellation or an error.
        /// </returns>
        Task Schedule(Action work, TimeSpan interval, ActorScheduleOptions options = ActorScheduleOptions.Default);

        /// <summary>
        /// Schedule or reschedule asynchronous work.
        /// </summary>
        /// <remarks>
        /// Each call to this will cancel work scheduled during a previous call.  If you want to schedule multiple pieces of
        /// work
        /// then use multiple schedulers or put it all in one procedure.
        /// Scheduling work merely consists of enqueuing it on the actor - no guarantee is made that the work will actually be
        /// executed at
        /// fixed intervals.
        /// Work is rescheduled each time after completion of the previous execution unless it propagates an error - in which
        /// case the work must be rescheduled.
        /// </remarks>
        /// <param name="work">The work to perform repeatedly.</param>
        /// <param name="interval">Interval at which the work is to be repeated.</param>
        /// <param name="options">Options.  See <see cref="ActorScheduleOptions"/> for details.</param>
        /// <returns>
        /// A task: completion of this indicates that work will no longer be scheduled.  Completion will be due to
        /// cancellation or an error.
        /// </returns>
        Task Schedule(Func<Task> work, TimeSpan interval, ActorScheduleOptions options = ActorScheduleOptions.Default);

        /// <summary>
        /// Cancels the current schedule.
        /// </summary>
        void CancelCurrent();
    }
}
