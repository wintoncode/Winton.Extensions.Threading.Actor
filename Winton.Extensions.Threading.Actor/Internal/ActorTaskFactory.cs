using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskFactory
    {
        private readonly ActorTaskScheduler _scheduler;

        public ActorTaskFactory(ActorTaskScheduler taskScheduler) => _scheduler = taskScheduler;

        public Task StartNew(Action action, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            return Task.Factory.StartNew(action, cancellationTokenSource.Token, taskCreationOptions | TaskCreationOptions.HideScheduler, _scheduler);
        }

        public Task<T> StartNew<T>(Func<T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            return Task.Factory.StartNew(function, cancellationTokenSource.Token, taskCreationOptions | TaskCreationOptions.HideScheduler, _scheduler);
        }
    }
}