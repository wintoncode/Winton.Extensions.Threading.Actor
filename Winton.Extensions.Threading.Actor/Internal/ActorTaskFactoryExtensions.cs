using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal static class ActorTaskFactoryExtensions
    {
        private const TaskCreationOptions BaseCreationOptions = TaskCreationOptions.HideScheduler;

        public static Task Create(this IActorTaskFactory self, Action work, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions)
        {
            var actorTaskState = new ActorTaskContext(cancellationToken);
            return self.Create(state =>
                               {
                                   try
                                   {
                                       work();
                                   }
                                   finally
                                   {
                                       (state as ActorTaskContext)?.Canceller.Dispose();
                                   }
                               },
                               actorTaskState.Canceller.Token, taskCreationOptions | BaseCreationOptions, actorTaskState);
        }

        public static Task<T> Create<T>(this IActorTaskFactory self, Func<T> work, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions)
        {
            var actorTaskState = new ActorTaskContext(cancellationToken);
            return self.Create(state =>
                               {
                                   try
                                   {
                                       return work();
                                   }
                                   finally
                                   {
                                       (state as ActorTaskContext)?.Canceller.Dispose();
                                   }
                               },
                               actorTaskState.Canceller.Token, taskCreationOptions | BaseCreationOptions, actorTaskState);
        }
    }
}
