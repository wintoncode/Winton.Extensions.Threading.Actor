using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorSynchronizationContext : SynchronizationContext
    {
        private readonly ActorTaskFactory _actorTaskFactory;
        private readonly ActorTaskTraits _actorTaskTraits;

        public ActorSynchronizationContext(ActorTaskFactory actorTaskFactory, ActorTaskTraits actorTaskTraits)
        {
            _actorTaskFactory = actorTaskFactory;
            _actorTaskTraits = actorTaskTraits;
        }

        public ActorSynchronizationContext ChangeActorTaskKind(ActorTaskTraits actorTaskTraits) => new ActorSynchronizationContext(_actorTaskFactory, actorTaskTraits);

        public override void Post(SendOrPostCallback callback, object state) => _actorTaskFactory.StartNew(() => callback(state), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, _actorTaskTraits);
    }
}
