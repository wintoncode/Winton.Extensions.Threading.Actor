using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class ActorWorkScheduler : IActorWorkScheduler
    {
        private readonly IActor _actor;
        private readonly IActorTaskFactory _actorTaskFactory;

        private CancellationTokenSource _cancellationTokenSource = null;

        public ActorWorkScheduler(IActor actor, IActorTaskFactory actorTaskFactory)
        {
            _actor = actor;
            _actorTaskFactory = actorTaskFactory;
        }

        public Task Schedule(Action work, TimeSpan interval, ActorScheduleOptions options)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            var enqueueOptions = GetEnqueueOptions(options);

            CancelCurrent();
            _cancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = _cancellationTokenSource.Token;

            return Schedule(() => _actor.Enqueue(work, cancellationToken, enqueueOptions), interval, options, cancellationToken);
        }

        public Task Schedule(Func<Task> work, TimeSpan interval, ActorScheduleOptions options = ActorScheduleOptions.Default)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            var enqueueOptions = GetEnqueueOptions(options);

            CancelCurrent();
            _cancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = _cancellationTokenSource.Token;

            return Schedule(() => _actor.Enqueue(work, cancellationToken, enqueueOptions), interval, options, cancellationToken);
        }

        public void CancelCurrent()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task Schedule(Func<Task> enqueuer, TimeSpan interval, ActorScheduleOptions options, CancellationToken cancellationToken)
        {
            if (!options.HasFlag(ActorScheduleOptions.NoInitialDelay))
            {
                await GetDelay(interval, cancellationToken).ConfigureAwait(false);
            }

            await enqueuer().ConfigureAwait(false);

            while (true)
            {
                await GetDelay(interval, cancellationToken).ConfigureAwait(false);
                await enqueuer().ConfigureAwait(false);
            }
        }

        private Task GetDelay(TimeSpan interval, CancellationToken cancellationToken)
        {
            return _actorTaskFactory.CreateDelay(interval, cancellationToken);
        }

        private static ActorEnqueueOptions GetEnqueueOptions(ActorScheduleOptions actorScheduleOptions)
        {
            if (actorScheduleOptions.HasFlag(ActorScheduleOptions.WorkIsLongRunning))
            {
                return ActorEnqueueOptions.WorkIsLongRunning;
            }
            else
            {
                return ActorEnqueueOptions.Default;
            }
        }
    }
}
