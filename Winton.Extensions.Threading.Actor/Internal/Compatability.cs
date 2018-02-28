#if NET451
using System;
#endif
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal static class Compatability
    {
        public static Task CompletedTask { get; } =
#if NET451
            ((Func<Task>)(() =>
            {
                var taskCompletionSource = new TaskCompletionSource<object>();
                taskCompletionSource.SetResult(null);
                return taskCompletionSource.Task;
            }))();
#else
            Task.CompletedTask;
#endif
    }
}
