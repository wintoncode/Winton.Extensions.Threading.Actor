using System;
using System.Threading;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal.StateMachine;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorImpl : IActor
    {
        private readonly TaskCompletionSource<object> _stoppedPromise = new TaskCompletionSource<object>();

        public ActorImpl(IActorTaskFactory actorTaskFactory)
        {
            Context = new ActorContext(actorTaskFactory);
        }

        public ActorId Id => Context.Id;

        public ActorStartWork StartWork
        {
            set => Context.StartWork = value;
        }

        public ActorStopWork StopWork
        {
            set => Context.StopWork = value;
        }

        public Task StoppedTask => _stoppedPromise.Task;

        public Task Start()
        {
            return Context.Start();
        }

        public async Task Stop()
        {
            await Context.Stop();
            _stoppedPromise.TrySetResult(null);
        }

        public Task Enqueue(Action action, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            var task = Context.ActorTaskFactory.Create(action, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options));
            Context.Schedule(task);
            return task;
        }

        public Task<T> Enqueue<T>(Func<T> function, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            var task = Context.ActorTaskFactory.Create(function, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options));
            Context.Schedule(task);
            return task;
        }

        public async Task Enqueue(Func<Task> asyncAction, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            var task = Context.ActorTaskFactory.Create(asyncAction, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options));
            Context.Schedule(task);
            await await task.ConfigureAwait(false);
        }

        public async Task<T> Enqueue<T>(Func<Task<T>> asyncFunction, CancellationToken cancellationToken, ActorEnqueueOptions options)
        {
            var task = Context.ActorTaskFactory.Create(asyncFunction, cancellationToken, ActorTaskOptions.GetTaskCreationOptions(options));
            Context.Schedule(task);
            return await await task.ConfigureAwait(false);
        }

        private ActorContext Context { get; }
    }
}
