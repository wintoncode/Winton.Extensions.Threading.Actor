using System;

namespace Winton.Extensions.Threading.Actor.Internal
{
    [Flags]
    internal enum ActorTaskTraits
    {
        None = 0,
        Resuming = 1,
        Critical = 1 << 2, // If the task does not run to completion then the scheduler will be terminated
        Terminal = 1 << 3,
        CriticalResumer = Critical | Resuming
    }
}
