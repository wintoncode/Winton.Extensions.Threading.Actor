using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTask
    {
        [ThreadStatic]
        private static CancellationTokenSource _currentCanceller;

        [ThreadStatic]
        private static ActorTaskTraits _currentActorTaskTraits;

        private CancellationTokenSource _canceller;

        public ActorTask(Task task)
        {
            Debug.Assert(_currentCanceller != null);

            Task = task;
            Traits = _currentActorTaskTraits;
            _canceller = _currentCanceller;

            _currentCanceller = null;
            _currentActorTaskTraits = ActorTaskTraits.None;
        }

        public static CancellationTokenSource CurrentCanceller
        {
            set => _currentCanceller = value;
        }

        public static ActorTaskTraits CurrentActorTaskTraits
        {
            set => _currentActorTaskTraits = value;
        }

        public ActorTaskTraits Traits { get; }

        public Task Task { get; }

        public ActorTask Next { get; set; }

        public void Cancel()
        {
            if (!(_canceller is null))
            {
                _canceller.Cancel();
                _canceller.Dispose();
                _canceller = null;
            }
        }

        public void CleanUpPostExecute()
        {
            if (!(_canceller is null))
            {
                _canceller.Dispose();
                _canceller = null;
            }
        }
    }
}
