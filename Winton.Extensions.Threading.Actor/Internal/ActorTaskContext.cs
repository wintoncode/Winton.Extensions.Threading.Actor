using System.Threading;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskContext
    {
        public ActorTaskContext(CancellationToken cancellationToken)
        {
            Canceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        public CancellationTokenSource Canceller { get; }
    }
}
