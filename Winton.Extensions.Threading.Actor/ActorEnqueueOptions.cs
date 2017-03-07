// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// Options for enqueuing work on actors.
    /// </summary>
    [Flags]
    public enum ActorEnqueueOptions
    {
        /// <summary>
        /// Default: all options turned off.
        /// </summary>
        Default = 0,
        /// <summary>
        /// Specifies that the work to be done will tend to be long-running.
        /// </summary>
        WorkIsLongRunning = 1
    }
}
