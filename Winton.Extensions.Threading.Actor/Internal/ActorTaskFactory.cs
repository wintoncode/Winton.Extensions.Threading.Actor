using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskFactory
    {
        private readonly ActorTaskScheduler _scheduler;

        public ActorTaskFactory(ActorTaskScheduler taskScheduler) => _scheduler = taskScheduler;

        public Task StartNew(Action action, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorEnqueueOptions actorEnqueueOptions = ActorEnqueueOptions.Default, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            if ((taskCreationOptions & TaskCreationOptions.LongRunning) > 0)
            {
                Console.WriteLine($"LongRunning enqueue from here: {new System.Diagnostics.StackTrace()}");
            }

            return Task.Factory.StartNew(
                actorEnqueueOptions.HasFlag(ActorEnqueueOptions.NoSuppressTransactionScope) ? action : ActorExtensions.SuppressTransactionScopeWrapper(action),
                cancellationTokenSource.Token,
                taskCreationOptions | TaskCreationOptions.HideScheduler,
                _scheduler);
        }

        public Task<Task> StartNew(Func<Task> asyncFunction, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorEnqueueOptions actorEnqueueOptions = ActorEnqueueOptions.Default, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            if ((taskCreationOptions & TaskCreationOptions.LongRunning) > 0)
            {
                Console.WriteLine($"LongRunning enqueue from here: {new System.Diagnostics.StackTrace()}");
            }

            return Task.Factory.StartNew(
                actorEnqueueOptions.HasFlag(ActorEnqueueOptions.NoSuppressTransactionScope) ? asyncFunction : ActorExtensions.SuppressTransactionScopeWrapper(asyncFunction),
                cancellationTokenSource.Token,
                taskCreationOptions | TaskCreationOptions.HideScheduler,
                _scheduler);
        }

        public Task<Task<T>> StartNew<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorEnqueueOptions actorEnqueueOptions = ActorEnqueueOptions.Default, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            if ((taskCreationOptions & TaskCreationOptions.LongRunning) > 0)
            {
                Console.WriteLine($"LongRunning enqueue from here: {new System.Diagnostics.StackTrace()}");
            }

            return Task.Factory.StartNew(
                actorEnqueueOptions.HasFlag(ActorEnqueueOptions.NoSuppressTransactionScope) ? asyncFunction : ActorExtensions.SuppressTransactionScopeWrapper(asyncFunction),
                cancellationTokenSource.Token,
                taskCreationOptions | TaskCreationOptions.HideScheduler,
                _scheduler);
        }

        public Task<T> StartNew<T>(Func<T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorEnqueueOptions actorEnqueueOptions = ActorEnqueueOptions.Default, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            if ((taskCreationOptions & TaskCreationOptions.LongRunning) > 0)
            {
                Console.WriteLine($"LongRunning enqueue from here: {new System.Diagnostics.StackTrace()}");
            }

            return Task.Factory.StartNew(
                actorEnqueueOptions.HasFlag(ActorEnqueueOptions.NoSuppressTransactionScope) ? function : ActorExtensions.SuppressTransactionScopeWrapper(function),
                cancellationTokenSource.Token,
                taskCreationOptions | TaskCreationOptions.HideScheduler,
                _scheduler);
        }
    }
}