using System;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal.StateMachine
{
    internal sealed class StartingActorState : ActorState
    {
        public StartingActorState(ActorContext context)
            : base(context)
        {
        }

        protected override void StartImpl()
        {
        }

        protected override void StopImpl()
        {
            ReceivedStopSignal = true;
        }

        protected override void EnterImpl()
        {
            if (Context.StartWork.Type == ActorStateChangeWork.WorkType.NoOp)
            {
                throw new InvalidOperationException();
            }

            var task = ScheduleStartWork(Context.StartWork);

            Task.Run(async () =>
                     {
                         try
                         {
                             await task.ConfigureAwait(false);

                             Context.SetState<ActiveActorState>();

                             if (ReceivedStopSignal)
                             {
                                 // Note: we can't simply transition to the Stopped state as, by contract, the pending enqueued
                                 // work must be processed prior to actor stopping if the start-up work completes without fault.
                                 await Context.Stop();
                             }
                         }
                         catch (Exception exception)
                         {
                             if (exception is TaskCanceledException)
                             {
                                 Context.StartCompletionSource.SetCanceled();
                             }
                             else
                             {
                                 Context.StartCompletionSource.SetException(exception);
                             }

                             Context.SetState<StoppingActorState>();
                         }
                     });
        }

        protected override void ScheduleImpl(Task task)
        {
            if (!ReceivedStopSignal)
            {
                Context.InitialWorkQueue.Add(task);
            }
            else
            {
                Context.InitialWorkToBeCancelledQueue.Add(task);
            }
        }

        private bool ReceivedStopSignal { get; set; } = false;

        private Task ScheduleStartWork(ActorStartWork work)
        {
            switch (work.Type)
            {
                case ActorStateChangeWork.WorkType.Synchronous:
                    return ScheduleSyncStartWork(work);
                case ActorStateChangeWork.WorkType.Asynchronous:
                    return ScheduleAsyncStartWork(work);
                default:
                    throw new InvalidOperationException($"Unexpected work type '{work.Type}'.");
            }
        }

        private Task ScheduleSyncStartWork(ActorStartWork work)
        {
            var task = Context.ActorTaskFactory.Create(work.SyncWork, work.CancellationToken, work.TaskCreationOptions);
            Context.StartTask(task);
            return task;
        }

        private async Task ScheduleAsyncStartWork(ActorStartWork work)
        {
            var task = Context.ActorTaskFactory.Create(work.AsyncWork, work.CancellationToken, work.TaskCreationOptions);
            Context.StartTask(task);
            await await task.ConfigureAwait(false);
        }
    }
}
