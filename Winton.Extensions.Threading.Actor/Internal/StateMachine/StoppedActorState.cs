using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class StoppedActorState : ActorState
    {
        public StoppedActorState(ActorContext context)
            : base(context)
        {
        }

        protected override void StartImpl()
        {
        }

        protected override void ScheduleImpl(Task task)
        {
            task.Cancel();
        }

        protected override void StopImpl()
        {
        }

        protected override void EnterImpl()
        {
            foreach (var task in Context.InitialWorkQueue)
            {
                task.Cancel();
            }

            Context.InitialWorkQueue.Clear();
        }
    }
}
