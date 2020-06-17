using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

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

            void TransactionScopeWrapper(Action theAction)
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        theAction();
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            }

            return Task.Factory.StartNew(() => TransactionScopeWrapper(action), cancellationTokenSource.Token, taskCreationOptions | TaskCreationOptions.HideScheduler, _scheduler);
        }

        public Task<T> StartNew<T>(Func<T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, ActorTaskTraits traits = ActorTaskTraits.None)
        {
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ActorTask.CurrentActorTaskTraits = traits;
            ActorTask.CurrentCanceller = cancellationTokenSource;

            T TransactionScopeWrapper(Func<T> theFunction)
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                {
                    try
                    {
                        return theFunction();
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }
            }

            return Task.Factory.StartNew(() => TransactionScopeWrapper(function), cancellationTokenSource.Token, taskCreationOptions | TaskCreationOptions.HideScheduler, _scheduler);
        }
    }
}