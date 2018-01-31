using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class ActiveActorState : ActorState
    {
        public ActiveActorState(ActorContext context)
            : base(context)
        {
        }

        protected override void StartImpl()
        {
        }

        protected override void ScheduleImpl(Task task)
        {
            Context.StartTask(task);
        }

        protected override void StopImpl()
        {
            Context.SetState<StoppingActorState>();
        }

        protected override void EnterImpl()
        {
            Context.StartCompletionSource.SetResult(true);

            foreach (var task in Context.InitialWorkQueue)
            {
                Context.StartTask(task);
            }

            Context.InitialWorkQueue.Clear();
        }
    }
}
