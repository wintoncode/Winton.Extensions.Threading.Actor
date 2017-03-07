using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class InitialActorState : ActorState
    {
        public InitialActorState(ActorContext context)
            : base(context)
        {
        }

        protected override void StartImpl()
        {
            var work = Context.StartWork;

            if (work.Type != ActorStateChangeWork.WorkType.NoOp)
            {
                Context.SetState<StartingActorState>();
            }
            else
            {
                Context.StartCompletionSource.SetResult(true);
                Context.SetState<ActiveActorState>();
            }
        }

        protected override void StopImpl()
        {
            Context.StartCompletionSource.SetResult(true);
            Context.StopCompletionSource.SetResult(true);
            Context.SetState<StoppedActorState>();
        }

        protected override void ScheduleImpl(Task task)
        {
            Context.InitialWorkQueue.Add(task);
        }
    }
}
