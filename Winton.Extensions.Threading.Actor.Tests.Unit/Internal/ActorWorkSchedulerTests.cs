// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
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
        private readonly IActorWorkScheduler _scheduler;

        public ActorWorkSchedulerTests()
        {
            _actor = Mock.Of<IActor>();
            _scheduler = new ActorWorkScheduler(_actor, new ActorTaskFactory());
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToScheduleWorkToRepeatAtAFixedInterval(WorkType workType)
        {
            SetUpActor(workType);

            var expectedInterval = TimeSpan.FromMilliseconds(100);
            var times = new List<DateTime>();
            var sampleSize = 5;

            Action adder =
                () =>
                {
                    if (times.Count < sampleSize)
                    {
                        times.Add(DateTime.UtcNow);
                    }
                };

            // First scheduled call to add should not be immediate so mark start ...
            adder();

            switch (workType)
            {
                case WorkType.Sync:
                {
                    _scheduler.Schedule(adder, expectedInterval);
                }
                    break;
                case WorkType.Async:
                {
                    _scheduler.Schedule(async () =>
                                        {
                                            await Task.Yield();
                                            adder();
                                        }, expectedInterval);
                }
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            Within.FiveSeconds(() => times.Count.Should().Be(sampleSize));

            var actualIntervals = times.Take(sampleSize - 1).Zip(times.Skip(1), (x, y) => y - x).ToList();

            actualIntervals.Should().OnlyContain(x => Math.Abs((expectedInterval - x).TotalMilliseconds) < 30);
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToScheduleWorkToStartImmediatelyBeforeRepeatingAtIntervals(WorkType workType)
        {
            SetUpActor(workType);
            var expectedInterval = TimeSpan.FromMilliseconds(100);
            var scheduleTime = DateTime.UtcNow;
            DateTime? firstWork = null;


            switch (workType)
            {
                case WorkType.Sync:
                    _scheduler.Schedule(() =>
                                        {
                                            if (!firstWork.HasValue)
                                            {
                                                firstWork = DateTime.UtcNow;
                                            }
                                        }, expectedInterval, ActorScheduleOptions.NoInitialDelay);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            await Task.Yield();
                                            
                                            if (!firstWork.HasValue)
                                            {
                                                firstWork = DateTime.UtcNow;
                                            }
                                        }, expectedInterval, ActorScheduleOptions.NoInitialDelay);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }

            Within.FiveSeconds(() => firstWork.HasValue.Should().BeTrue());

            (firstWork.Value - scheduleTime).Should().BeLessThan(TimeSpan.FromMilliseconds(50));
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

            Within.OneSecond(() => output.Should().Equal(Enumerable.Repeat("one", 1)));

            _scheduler.CancelCurrent();

            Within.OneSecond(() => task.IsCanceled.Should().BeTrue());

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
            SetUpActor(workType1);
            SetUpActor(workType2);

            var output = new List<string>();
            var interval = TimeSpan.FromMilliseconds(100);
            var task1 = default(Task);
            var haveThreeTwos = new TaskCompletionSource<bool>();

            Action<string> adder =
                x =>
                {
                    if (!haveThreeTwos.Task.IsCompleted)
                    {
                        output.Add(x);

                        if (output.Count(y => y == "two") == 3)
                        {
                            haveThreeTwos.SetResult(true);
                        }
                    }
                };

            switch (workType1)
            {
                case WorkType.Sync:
                    task1 = _scheduler.Schedule(() => { adder("one"); }, interval);
                    break;
                case WorkType.Async:
                    task1 = _scheduler.Schedule(async () =>
                                               {
                                                   await Task.Yield();
                                                   adder("one");
                                               }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType1}.");
            }

            Within.FiveSeconds(() => output.Count.Should().BeGreaterOrEqualTo(1));

            switch (workType2)
            {
                case WorkType.Sync:
                    _scheduler.Schedule(() => { adder("two"); }, interval);
                    break;
                case WorkType.Async:
                    _scheduler.Schedule(async () =>
                                        {
                                            adder("two");
                                            await Task.Yield();
                                        }, interval);
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType2}.");
            }

            haveThreeTwos.Task.AwaitingShouldCompleteIn(TimeSpan.FromSeconds(5));

            var firstTwoIndex = output.IndexOf("two");

            output.Skip(firstTwoIndex).Should().OnlyContain(x => x == "two");

            task1.IsCanceled.Should().BeTrue();
        }

        [Theory]
        [InlineData(WorkType.Async)]
        [InlineData(WorkType.Sync)]
        public void ShouldBeAbleToConfigureScheduleToRescheduleInCaseOfUnexpectedErrorButNotCancellation(WorkType workType)
        {
            SetUpActor(workType);
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

            Within.FiveSeconds(() => times.Should().HaveCount(4));

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
            var interval = TimeSpan.FromMilliseconds(100);
            var times = new List<DateTime>();
            var task = default(Task);

            switch (workType)
            {
                case WorkType.Sync:
                {
                    task = _scheduler.Schedule(() =>
                                               {
                                                   times.Add(DateTime.UtcNow);

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
                                                   times.Add(DateTime.UtcNow);
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

            Expect.That(async () => await task).ShouldThrow<Exception>().WithMessage("Pah!");

            // The schedule should have been cancelled so expect the times list to not be added to
            For.OneSecond(() => times.Should().HaveCount(3));
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
                                                                                          return Task.CompletedTask;
                                                                                      }
                                                                                      catch (Exception exception)
                                                                                      {
                                                                                          return Task.FromException(exception);
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
                                                                                              return Task.CompletedTask;
                                                                                          }
                                                                                          catch (AggregateException exception)
                                                                                          {
                                                                                              return Task.FromException(exception.InnerExceptions.First());
                                                                                          }
                                                                                      });
                    break;
                default:
                    throw new Exception($"Unhandled test case {workType}.");
            }
        }
    }
}
