// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using FluentAssertions;
using Winton.Extensions.Threading.Actor.Internal;
using Winton.Extensions.Threading.Actor.Tests.Utilities;
using Xunit;

namespace Winton.Extensions.Threading.Actor.Tests.Unit.Internal
{
    public sealed class ActorWorkSchedulerTests
    {
        public enum WorkType
        {
            Sync,
            Async
        }

        private readonly IActor _actor;
        private readonly IActorTaskFactory _actorTaskFactory;
        private readonly IActorWorkScheduler _scheduler;
        private readonly FixableTimeSource _utcTimeSource;

        public ActorWorkSchedulerTests()
        {
            _utcTimeSource = new FixableTimeSource();
            _actorTaskFactory = new TestActorTaskFactory(_utcTimeSource);
            _actor = Mock.Of<IActor>();
            _scheduler = new ActorWorkScheduler(_actor, _actorTaskFactory);
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToScheduleWorkToRepeatAtAFixedInterval(WorkType workType)
        {
            SetUpActor(workType);

            var interval = TimeSpan.FromMilliseconds(400);
            var halfInterval = TimeSpan.FromMilliseconds(200);
            var times = new List<DateTime>();
            var expectedTimes = Enumerable.Range(1, 3).Select(x => _utcTimeSource.Current + TimeSpan.FromTicks(interval.Ticks * x)).ToList();

            switch (workType)
            {
                case WorkType.Sync:
                {
                    _scheduler.Schedule(() => times.Add(_utcTimeSource.Current), interval);
                }
                    break;
                case WorkType.Async:
                {
                    _scheduler.Schedule(async () =>
                                        {
                                            await Task.Yield();
                                            times.Add(_utcTimeSource.Current);
                                        }, interval);
                }
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            for (var i = 0; i < expectedTimes.Count; i++)
            {
                _utcTimeSource.Increment(halfInterval);
                _utcTimeSource.Increment(halfInterval);
                Within.FiveSeconds(() => times.Count.Should().Be(i + 1));
            }

            times.Should().Equal(expectedTimes);
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToScheduleWorkToStartImmediatelyBeforeRepeatingAtIntervals(WorkType workType)
        {
            SetUpActor(workType);
            var interval = TimeSpan.FromMilliseconds(1000);
            var times = new List<DateTime>();
            var expectedTimes = Enumerable.Range(0, 4).Select(x => _utcTimeSource.Current + TimeSpan.FromTicks(interval.Ticks * x)).ToList();

            switch (workType)
            {
                case WorkType.Sync:
                    _scheduler.Schedule(() => times.Add(_utcTimeSource.Current), interval, ActorScheduleOptions.NoInitialDelay);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            await Task.Yield();
                                            times.Add(_utcTimeSource.Current);
                                        }, interval, ActorScheduleOptions.NoInitialDelay);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            Within.OneSecond(() => times.Count.Should().Be(1));

            for (var i = 0; i < expectedTimes.Count - 1; i++)
            {
                _utcTimeSource.Increment(interval);
                Within.OneSecond(() => times.Count.Should().Be(i + 2));
            }

            times.Should().Equal(expectedTimes);
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToSpecifyThatWorkIsLongRunning(WorkType workType)
        {
            SetUpActor(workType);

            switch (workType)
            {
                case WorkType.Sync:
                {
                    var work = (Action)(() => { });
                    _scheduler.Schedule(work, TimeSpan.FromMilliseconds(100), ActorScheduleOptions.NoInitialDelay | ActorScheduleOptions.WorkIsLongRunning);
                    Within.FiveSeconds(() => Expect.That(() => Mock.Get(_actor).Verify(x => x.Enqueue(work, It.IsAny<CancellationToken>(), ActorEnqueueOptions.WorkIsLongRunning), Times.Once)).ShouldNotThrow());
                }
                    break;
                case WorkType.Async:
                {
                    var work = (Func<Task>)(async () => { await Task.Yield(); });
                    _scheduler.Schedule(work, TimeSpan.FromMilliseconds(100), ActorScheduleOptions.NoInitialDelay | ActorScheduleOptions.WorkIsLongRunning);
                    Within.FiveSeconds(() => Expect.That(() => Mock.Get(_actor).Verify(x => x.Enqueue(work, It.IsAny<CancellationToken>(), ActorEnqueueOptions.WorkIsLongRunning), Times.Once)).ShouldNotThrow());
                }
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToCancelSchedule(WorkType workType)
        {
            SetUpActor(workType);

            var output = new List<string>();
            var interval = TimeSpan.FromMilliseconds(100);
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                    task = _scheduler.Schedule(() => output.Add("one"), interval);
                    break;
                case WorkType.Async:
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   output.Add("one");
                                                   await Task.Yield();
                                               }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            _utcTimeSource.Increment(interval);

            Within.OneSecond(() => output.Should().Equal(Enumerable.Repeat("one", 1)));

            _scheduler.CancelCurrent();

            _utcTimeSource.Increment(interval);

            For.OneSecond(() => output.Count.Should().Be(1));

            task.IsCanceled.Should().BeTrue();
        }

        [Theory]
        [InlineData(WorkType.Async, WorkType.Async)]
        [InlineData(WorkType.Async, WorkType.Sync)]
        [InlineData(WorkType.Sync, WorkType.Async)]
        [InlineData(WorkType.Sync, WorkType.Sync)]
        public void ASecondCallToScheduleShouldCancelTheWorkPreviouslyScheduled(WorkType workType1, WorkType workType2)
        {
            SetUpActor(workType1);
            SetUpActor(workType2);

            var output = new List<string>();
            var interval = TimeSpan.FromMilliseconds(100);
            var task = default(Task);

            switch (workType1)
            {
                case WorkType.Sync:
                    task = _scheduler.Schedule(() => { output.Add("one"); }, interval);
                    break;
                case WorkType.Async:
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   await Task.Yield();
                                                   output.Add("one");
                                               }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType1}.");
            }

            _utcTimeSource.Increment(interval);

            Within.FiveSeconds(() => output.Count.Should().Be(1));

            switch (workType2)
            {
                case WorkType.Sync:
                    _scheduler.Schedule(() => { output.Add("two"); }, interval);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            output.Add("two");
                                            await Task.Yield();
                                        }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType2}.");
            }

            _utcTimeSource.Increment(interval);

            Within.FiveSeconds(() => output.Count.Should().Be(2));

            output.Should().Equal("one", "two");
            task.IsCanceled.Should().BeTrue();
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToConfigureScheduleToRescheduleInCaseOfUnexpectedErrorButNotCancellation(WorkType workType)
        {
            SetUpActor(workType);
            var interval = TimeSpan.FromMilliseconds(1000);
            var times = new List<DateTime>();
            var expectedTimes = Enumerable.Range(1, 6).Select(x => _utcTimeSource.Current + TimeSpan.FromTicks(interval.Ticks * x)).ToList();
            var emittedException = default(Exception);
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                    task = _scheduler.Schedule(() =>
                                               {
                                                   times.Add(_utcTimeSource.Current);

                                                   if (times.Count == 3)
                                                   {
                                                       throw new InvalidOperationException("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.Default, x => emittedException = x);
                    break;
                case WorkType.Async:
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   times.Add(_utcTimeSource.Current);
                                                   await Task.Yield();
                                                   
                                                   if (times.Count == 3)
                                                   {
                                                       throw new InvalidOperationException("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.Default, x => emittedException = x);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            for (var i = 0; i < expectedTimes.Count; i++)
            {
                _utcTimeSource.Increment(interval);
                Within.FiveSeconds(() => times.Count.Should().Be(i + 1));
            }

            times.Should().Equal(expectedTimes);
            emittedException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Pah!");

            _scheduler.CancelCurrent();

            Within.FiveSeconds(() => task.IsCanceled.Should().BeTrue());
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void WhenAnUnhandledErrorOccursInTheWorkTheScheduleShouldStopAndEmitTheError(WorkType workType)
        {
            SetUpActor(workType);
            var interval = TimeSpan.FromMilliseconds(1000);
            var times = new List<DateTime>();
            var expectedTimes = Enumerable.Range(0, 3).Select(x => _utcTimeSource.Current + TimeSpan.FromTicks(interval.Ticks * x)).ToList();
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                {
                    task = _scheduler.Schedule(() =>
                                               {
                                                   times.Add(_utcTimeSource.Current);

                                                   if (times.Count == 3)
                                                   {
                                                       throw new Exception("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.NoInitialDelay);
                }
                    break;
                case WorkType.Async:
                {
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   times.Add(_utcTimeSource.Current);
                                                   await Task.Yield();
                                                   
                                                   if (times.Count == 3)
                                                   {
                                                       throw new Exception("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.NoInitialDelay);
                }
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            // Should hit exception on final loop of this
            for (var i = 0; i < expectedTimes.Count - 1; i++)
            {
                Within.FiveSeconds(() => times.Count.Should().Be(i + 1));
                _utcTimeSource.Increment(interval);
            }

            Expect.That(async () => await task).ShouldThrow<Exception>().WithMessage("Pah!");

            // The schedule should have been cancelled so these should have no effect.
            _utcTimeSource.Increment(interval);
            _utcTimeSource.Increment(interval);

            Within.FiveSeconds(() => times.Should().Equal(expectedTimes));
        }

        [Fact]
        public void SynchronousSchedulerExtensionShouldEmitAnyArgumentOutOfRangeExceptions()
        {
            Expect.That(() => _scheduler.Schedule(() => { }, TimeSpan.Zero, ActorScheduleOptions.Default, x => { }))
                  .ShouldThrow<ArgumentOutOfRangeException>()
                  .And.ParamName.Should().Be("interval");
        }

        [Fact]
        public void SynchronousSchedulerExtensionShouldEmitAnyArgumentNullExceptions()
        {
            Expect.That(() => _scheduler.Schedule((Action)null, TimeSpan.FromDays(1), ActorScheduleOptions.Default, x => { }))
                  .ShouldThrow<ArgumentNullException>()
                  .And.ParamName.Should().Be("work");
        }

        [Fact]
        public void AsynchronousSchedulerExtensionShouldEmitAnyArgumentOutOfRangeExceptions()
        {
            Expect.That(() => _scheduler.Schedule(async () => { await Task.Delay(10); }, TimeSpan.Zero, ActorScheduleOptions.Default, x => { }))
                  .ShouldThrow<ArgumentOutOfRangeException>()
                  .And.ParamName.Should().Be("interval");
        }

        [Fact]
        public void AsynchronousSchedulerExtensionShouldEmitAnyArgumentNullExceptions()
        {
            Expect.That(() => _scheduler.Schedule((Func<Task>)null, TimeSpan.FromDays(1), ActorScheduleOptions.Default, x => { }))
                  .ShouldThrow<ArgumentNullException>()
                  .And.ParamName.Should().Be("work");
        }

        private void SetUpActor(WorkType workType)
        {
            switch (workType)
            {
                case WorkType.Sync:
                    Mock.Get(_actor)
                        .Setup(x => x.Enqueue(It.IsAny<Action>(), It.IsAny<CancellationToken>(), It.IsAny<ActorEnqueueOptions>()))
                        .Returns<Action, CancellationToken?, ActorEnqueueOptions>((x, y, z) =>
                                                                                  {
                                                                                      try
                                                                                      {
                                                                                          x();
                                                                                          return _actorTaskFactory.FromCompleted();
                                                                                      }
                                                                                      catch (Exception exception)
                                                                                      {
                                                                                          return _actorTaskFactory.FromException(exception);
                                                                                      }
                                                                                  });
                    break;
                case WorkType.Async:
                    Mock.Get(_actor)
                        .Setup(x => x.Enqueue(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>(), It.IsAny<ActorEnqueueOptions>()))
                        .Returns<Func<Task>, CancellationToken?, ActorEnqueueOptions>((x, y, z) =>
                                                                                      {
                                                                                          try
                                                                                          {
                                                                                              x().Wait();
                                                                                              return _actorTaskFactory.FromCompleted();
                                                                                          }
                                                                                          catch (AggregateException exception)
                                                                                          {
                                                                                              return _actorTaskFactory.FromException(exception.InnerExceptions.First());
                                                                                          }
                                                                                      });
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }
        }

        private sealed class TestActorTaskFactory : IActorTaskFactory, IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly ConcurrentDictionary<DateTime, TaskCompletionSource<bool>> _delays = new ConcurrentDictionary<DateTime, TaskCompletionSource<bool>>();
            private readonly IActorTaskFactory _impl = new ActorTaskFactory();
            private readonly FixableTimeSource _timeSource;

            private bool _disposed = false;

            public TestActorTaskFactory(FixableTimeSource timeSource)
            {
                _timeSource = timeSource;
                timeSource.OnUpdate += HandleTimeUpdate;
            }

            public Task FromException(Exception exception)
            {
                return _impl.FromException(exception);
            }

            public Task FromCompleted()
            {
                return _impl.FromCompleted();
            }

            public Task Create(Action<object> action, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state)
            {
                return _impl.Create(action, cancellationToken, taskCreationOptions, state);
            }

            public Task<T> Create<T>(Func<object, T> function, CancellationToken cancellationToken, TaskCreationOptions taskCreationOptions, object state)
            {
                return _impl.Create(function, cancellationToken, taskCreationOptions, state);
            }

            public Task CreateDelay(TimeSpan delay, CancellationToken cancellationToken)
            {
                if (delay < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(delay));
                }

                if (delay == TimeSpan.Zero)
                {
                    return FromCompleted();
                }

                return
                    _delays.AddOrUpdate(_timeSource.Current + delay,
                                        _ =>
                                        {
                                            var taskCompletionSource = new TaskCompletionSource<bool>();
                                            cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());
                                            return taskCompletionSource;
                                        },
                                        (x, y) =>
                                        {
                                            if (y.Task.IsCompleted)
                                            {
                                                var taskCompletionSource = new TaskCompletionSource<bool>();
                                                cancellationToken.Register(() => taskCompletionSource.TrySetCanceled());
                                                return taskCompletionSource;
                                            }
                                            else
                                            {
                                                return y;
                                            }
                                        })
                           .Task;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            private void HandleTimeUpdate(DateTime currentTime)
            {
                foreach (var key in _delays.Keys)
                {
                    if (key <= currentTime)
                    {
                        var taskCompletionSource = default(TaskCompletionSource<bool>);
                        _delays.TryRemove(key, out taskCompletionSource);
                        taskCompletionSource.TrySetResult(true);
                    }
                }
            }

            private void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _cancellationTokenSource.Cancel();
                    }

                    _disposed = true;
                }
            }
        }
    }
}
