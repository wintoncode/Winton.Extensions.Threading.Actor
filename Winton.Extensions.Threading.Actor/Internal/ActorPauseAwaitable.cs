using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    /// <summary>
    /// Awaitable for controlling the pause/resume logic. Forces all awaits requiring a pause to be handled asynchronously
    /// to simplify matters elsewhere.
    /// </summary>
    public struct ActorPauseAwaitable : ICriticalNotifyCompletion
    {
        private readonly TaskAwaiter _taskAwaiter;

        public ActorPauseAwaitable GetAwaiter()
        {
            return this;
        }

        internal ActorPauseAwaitable(Task task)
        {
            _taskAwaiter = task.GetAwaiter();
            IsCompleted = false;
        }

        public bool IsCompleted { get; }

        public void OnCompleted(Action continuation)
        {
            _taskAwaiter.OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _taskAwaiter.UnsafeOnCompleted(continuation);
        }

        public void GetResult()
        {
            _taskAwaiter.GetResult();
        }
    }

    /// <summary>
    /// Awaitable for controlling the pause/resume logic. Forces all awaits requiring a pause to be handled asynchronously
    /// to simplify matters elsewhere.
    /// </summary>
    public struct ActorPauseAwaitable<TResult> : ICriticalNotifyCompletion
    {
        private readonly TaskAwaiter<TResult> _taskAwaiter;

        public ActorPauseAwaitable<TResult> GetAwaiter()
        {
            return this;
        }

        internal ActorPauseAwaitable(Task<TResult> task)
        {
            _taskAwaiter = task.GetAwaiter();
            IsCompleted = false;
        }

        public bool IsCompleted { get; }

        public void OnCompleted(Action continuation)
        {
            _taskAwaiter.OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _taskAwaiter.UnsafeOnCompleted(continuation);
        }

        public TResult GetResult()
        {
            return _taskAwaiter.GetResult();
        }
    }
}