using System;
using System.Threading;
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
            var finalWork =
                (Action)(() =>
                         {
                             try
                             {
                                 Context.StopWork.CancellationToken.ThrowIfCancellationRequested();
                                 Context.StopWork.SyncWork();
                             }
                             finally
                             {
                                 Context.TerminateTaskScheduler();
                             }
                         });

            var finalTask = Context.ActorTaskFactory.Create(finalWork, CancellationToken.None, Context.StopWork.TaskCreationOptions);

            Task.Run(async () =>
                     {
                         try
                         {
                             await finalTask;
                             Context.StopCompletionSource.SetResult(true);
                         }
                         catch (Exception exception)
                         {
                             if (exception is TaskCanceledException)
                             {
                                 Context.StopCompletionSource.SetCanceled();
                             }
                             else
                             {
                                 Context.StopCompletionSource.SetException(exception);
                             }
                         }
                     });

            Context.StartTask(finalTask);
            Context.SetState<StoppedActorState>();
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
