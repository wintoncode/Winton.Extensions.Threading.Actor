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
                Context.SetState<ActiveActorState>();
            }
        }

        protected override void StopImpl()
        {
            Context.SetState<StoppingActorState>();
        }

        protected override void ScheduleImpl(Task task)
        {
            Context.InitialWorkQueue.Add(task);
        }
    }
}
