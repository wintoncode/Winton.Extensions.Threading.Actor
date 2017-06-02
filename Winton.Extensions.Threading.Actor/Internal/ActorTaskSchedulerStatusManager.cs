using System;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorTaskSchedulerStatusManager
    {
        public ActorTaskSchedulerStatus State { get; private set; } = ActorTaskSchedulerStatus.Inactive;

        public void MarkInactive()
        {
            ValidateNotTerminated();
            State = ActorTaskSchedulerStatus.Inactive;
        }

        public void MarkActive()
        {
            ValidateNotTerminated();
            State = ActorTaskSchedulerStatus.Active;
        }

        public void MarkPaused()
        {
            ValidateNotTerminated();
            State = ActorTaskSchedulerStatus.Paused;
        }

        public void MarkTerminated()
        {
            State = ActorTaskSchedulerStatus.Terminated;
        }

        private void ValidateNotTerminated()
        {
            if (State == ActorTaskSchedulerStatus.Terminated)
            {
                throw new InvalidOperationException("Already terminated.");
            }
        }
    }
}
