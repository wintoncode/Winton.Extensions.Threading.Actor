using System;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskSchedulerState
    {
        private State _state = State.Inactive;

        public bool IsActive => _state == State.Active;

        public bool IsTerminated => _state == State.Terminated;

        public void MarkInactive()
        {
            ValidateNotTerminated();
            _state = State.Inactive;
        }

        public void MarkActive()
        {
            ValidateNotTerminated();
            _state = State.Active;
        }

        public void MarkTerminated()
        {
            _state = State.Terminated;
        }

        private enum State
        {
            Inactive,
            Active,
            Terminated
        }

        private void ValidateNotTerminated()
        {
            if (IsTerminated)
            {
                throw new InvalidOperationException("Already terminated.");
            }
        }
    }
}
