using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly ActorTaskScheduler _scheduler;
        private readonly IActorTaskFactory _actorTaskFactory;

        public ActorSynchronizationContext(ActorTaskScheduler scheduler, IActorTaskFactory actorTaskFactory)
        {
            _scheduler = scheduler;
            _actorTaskFactory = actorTaskFactory;
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            _actorTaskFactory.Create(() => callback(state), CancellationToken.None, TaskCreationOptions.HideScheduler).Start(_scheduler);
        }
    }
}
