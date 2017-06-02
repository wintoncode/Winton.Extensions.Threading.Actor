using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly ActorTaskScheduler _scheduler;
        private readonly IActorTaskFactory _actorTaskFactory;
        private readonly ActorTaskKind _actorTaskKind;

        public ActorSynchronizationContext(ActorTaskScheduler scheduler, IActorTaskFactory actorTaskFactory, ActorTaskKind actorTaskKind)
        {
            _scheduler = scheduler;
            _actorTaskFactory = actorTaskFactory;
            _actorTaskKind = actorTaskKind;
        }

        public ActorSynchronizationContext ChangeActorTaskKind(ActorTaskKind actorTaskKind)
        {
            return new ActorSynchronizationContext(_scheduler, _actorTaskFactory, actorTaskKind);
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            _actorTaskFactory.Create(() => callback(state), CancellationToken.None, TaskCreationOptions.HideScheduler, _actorTaskKind).Start(_scheduler);
        }
    }
}
