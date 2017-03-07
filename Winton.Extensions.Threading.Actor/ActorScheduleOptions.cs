// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Options for scheduling work using <see cref="IActorWorkScheduler"/>.
    /// </summary>
    [Flags]
    public enum ActorScheduleOptions
    {
        /// <summary>
        /// Default: all options turned off.
        /// </summary>
        Default = 0,
        /// <summary>
        /// By default, the first time work will be enqueued is after the given repeat interval.  Setting this
        /// option ensures the work is initially enqueued without delay.
        /// </summary>
        NoInitialDelay = 1,
        /// <summary>
        /// Specifies that the work to be done will tend to be long-running.  Ideally for an actor (which should always be
        /// responsive and should not block on work) this would never be used.  However, one use case might be if an actor
        /// were being used as a "single-threaded" work slave for another actor.
        /// </summary>
        WorkIsLongRunning = 2
    }
}
