// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Threading;

namespace Winton.Extensions.Threading.Actor.Tests.Utilities
{
    public static class For
    {
        public static void OneSecond(Action action)
        {
            APeriodOf(action, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250));
        }

        private static void APeriodOf(Action action, TimeSpan maxWait, TimeSpan checkInterval)
        {
            var stopTime = DateTime.UtcNow + maxWait;

            while (DateTime.UtcNow < stopTime)
            {
                action();
                Thread.Sleep(checkInterval);
            }

            action();
        }
    }
}