// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;

namespace Winton.Extensions.Threading.Actor.Tests.Utilities
{
    /// <summary>
    /// A time source for testing whereby the current time can be fixed.
    /// </summary>
    /// <remarks>
    /// This can be read by multiple threads but it's assumed that it's written to by the thread that creates the source
    /// (i.e. the thread running the unit test).
    /// </remarks>
    public sealed class FixableTimeSource
    {
        private readonly int _ownerThreadId = Thread.CurrentThread.ManagedThreadId;

        private long _ticks;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="initialTime">
        /// The time to initially fix to.  If null then the time used is <see cref="DateTime.UtcNow"/>
        /// </param>
        public FixableTimeSource(DateTime? initialTime = null)
        {
            _ticks = (initialTime ?? DateTime.UtcNow).Ticks;
        }

        public DateTime Current => new DateTime(Interlocked.Read(ref _ticks));

        public event Action<DateTime> OnUpdate;

        public void Increment(TimeSpan increment)
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException("All time updates must be performed on the thread that created the object.");
            }

            var newTime = new DateTime(Interlocked.Add(ref _ticks, increment.Ticks));
            OnUpdate?.Invoke(newTime);
        }
    }
}
