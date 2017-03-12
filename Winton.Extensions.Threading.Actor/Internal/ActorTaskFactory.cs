using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskFactory : IActorTaskFactory
    {
        public Task FromException(Exception exception)
        {
#if NET451
            var taskCompletionSource = new TaskCompletionSource<object>();
            taskCompletionSource.SetException(exception);
            return taskCompletionSource.Task;
#else
            return Task.FromException(exception);
#endif
        }

        public Task FromCompleted()
        {
            return Task.FromResult(true);
        }

        public Task Create(Action<object> action, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state)
        {
            return new Task(action, state, cancellationToken, taskCreationOptions);
        }

        public Task<T> Create<T>(Func<object, T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state)
        {
            return new Task<T>(function, state, cancellationToken, taskCreationOptions);
        }

        public Task CreateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }
}