// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;

namespace Winton.Extensions.Threading.Actor.Tests.Utilities
{
    public static class Expect
    {
        public static Action That(Action action)
        {
            return action;
        }

        public static Func<T> That<T>(Func<T> func)
        {
            return func;
        }
    }
}