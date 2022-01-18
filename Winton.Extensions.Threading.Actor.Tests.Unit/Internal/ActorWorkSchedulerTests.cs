// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Winton.Extensions.Threading.Actor.Tests.Utilities;
using Xunit;

namespace Winton.Extensions.Threading.Actor.Tests.Unit.Internal
{
    public sealed class ActorWorkSchedulerTests : IDisposable
    {
        public enum WorkType
        {
            Sync,
            Async
        }

        private readonly IActor _actor;
        private readonly IActorWorkScheduler _scheduler;

        public ActorWorkSchedulerTests()
        {
            _actor = new Actor();
            _actor.Start();
            _scheduler = _actor.CreateWorkScheduler();
        }

        public void Dispose()
        {
            _actor.Stop().Wait();
            _scheduler.CancelCurrent();
        }

        [Theory]
        [InlineData(WorkType.Async, ActorScheduleOptions.Default)]
        [InlineData(WorkType.Async, ActorScheduleOptions.NoInitialDelay)]
        [InlineData(WorkType.Sync, ActorScheduleOptions.Default)]
        [InlineData(WorkType.Sync, ActorScheduleOptions.NoInitialDelay)]
        public void ShouldBeAbleToScheduleWorkToRepeatAtAFixedInterval(WorkType workType, ActorScheduleOptions actorScheduleOptions)
        {
            var barrier = new TaskCompletionSource<bool>();
            var expectedInterval = TimeSpan.FromMilliseconds(100);
            var times = new List<DateTime>();
            var sampleSize = 5;

            void Adder()
            {
                if (times.Count < sampleSize)
                {
                    times.Add(DateTime.UtcNow);
                }

                if (times.Count == sampleSize)
                {
                    // Block here so that we can assess something that's not moving
                    barrier.Task.Wait();
                }
            }

            switch (workType)
            {
                case WorkType.Sync:
                    _scheduler.Schedule((Action)Adder, expectedInterval, actorScheduleOptions);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            await Task.Yield();
                                            Adder();
                                        },
                                        expectedInterval,
                                        actorScheduleOptions);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            Within.FiveSeconds(() => times.Count.Should().Be(sampleSize));

            var actualIntervals = times.Take(sampleSize - 1).Zip(times.Skip(1), (x, y) => y - x).ToList();

            // Use 75ms instead of 100ms to give it a bit of latitude: mainly we just want to make sure there is some delaying going on.
            actualIntervals.Should().OnlyContain(x => x >= TimeSpan.FromMilliseconds(75));

            barrier.SetResult(true);
        }

        [Fact]
        public async Task ShouldBeAbleToSpecifyThatSyncWorkIsLongRunning()
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _scheduler.Schedule(
                () =>
                {
                    try
                    {
                        ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                        taskCompletionSource.TrySetResult(null);
                    }
                    catch (Exception exception)
                    {
                        taskCompletionSource.TrySetException(exception);
                    }
                }, TimeSpan.FromMilliseconds(1000), ActorScheduleOptions.NoInitialDelay | ActorScheduleOptions.WorkIsLongRunning);

            await taskCompletionSource.Awaiting(x => x.Task).Should().NotThrowAsync();
        }

        [Fact]
        public async Task ShouldBeAbleToSpecifyThatAsyncWorkIsLongRunning()
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            _scheduler.Schedule(
                async () =>
                {
                    await Task.Yield();

                    try
                    {
                        ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                        taskCompletionSource.TrySetResult(null);
                    }
                    catch (Exception exception)
                    {
                        taskCompletionSource.TrySetException(exception);
                    }
                }, TimeSpan.FromMilliseconds(1000), ActorScheduleOptions.NoInitialDelay | ActorScheduleOptions.WorkIsLongRunning);

            await taskCompletionSource.Awaiting(x => x.Task).Should().NotThrowAsync();
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToCancelSchedule(WorkType workType)
        {
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

            Within.FiveSeconds(() => output.Should().Equal(Enumerable.Repeat("one", 1)));

            _scheduler.CancelCurrent();

            Within.FiveSeconds(() => task.IsCanceled.Should().BeTrue());

            var marker = output.Count;

            For.OneSecond(() => output.Count.Should().Be(marker));
        }

        [Theory]
        [InlineData(WorkType.Async, WorkType.Async)]
        [InlineData(WorkType.Async, WorkType.Sync)]
        [InlineData(WorkType.Sync, WorkType.Async)]
        [InlineData(WorkType.Sync, WorkType.Sync)]
        public void ASecondCallToScheduleShouldCancelTheWorkPreviouslyScheduled(WorkType workType1, WorkType workType2)
        {
            var output = new List<string>();
            var interval = TimeSpan.FromMilliseconds(100);
            var task1 = default(Task);
            var firstTwoAddedPromise = new TaskCompletionSource<bool>();
            var gotAOneAfterATwoPromise = new TaskCompletionSource<bool>();

            void Adder(string x)
            {
                output.Add(x);

                if (x == "two")
                {
                    firstTwoAddedPromise.TrySetResult(true);
                }
                else if (firstTwoAddedPromise.Task.IsCompleted && x == "one")
                {
                    gotAOneAfterATwoPromise.TrySetResult(true);
                }
            }

            switch (workType1)
            {
                case WorkType.Sync:
                    task1 = _scheduler.Schedule(() => { Adder("one"); }, interval);
                    break;
                case WorkType.Async:
                    task1 = _scheduler.Schedule(async () =>
                                               {
                                                   await Task.Delay(TimeSpan.FromMilliseconds(10));
                                                   Adder("one");
                                               }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType1}.");
            }

            Within.FiveSeconds(() => output.Count.Should().BeGreaterOrEqualTo(1));

            switch (workType2)
            {
                case WorkType.Sync:
                    _scheduler.Schedule(() => { Adder("two"); }, interval);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            Adder("two");
                                            await Task.Yield();
                                        }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType2}.");
            }

            firstTwoAddedPromise.Task.AwaitingShouldCompleteIn(TimeSpan.FromSeconds(5));

            For.OneSecond(() => gotAOneAfterATwoPromise.Task.IsCompleted.Should().BeFalse("The first bit of work is still being scheduled."));

            task1.IsCanceled.Should().BeTrue();
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToConfigureScheduleToRescheduleInCaseOfUnexpectedErrorButNotCancellation(WorkType workType)
        {
            var interval = TimeSpan.FromMilliseconds(100);
            var times = new List<DateTime>();
            var emittedException = default(Exception);
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                    task = _scheduler.Schedule(() =>
                                               {
                                                   times.Add(DateTime.UtcNow);

                                                   if (times.Count == 3)
                                                   {
                                                       throw new InvalidOperationException("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.Default, x => emittedException = x);
                    break;
                case WorkType.Async:
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   times.Add(DateTime.UtcNow);

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

            Within.FiveSeconds(() => times.Count.Should().BeGreaterOrEqualTo(4));

            emittedException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Pah!");

            _scheduler.CancelCurrent();

            Within.FiveSeconds(() => task.IsCanceled.Should().BeTrue());
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public async Task WhenAnUnhandledErrorOccursInTheWorkTheScheduleShouldStopAndEmitTheError(WorkType workType)
        {
            var interval = TimeSpan.FromMilliseconds(100);
            var times = new List<DateTime>();
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                    task = _scheduler.Schedule(() =>
                                               {
                                                   times.Add(DateTime.UtcNow);

                                                   if (times.Count == 3)
                                                   {
                                                       throw new Exception("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.NoInitialDelay);
                    break;
                case WorkType.Async:
                    task = _scheduler.Schedule(async () =>
                                               {
                                                   times.Add(DateTime.UtcNow);
                                                   await Task.Yield();

                                                   if (times.Count == 3)
                                                   {
                                                       throw new Exception("Pah!");
                                                   }
                                               }, interval, ActorScheduleOptions.NoInitialDelay);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            await Expect.That(async () => await task).Should().ThrowAsync<Exception>().WithMessage("Pah!");

            // The schedule should have been cancelled so expect the times list to not be added to
            For.OneSecond(() => times.Should().HaveCount(3));
        }

        [Fact]
        public async Task SynchronousSchedulerExtensionShouldEmitAnyArgumentOutOfRangeExceptions()
        {
            await Expect.That(() => _scheduler.Schedule(() => { }, TimeSpan.Zero, ActorScheduleOptions.Default, x => { }))
                  .Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("interval");
        }

        [Fact]
        public async Task SynchronousSchedulerExtensionShouldEmitAnyArgumentNullExceptions()
        {
            await Expect.That(() => _scheduler.Schedule((Action)null, TimeSpan.FromDays(1), ActorScheduleOptions.Default, x => { }))
                  .Should().ThrowAsync<ArgumentNullException>().WithParameterName("work");
        }

        [Fact]
        public async Task AsynchronousSchedulerExtensionShouldEmitAnyArgumentOutOfRangeExceptions()
        {
            await Expect.That(() => _scheduler.Schedule(async () => { await Task.Delay(10); }, TimeSpan.Zero, ActorScheduleOptions.Default, x => { }))
                  .Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("interval");
        }

        [Fact]
        public async Task AsynchronousSchedulerExtensionShouldEmitAnyArgumentNullExceptions()
        {
            await Expect.That(() => _scheduler.Schedule(null, TimeSpan.FromDays(1), ActorScheduleOptions.Default, x => { }))
                  .Should().ThrowAsync<ArgumentNullException>().WithParameterName("work");
        }
    }
}
