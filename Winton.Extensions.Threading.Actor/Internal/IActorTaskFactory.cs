using System;
using System.Threading;
using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    /// <summary>
    /// A <see cref="Task"/> factory indirection allowing for unit testing of code that uses Tasks.
    /// </summary>
    internal interface IActorTaskFactory
    {
        /// <summary>
        /// Create a faulted <see cref="Task"/> from an exception.
        /// </summary>
        /// <param name="exception">Exception to wrap.</param>
        /// <returns>Task.</returns>
        Task FromException(Exception exception);

        /// <summary>
        /// Create a completed <see cref="Task"/> with no "return" value.
        /// </summary>
        /// <returns>Task.</returns>
        Task FromCompleted();

        /// <summary>
        /// Create an new unscheduled <see cref="Task"/>.
        /// </summary>
        /// <param name="action">Work for the task to perform.</param>
        /// <param name="cancellationToken">Cancellation token.  <see cref="CancellationToken.None"/> is acceptable.</param>
        /// <param name="taskCreationOptions">Creation options.</param>
        /// <param name="state">Async state</param>
        /// <returns>Task.</returns>
        Task Create(Action<object> action, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state);

        /// <summary>
        /// Create an new unscheduled <see cref="Task"/>.
        /// </summary>
        /// <param name="function">Work for the task to perform.</param>
        /// <param name="cancellationToken">Cancellation token.  <see cref="CancellationToken.None"/> is acceptable.</param>
        /// <param name="taskCreationOptions">Creation options.</param>
        /// <param name="state">Async state.</param>
        /// <returns>Task.</returns>
        Task<T> Create<T>(Func<object, T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state);

        /// <summary>
        /// Create a task that simulates a delay.  A proxy to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
        /// </summary>
        /// <param name="delay">Delay.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CreateDelay(TimeSpan delay, CancellationToken cancellationToken);
    }
}
