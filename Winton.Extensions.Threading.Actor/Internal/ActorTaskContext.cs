using System.Threading;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskContext
    {
        public ActorTaskContext(CancellationToken cancellationToken, ActorTaskKind kind)
        {
            Kind = kind;
            Canceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        public CancellationTokenSource Canceller { get; }

        public ActorTaskKind Kind { get; }
    }
}
