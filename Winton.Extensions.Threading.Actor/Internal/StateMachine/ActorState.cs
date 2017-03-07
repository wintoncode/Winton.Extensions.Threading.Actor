using System;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal abstract class ActorState
    {
        protected ActorState(ActorContext context)
        {
            Context = context;
        }

        public Task Start()
        {
            try
            {
                StartImpl();
            }
            catch (Exception exception)
            {
                Context.StartCompletionSource.SetException(exception);
            }

            return Context.StartCompletionSource.Task;
        }

        public Task Stop()
        {
            try
            {
                StopImpl();
            }
            catch (Exception exception)
            {
                Context.StopCompletionSource.SetException(exception);
            }

            return Context.StopCompletionSource.Task;
        }

        public void Schedule(Task task)
        {
            ScheduleImpl(task);
        }

        public void Enter()
        {
            EnterImpl();
        }

        public void Exit()
        {
            ExitImpl();
        }

        protected virtual void EnterImpl()
        {
        }

        protected virtual void ExitImpl()
        {
        }

        protected ActorContext Context { get; }

        protected abstract void StartImpl();

        protected abstract void StopImpl();

        protected abstract void ScheduleImpl(Task task);
    }
}
