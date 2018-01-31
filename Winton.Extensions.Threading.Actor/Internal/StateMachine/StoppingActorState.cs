using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class StoppingActorState : ActorState
    {
        public StoppingActorState(ActorContext context)
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
            var runStopWork = Context.StartCompletionSource.Task.Status == TaskStatus.RanToCompletion;

            Context.StartCompletionSource.TrySetResult(true);

            Action finalWork =
                () =>
                {
                    try
                    {
                        Context.StopWork.CancellationToken.ThrowIfCancellationRequested();

                        if (runStopWork)
                        {
                            Context.StopWork.SyncWork();
                        }
                    }
                    finally
                    {
                        Context.TerminateTaskScheduler();
                    }
                };
            var finalTask = Context.ActorTaskFactory.Create(finalWork, CancellationToken.None, Context.StopWork.TaskCreationOptions);

            Task.Run(async () =>
                     {
                         try
                         {
                             await finalTask;
                             Context.StopCompletionSource.SetResult(true);
                         }
                         catch (OperationCanceledException)
                         {
                             Context.StopCompletionSource.SetCanceled();
                         }
                         catch (Exception exception)
                         {
                             Context.StopCompletionSource.SetException(exception);
                         }
                         finally
                         {
                             Context.SetState<StoppedActorState>();
                         }
                     });

            Context.StartTask(finalTask);

            foreach (var task in Context.InitialWorkQueue.Concat(Context.InitialWorkToBeCancelledQueue))
            {
                task.Cancel();
            }

            Context.InitialWorkQueue.Clear();
            Context.InitialWorkToBeCancelledQueue.Clear();
        }
    }
}
