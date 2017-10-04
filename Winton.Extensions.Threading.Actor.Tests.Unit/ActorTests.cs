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

namespace Winton.Extensions.Threading.Actor.Tests.Unit
{
    public sealed class ActorTests : IDisposable
    {
        public enum ResumeTestCase
        {
            AwaitOnSecondActor,
            AwaitOnTaskFactoryScheduledTask
        }

        public enum StopTaskCancellationTestCase
        {
            CancelPriorToInvoke,
            CancelDuringWork
        }

        public enum StopWorkOutcome
        {
            Completes,
            Faults
        }

        private readonly List<IActor> _createdActors = new List<IActor>();
        private readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(10);

        public void Dispose()
        {
            var closeErrors = new List<Exception>();

            foreach (var actor in _createdActors)
            {
                try
                {
                    actor.Stop().Wait(_waitTimeout);
                }
                catch (Exception exception)
                {
                    closeErrors.Add(exception);
                }
            }

            _createdActors.Clear();

            if (closeErrors.Any())
            {
                Console.WriteLine($"One or more errors occurred whilst stopping the test actor(s):\n{string.Join("\n", closeErrors)}");
            }
        }

        [Fact]
        public void ShouldBeAbleToEnqueueBasicNonVoidTaskAndAwaitItsReturn()
        {
            var actor = CreateActor();
            actor.Enqueue(() => true).AwaitingShouldCompleteIn(_waitTimeout).And.Should().BeTrue();
        }

        [Fact]
        public async Task ShouldBeAbleToEnqueueBasicVoidTaskAndAwaitItsReturn()
        {
            var actor = CreateActor();
            var ran = false;

            await actor.Enqueue(() => { ran = true; });
            ran.Should().BeTrue();
        }

        [Fact]
        public void ShouldRunMultipleNonVoidTasksInTheOrderTheyAreEnqueued()
        {
            var actor = CreateActor();
            var output = new List<int>();
            var tasks =
                Enumerable.Range(1, 50)
                          .Select(x => actor.Enqueue(() =>
                                                     {
                                                         // Without this the test will usually pass even
                                                         // with a dodgy implementation
                                                         Thread.Sleep(1);
                                                         return x;
                                                     })
                                            .ContinueWith(y => output.Add(y.Result), TaskContinuationOptions.ExecuteSynchronously))
                          .ToArray();

            Task.WhenAll(tasks).AwaitingShouldCompleteIn(_waitTimeout);

            output.Should().Equal(Enumerable.Range(1, 50));
        }

        [Fact]
        public void ShouldRunMultipleVoidTasksInTheOrderTheyAreEnqueued()
        {
            var actor = CreateActor();
            var output = new List<int>();
            var tasks =
                Enumerable.Range(1, 50)
                          .Select(x => actor.Enqueue(() =>
                                                     {
                                                         // Without this the test will usually pass even
                                                         // with a dodgy implementation
                                                         Thread.Sleep(1);
                                                         output.Add(x);
                                                     }))
                          .ToArray();

            Task.WhenAll(tasks).AwaitingShouldCompleteIn(_waitTimeout);

            output.Should().Equal(Enumerable.Range(1, 50));
        }

        [Fact]
        public void ShouldHideSchedulerFromPotentialChildTasksOfBasicNonVoidTask()
        {
            var actor = CreateActor();
            var task = actor.Enqueue(() => TaskScheduler.Current);

            task.AwaitingShouldCompleteIn(_waitTimeout).And.Should().Be(TaskScheduler.Default);
        }

        [Fact]
        public void ShouldHideSchedulerFromPotentialChildTasksOfBasicVoidTask()
        {
            var actor = CreateActor();
            var taskScheduler = default(TaskScheduler);

            actor.Enqueue(() => { taskScheduler = TaskScheduler.Current; }).AwaitingShouldCompleteIn(_waitTimeout);

            taskScheduler.Should().Be(TaskScheduler.Default);
        }

        [Theory]
        [InlineData(ResumeTestCase.AwaitOnTaskFactoryScheduledTask)]
        [InlineData(ResumeTestCase.AwaitOnSecondActor)]
        public void ShouldBeAbleToResumeAsyncNonVoidTask(ResumeTestCase testCase)
        {
            var actor1 = CreateActor();
            var actor2 = CreateActor();

            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "PreActor2Call",
                    "PreTrigger",
                    "PostTrigger",
                    "Slept",
                    "PostActor2Call"
                };

            var trigger = new TaskCompletionSource<bool>();
            var suspendWork = default(Func<Task<int>>);
            var actor1TaskSchedulers = new List<TaskScheduler>();
            var actor1CurrentActorIds = new List<ActorId>();
            var nonActor1CurrentActorIds = new List<ActorId>();

            switch (testCase)
            {
                case ResumeTestCase.AwaitOnSecondActor:
                    suspendWork = () => actor2.Enqueue(() =>
                                                       {
                                                           nonActor1CurrentActorIds.Add(Actor.CurrentId);
                                                           ThrowIfWaitTimesOut(trigger.Task);
                                                           return 345;
                                                       });
                    break;
                case ResumeTestCase.AwaitOnTaskFactoryScheduledTask:
                    suspendWork = () => Task.Factory.StartNew(() =>
                                                              {
                                                                  nonActor1CurrentActorIds.Add(Actor.CurrentId);
                                                                  ThrowIfWaitTimesOut(trigger.Task);
                                                                  return 345;
                                                              });
                    break;
                default:
                    throw new Exception($"Unhandled test case {testCase}.");
            }

            var task1 =
                actor1.Enqueue(async () =>
                               {
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                                   stageOrder.Add("PreActor2Call");
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   var value = await suspendWork();
                                   stageOrder.Add("PostActor2Call");
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                                   return value * 37;
                               });

            var task2 =
                actor1.Enqueue(() =>
                               {
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   stageOrder.Add("PreTrigger");
                                   trigger.SetResult(true);
                                   stageOrder.Add("PostTrigger");
                                   Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                   stageOrder.Add("Slept");
                               });

            Task.WhenAll(task1, task2).AwaitingShouldCompleteIn(_waitTimeout);

            actor1TaskSchedulers.Should().NotBeEmpty().And.OnlyContain(x => ReferenceEquals(x, TaskScheduler.Default));
            stageOrder.Should().Equal(expectedStageOrder);
            actor1CurrentActorIds.Should().NotBeEmpty().And.OnlyContain(x => x == actor1.Id);
            nonActor1CurrentActorIds.Should().NotBeEmpty().And.OnlyContain(x => x != actor1.Id);
            task1.Result.Should().Be(37 * 345);
        }

        [Theory]
        [InlineData(ResumeTestCase.AwaitOnTaskFactoryScheduledTask)]
        [InlineData(ResumeTestCase.AwaitOnSecondActor)]
        public void ShouldBeAbleToResumeAsyncVoidTask(ResumeTestCase testCase)
        {
            var actor1 = CreateActor();
            var actor2 = CreateActor();

            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "PreActor2Call",
                    "PreTrigger",
                    "PostTrigger",
                    "Slept",
                    "PostActor2Call",
                    "Result=345"
                };

            var trigger = new TaskCompletionSource<bool>();
            var suspendWork = default(Func<Task<int>>);
            var actor1TaskSchedulers = new List<TaskScheduler>();
            var actor1CurrentActorIds = new List<ActorId>();
            var nonActor1CurrentActorIds = new List<ActorId>();

            switch (testCase)
            {
                case ResumeTestCase.AwaitOnSecondActor:
                    suspendWork = () => actor2.Enqueue(() =>
                                                       {
                                                           nonActor1CurrentActorIds.Add(Actor.CurrentId);
                                                           ThrowIfWaitTimesOut(trigger.Task);
                                                           return 345;
                                                       });
                    break;
                case ResumeTestCase.AwaitOnTaskFactoryScheduledTask:
                    suspendWork = () => Task.Factory.StartNew(() =>
                                                              {
                                                                  nonActor1CurrentActorIds.Add(Actor.CurrentId);
                                                                  ThrowIfWaitTimesOut(trigger.Task);
                                                                  return 345;
                                                              });
                    break;
                default:
                    throw new Exception($"Unhandled test case {testCase}.");
            }

            var task1 =
                actor1.Enqueue(async () =>
                               {
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   stageOrder.Add("PreActor2Call");
                                   var value = await suspendWork();
                                   stageOrder.Add("PostActor2Call");
                                   Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                   stageOrder.Add($"Result={value}");
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                               });

            var task2 =
                actor1.Enqueue(() =>
                               {
                                   actor1CurrentActorIds.Add(Actor.CurrentId);
                                   actor1TaskSchedulers.Add(TaskScheduler.Current);
                                   stageOrder.Add("PreTrigger");
                                   trigger.SetResult(true);
                                   stageOrder.Add("PostTrigger");
                                   Thread.Sleep(TimeSpan.FromSeconds(0.5));
                                   stageOrder.Add("Slept");
                               });

            Task.WhenAll(task1, task2).AwaitingShouldCompleteIn(_waitTimeout);

            actor1TaskSchedulers.Should().NotBeEmpty().And.OnlyContain(x => ReferenceEquals(x, TaskScheduler.Default));
            stageOrder.Should().Equal(expectedStageOrder);
            actor1CurrentActorIds.Should().NotBeEmpty().And.OnlyContain(x => x == actor1.Id);
            nonActor1CurrentActorIds.Should().NotBeEmpty().And.OnlyContain(x => x != actor1.Id);
        }

        [Theory]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, TaskCreationOptions.LongRunning)]
        [InlineData(ActorEnqueueOptions.Default, TaskCreationOptions.None)]
        public void ShouldScheduleTaskAsLongRunningIfRequested(ActorEnqueueOptions enqueueOptions, TaskCreationOptions expectedTaskCreationOptions)
        {
            expectedTaskCreationOptions |= TaskCreationOptions.HideScheduler;

            var taskFactory = SetUpTaskFactory();
            var actor = new Actor(taskFactory);

            actor.Start();
            actor.Enqueue(() => { }, enqueueOptions);
            actor.Enqueue(() => 676, enqueueOptions);
            actor.Enqueue(async () => await Task.Delay(10000), enqueueOptions);
            actor.Enqueue(async () =>
                          {
                              await Task.Delay(10000);
                              return "moose";
                          }, enqueueOptions);

            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Action<object>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Func<object, int>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Func<object, Task>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Func<object, Task<string>>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ShouldBeAbleToSpecifyWorkToRunAtStartUpWhichIsGuaranteedToBeTheFirstThingRun()
        {
            var numbers = new List<int>();
            var actor = CreateActor(x => x.StartWork = new ActorStartWork(() => { numbers.Add(1); }), ActorCreateOptions.None);

            _createdActors.Add(actor);

            actor.Enqueue(() => numbers.Add(2));

            var task = actor.Enqueue(() => numbers.Add(3));
            var startTask = actor.Start();

            startTask.AwaitingShouldCompleteIn(_waitTimeout);
            task.AwaitingShouldCompleteIn(_waitTimeout);

            numbers.Should().Equal(Enumerable.Range(1, 3));
        }

        [Fact]
        public void ShouldNotEnqueueAnyMoreWorkAfterAskedToStop()
        {
            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "Start",
                    "Work1",
                    "Work2",
                    "Stop"
                };
            var actor = CreateActor(x =>
                                    {
                                        x.StartWork = new ActorStartWork(() => stageOrder.Add("Start"));
                                        x.StopWork = new ActorStopWork(() => stageOrder.Add("Stop"));
                                    },
                                    ActorCreateOptions.None);

            actor.Enqueue(() => stageOrder.Add("Work1"));
            actor.Enqueue(() => stageOrder.Add("Work2"));

            actor.Start().AwaitingShouldCompleteIn(_waitTimeout);

            var stopTask = actor.Stop();
            var lateWork = actor.Enqueue(() => stageOrder.Add("Work3"));

            MarkAlreadyStopped();

            ShouldBeCancelled(lateWork);
            stopTask.AwaitingShouldCompleteIn(_waitTimeout);

            stageOrder.Should().Equal(expectedStageOrder);
        }

        [Fact]
        public async Task ShouldCompleteStoppedTaskWhenStopCompleted()
        {
            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "Start",
                    "Stop",
                    "Stopped"
                };
            var actor = CreateActor(x =>
                                    {
                                        x.StartWork = new ActorStartWork(() => stageOrder.Add("Start"));
                                        x.StopWork = new ActorStopWork(
                                            () =>
                                            {
                                                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                                                stageOrder.Add("Stop");
                                            });
                                    },
                                    ActorCreateOptions.None);

            await actor.Start();

            var stopTask = actor.Stop();

            await actor.StoppedTask;
            stageOrder.Add("Stopped");

            await stopTask;

            stageOrder.Should().Equal(expectedStageOrder);
        }

        [Theory]
        [InlineData(ResumeTestCase.AwaitOnTaskFactoryScheduledTask, StopWorkOutcome.Completes)]
        [InlineData(ResumeTestCase.AwaitOnTaskFactoryScheduledTask, StopWorkOutcome.Faults)]
        [InlineData(ResumeTestCase.AwaitOnSecondActor, StopWorkOutcome.Completes)]
        [InlineData(ResumeTestCase.AwaitOnSecondActor, StopWorkOutcome.Faults)]
        public void ShouldNotBeAbleToResumeWorkAfterStop(ResumeTestCase resumeTestCase, StopWorkOutcome stopWorkOutcome)
        {
            var actor1 = CreateActor(x => x.StopWork = new ActorStopWork(() =>
                                                                         {
                                                                             if (stopWorkOutcome == StopWorkOutcome.Faults)
                                                                             {
                                                                                 throw new InvalidOperationException("Never meant to be");
                                                                             }
                                                                         }));
            var actor2 = CreateActor();
            var pretrigger = new TaskCompletionSource<bool>();
            var trigger = new TaskCompletionSource<bool>();
            var suspendWork = default(Func<Task<int>>);
            var stages = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "PreSuspend",
                    "PreTriggerWait",
                    "PostTriggerWait"
                };

            switch (resumeTestCase)
            {
                case ResumeTestCase.AwaitOnSecondActor:
                    suspendWork = () => actor2.Enqueue(() =>
                                                       {
                                                           stages.Add("PreTriggerWait");
                                                           pretrigger.SetResult(true);
                                                           ThrowIfWaitTimesOut(trigger.Task);
                                                           stages.Add("PostTriggerWait");
                                                           return 345;
                                                       });
                    break;
                case ResumeTestCase.AwaitOnTaskFactoryScheduledTask:
                    suspendWork = () => new TaskFactory(TaskScheduler.Default).StartNew(() =>
                                                                                        {
                                                                                            stages.Add("PreTriggerWait");
                                                                                            pretrigger.SetResult(true);
                                                                                            ThrowIfWaitTimesOut(trigger.Task);
                                                                                            stages.Add("PostTriggerWait");
                                                                                            return 345;
                                                                                        });
                    break;
                default:
                    throw new Exception($"Unhandled test case {resumeTestCase}.");
            }

            //var task1 =
            actor1.Enqueue(async () =>
                           {
                               stages.Add("PreSuspend");
                               var value = await suspendWork();
                               stages.Add("PostSuspend");
                               return value * 37;
                           });

            pretrigger.Task.AwaitingShouldCompleteIn(_waitTimeout);
            stages.Should().Equal(expectedStageOrder.Take(2));

            var stopTask = actor1.Stop();
            MarkAlreadyStopped();
            trigger.SetResult(true);

            switch (stopWorkOutcome)
            {
                case StopWorkOutcome.Completes:
                    stopTask.AwaitingShouldCompleteIn(_waitTimeout);
                    break;
                case StopWorkOutcome.Faults:
                    ((Func<Task>)(async () => await stopTask)).ShouldThrow<InvalidOperationException>().WithMessage("Never meant to be");
                    break;
                default:
                    throw new Exception($"Unhandled test case {stopWorkOutcome}.");
            }

            Within.OneSecond(() => stages.Should().Equal(expectedStageOrder));

            // The below would be nice but has proved intractable to achieve.  It seems the async/await syntatic sugar
            // fails to pass on the AsyncState from the initial task so that the associated CancellationTokenSource is
            // not preserved.
            //AssertCancelled(task1);
        }

        [Fact]
        public void ShouldNotProcessAnyWorkUntilAsyncStartUpWorkIsComplete()
        {
            var trigger = new TaskCompletionSource<object>();
            var numbers = new List<int>();
            var actor = CreateActor(x => x.StartWork = new ActorStartWork(async () =>
                                                                          {
                                                                              await trigger.Task;
                                                                              numbers.Add(1);
                                                                          }),
                                    ActorCreateOptions.None);

            _createdActors.Add(actor);

            actor.Enqueue(() => numbers.Add(2));

            var task = actor.Enqueue(() => numbers.Add(3));
            var startTask = actor.Start();

            startTask.IsCompleted.Should().BeFalse();

            trigger.SetResult(null);

            startTask.AwaitingShouldCompleteIn(_waitTimeout);
            task.AwaitingShouldCompleteIn(_waitTimeout);

            numbers.Should().Equal(Enumerable.Range(1, 3));
        }

        [Fact]
        public void ShouldNotStartProcessingIfStopAlreadyCalled()
        {
            var stageOrder = new List<string>();
            var actor = CreateActor(x =>
                                    {
                                        x.StartWork = new ActorStartWork(() => stageOrder.Add("Start"));
                                        x.StopWork = new ActorStopWork(() => stageOrder.Add("Stop"));
                                    },
                                    ActorCreateOptions.None);

            var shouldBeCancelled =
                new List<Task>
                {
                    actor.Enqueue(() => stageOrder.Add("Work1")),
                    actor.Enqueue(() => stageOrder.Add("Work2"))
                };

            var stopTask = actor.Stop();
            var startTask = actor.Start();

            MarkAlreadyStopped();

            startTask.AwaitingShouldCompleteIn(_waitTimeout);
            stopTask.AwaitingShouldCompleteIn(_waitTimeout);

            stageOrder.Should().BeEmpty();

            ShouldBeCancelled(shouldBeCancelled[0]);
            ShouldBeCancelled(shouldBeCancelled[1]);
        }

        [Fact]
        public void ShouldProcessAlreadyQueuedWorkBeforeSignallingStoppedWhenAskedToStop()
        {
            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "Start",
                    "Work1",
                    "Work2",
                    "Stop"
                };
            var actor = CreateActor(x =>
                                    {
                                        x.StartWork = new ActorStartWork(() => stageOrder.Add("Start"));
                                        x.StopWork = new ActorStopWork(() => stageOrder.Add("Stop"));
                                    },
                                    ActorCreateOptions.None);

            actor.Enqueue(() => stageOrder.Add("Work1"));
            actor.Enqueue(() => stageOrder.Add("Work2"));

            actor.Start().AwaitingShouldCompleteIn(_waitTimeout);
            actor.Stop().AwaitingShouldCompleteIn(_waitTimeout);

            stageOrder.Should().Equal(expectedStageOrder);
        }

        [Fact]
        public void ShouldProcessAlreadyQueuedWorkBeforeSignallingStoppedWhenAskedToStopWhilstStartWorkStillBeingProcessed()
        {
            var stageOrder = new List<string>();
            var expectedStageOrder =
                new List<string>
                {
                    "Start",
                    "Work1",
                    "Work2",
                    "Stop"
                };
            var actor = CreateActor(x =>
                                    {
                                        x.StartWork = new ActorStartWork(() => stageOrder.Add("Start"));
                                        x.StopWork = new ActorStopWork(() => stageOrder.Add("Stop"));
                                    },
                                    ActorCreateOptions.None);

            actor.Enqueue(() => stageOrder.Add("Work1"));
            actor.Enqueue(() => stageOrder.Add("Work2"));

            var startTask = actor.Start();
            var stopTask = actor.Stop();

            startTask.AwaitingShouldCompleteIn(_waitTimeout);
            stopTask.AwaitingShouldCompleteIn(_waitTimeout);

            stageOrder.Should().Equal(expectedStageOrder);
        }

        [Theory]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, TaskCreationOptions.LongRunning)]
        [InlineData(ActorEnqueueOptions.Default, TaskCreationOptions.None)]
        public void ShouldScheduleStartTaskAsLongRunningIfRequested(ActorEnqueueOptions startOptions, TaskCreationOptions expectedTaskCreationOptions)
        {
            expectedTaskCreationOptions |= TaskCreationOptions.HideScheduler;

            var taskFactory = SetUpTaskFactory();
            var actor = new Actor(taskFactory)
                        {
                            StartWork = new ActorStartWork(() => { })
                                        {
                                            Options = startOptions
                                        }
                        };

            actor.Start();

            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Action<object>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
        }

        [Theory]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, TaskCreationOptions.LongRunning)]
        [InlineData(ActorEnqueueOptions.Default, TaskCreationOptions.None)]
        public void ShouldScheduleStopTaskAsLongRunningIfRequested(ActorEnqueueOptions stopOptions, TaskCreationOptions expectedTaskCreationOptions)
        {
            expectedTaskCreationOptions |= TaskCreationOptions.HideScheduler;
            var taskFactory = SetUpTaskFactory();

            var actor = new Actor(taskFactory)
                        {
                            StopWork = new ActorStopWork(() => { })
                                       {
                                           Options = stopOptions
                                       }
                        };

            actor.Start();
            actor.Stop();

            Expect.That(() => Mock.Get(taskFactory).Verify(x => x.Create(It.IsAny<Action<object>>(), It.IsAny<CancellationToken>(), expectedTaskCreationOptions, It.IsAny<object>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ShouldSilentlyCompleteCallsToStartAfterTheFirstButOnlyOnceActorHasFinallyStarted()
        {
            var barrier = new TaskCompletionSource<bool>();
            var attempts = 0;
            var actor = CreateActor(x => x.StartWork = new ActorStartWork(() =>
                                                                          {
                                                                              Interlocked.Increment(ref attempts);
                                                                              ThrowIfWaitTimesOut(barrier.Task);
                                                                          }),
                                    ActorCreateOptions.None);

            var task1 = actor.Start();
            var task2 = actor.Start();

            task2.Wait(TimeSpan.FromSeconds(1)).Should().BeFalse("Should not have already completed.");
            barrier.SetResult(true);
            Task.WhenAll(task1, task2).AwaitingShouldCompleteIn(_waitTimeout);
            attempts.Should().Be(1);
        }

        [Fact]
        public void ShouldSilentlyCompleteCallsToStopAfterTheFirstButOnlyOnceActorHasFinallyStopped()
        {
            var barrier = new TaskCompletionSource<bool>();
            var attempts = 0;
            var actor = CreateActor(x => x.StopWork = new ActorStopWork(() =>
                                                                        {
                                                                            Interlocked.Increment(ref attempts);
                                                                            ThrowIfWaitTimesOut(barrier.Task);
                                                                        }));

            var task1 = actor.Stop();
            MarkAlreadyStopped();
            var task2 = actor.Stop();

            task2.Wait(TimeSpan.FromSeconds(1)).Should().BeFalse("Should not have already completed.");
            barrier.SetResult(true);
            Task.WhenAll(task1, task2).AwaitingShouldCompleteIn(_waitTimeout);
            attempts.Should().Be(1);
        }

        [Fact]
        public void ShouldOnlyBeAbleToSpecifyStartWorkOnce()
        {
            var actor = CreateActor(ActorCreateOptions.None);

            actor.StartWork = new ActorStartWork(() => { });

            Action action = () => actor.StartWork = new ActorStartWork(() => { });
            action.ShouldThrow<InvalidOperationException>().WithMessage("Start work already specified.");
        }

        [Fact]
        public void ShouldOnlyBeAbleToSpecifyStopWorkOnce()
        {
            var actor = CreateActor(ActorCreateOptions.None);

            actor.StopWork = new ActorStopWork(() => { });

            Action action = () => actor.StopWork = new ActorStopWork(() => { });
            action.ShouldThrow<InvalidOperationException>().WithMessage("Stop work already specified.");
        }

        [Fact]
        public void ShouldNotBeAbleToSpecifyStartWorkOnceActorStarted()
        {
            var actor = CreateActor();
            Action action = () => actor.StartWork = new ActorStartWork(() => { });
            action.ShouldThrow<InvalidOperationException>().WithMessage("Start work cannot be specified after starting an actor.");
        }

        [Fact]
        public void ShouldNotBeAbleToSpecifyStopWorkOnceActorStarted()
        {
            var actor = CreateActor();
            Action action = () => actor.StopWork = new ActorStopWork(() => { });
            action.ShouldThrow<InvalidOperationException>()
                  .WithMessage("Stop work cannot be specified after starting an actor.");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ShouldBeAbleToCancelAnyEnqueuedWork(bool delayStart)
        {
            var actor = CreateActor(delayStart ? ActorCreateOptions.None : ActorCreateOptions.Start);

            var cancellationTokenSource1 = new CancellationTokenSource();
            var cancellationTokenSource2 = new CancellationTokenSource();
            var cancellationTokenSource3 = new CancellationTokenSource();
            var cancellationTokenSource4 = new CancellationTokenSource();

            cancellationTokenSource3.Cancel();

            var task1 = actor.Enqueue(() => { cancellationTokenSource1.Cancel(); });
            var task2 = actor.Enqueue(() =>
                                      {
                                          cancellationTokenSource1.Token.ThrowIfCancellationRequested();
                                          return 28282;
                                      }, cancellationTokenSource1.Token);
            var task3 = actor.Enqueue(() =>
                                      {
                                          cancellationTokenSource2.Cancel();
                                          cancellationTokenSource2.Token.ThrowIfCancellationRequested();
                                      }, cancellationTokenSource2.Token);
            var task4 = actor.Enqueue(() =>
                                      {
                                          cancellationTokenSource4.Cancel();
                                          return 676;
                                      });
            var task5 = actor.Enqueue(async () =>
                                      {
                                          await Task.Delay(100);
                                          cancellationTokenSource3.Token.ThrowIfCancellationRequested();
                                      }, cancellationTokenSource3.Token);
            var task6 = actor.Enqueue(async () => await Task.Delay(100));
            var task7 = actor.Enqueue(async () =>
                                      {
                                          await Task.Delay(100);
                                          cancellationTokenSource4.Token.ThrowIfCancellationRequested();
                                          return "moose";
                                      }, cancellationTokenSource4.Token);
            var task8 = actor.Enqueue(async () =>
                                      {
                                          await Task.Delay(100);
                                          return "moose";
                                      });

            if (delayStart)
            {
                actor.Start();
            }

            task1.AwaitingShouldCompleteIn(_waitTimeout);
            task4.AwaitingShouldCompleteIn(_waitTimeout);
            task6.AwaitingShouldCompleteIn(_waitTimeout);
            task8.AwaitingShouldCompleteIn(_waitTimeout);
            ShouldBeCancelled(task2);
            ShouldBeCancelled(task3);
            ShouldBeCancelled(task5);
            ShouldBeCancelled(task7);
        }

        [Theory]
        [InlineData(StopTaskCancellationTestCase.CancelDuringWork)]
        [InlineData(StopTaskCancellationTestCase.CancelPriorToInvoke)]
        public void ShouldBeAbleToCancelStopWorkButNotTermination(StopTaskCancellationTestCase testCase)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var startedStopWorkFlag = new TaskCompletionSource<bool>();
            var actor = CreateActor(x => x.StopWork = new ActorStopWork(() =>
                                                                        {
                                                                            startedStopWorkFlag.SetResult(true);
                                                                            Task.Delay(TimeSpan.FromMinutes(1)).Wait(cancellationTokenSource.Token);
                                                                        },
                                                                        cancellationTokenSource.Token));
            var barrier = new TaskCompletionSource<bool>();
            actor.Enqueue(() => ThrowIfWaitTimesOut(barrier.Task));
            var stopTask = actor.Stop();

            MarkAlreadyStopped();

            switch (testCase)
            {
                case StopTaskCancellationTestCase.CancelPriorToInvoke:
                    cancellationTokenSource.Cancel();
                    barrier.SetResult(true);
                    break;
                case StopTaskCancellationTestCase.CancelDuringWork:
                    barrier.SetResult(true);
                    startedStopWorkFlag.Task.AwaitingShouldCompleteIn(_waitTimeout);
                    cancellationTokenSource.Cancel();
                    break;
                default:
                    throw new Exception($"Unhandled test case {testCase}.");
            }

            ShouldBeCancelled(stopTask);

            if (testCase == StopTaskCancellationTestCase.CancelPriorToInvoke)
            {
                startedStopWorkFlag.Task.Wait(TimeSpan.FromSeconds(1)).Should().BeFalse();
            }

            ShouldBeCancelled(actor.Enqueue(() => { }));
        }

        [Fact]
        public void ShouldNotFailEnqueuingWorkAlreadyCancelled()
        {
            var actor = CreateActor();

            var cancellationTokenSource1 = new CancellationTokenSource();
            var cancellationTokenSource2 = new CancellationTokenSource();
            var cancellationTokenSource3 = new CancellationTokenSource();
            var cancellationTokenSource4 = new CancellationTokenSource();

            cancellationTokenSource1.Cancel();
            cancellationTokenSource2.Cancel();
            cancellationTokenSource3.Cancel();
            cancellationTokenSource4.Cancel();

            var task1 = actor.Enqueue(() => 28282, cancellationTokenSource1.Token);
            var task2 = actor.Enqueue(() => { }, cancellationTokenSource2.Token);
            var task3 = actor.Enqueue(async () => { await Task.Delay(100); }, cancellationTokenSource3.Token);
            var task4 = actor.Enqueue(async () =>
                                      {
                                          await Task.Delay(100);
                                          return "moose";
                                      }, cancellationTokenSource4.Token);

            ShouldBeCancelled(task1);
            ShouldBeCancelled(task2);
            ShouldBeCancelled(task3);
            ShouldBeCancelled(task4);
        }

        [Fact]
        public void ShouldStopActorAndNotProcessAnyAlreadyEnqueuedWorkIfStartWorkCancelled()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var actor = CreateActor(x => x.StartWork = new ActorStartWork(() => { Task.Delay(TimeSpan.FromMinutes(1)).Wait(cancellationTokenSource.Token); }, cancellationTokenSource.Token),
                                    ActorCreateOptions.None);

            var attempts = 0;

            var startTask = actor.Start();
            var task = actor.Enqueue(() => Interlocked.Increment(ref attempts));

            MarkAlreadyStopped();
            cancellationTokenSource.Cancel();

            ShouldBeCancelled(startTask);
            ShouldBeCancelled(task);
            ShouldBeCancelled(actor.Enqueue(() => { }));
            attempts.Should().Be(0);
        }

        [Theory]
        [InlineData(ActorEnqueueOptions.Default, ActorEnqueueOptions.Default)]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, ActorEnqueueOptions.Default)]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, ActorEnqueueOptions.WorkIsLongRunning)]
        [InlineData(ActorEnqueueOptions.Default, ActorEnqueueOptions.WorkIsLongRunning)]
        public async Task ShouldBeAbleToPauseActorUntilResumeFromAwait(ActorEnqueueOptions awaiterOptions, ActorEnqueueOptions otherOptions)
        {
            // Do this repeatedly to try to expose race conditions in the pausing logic
            for (var i = 0; i < 1000; i++)
            {
                var actor = new Actor();
                var numbers = new List<int>();

                await actor.Start();

                var tasks = new Task[3];

                tasks[0] =
                    actor.Enqueue(
                        async () =>
                        {
                            numbers.Add(i);
                            var task = Task.Run(() => numbers.Add(i + 1));
                            await task.WhileActorPaused();
                            numbers.Add(i + 2);
                        }, awaiterOptions);
                tasks[1] = actor.Enqueue(() => numbers.Add(i + 3), otherOptions);
                tasks[2] = actor.Enqueue(() => numbers.Add(i + 4), otherOptions);

                foreach (var task in tasks)
                {
                    await task;
                }

                await actor.Stop();

                numbers.Should().Equal(i, i + 1, i + 2, i + 3, i + 4);
            }
        }

        [Theory]
        [InlineData(ActorEnqueueOptions.Default, ActorEnqueueOptions.Default)]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, ActorEnqueueOptions.Default)]
        [InlineData(ActorEnqueueOptions.WorkIsLongRunning, ActorEnqueueOptions.WorkIsLongRunning)]
        [InlineData(ActorEnqueueOptions.Default, ActorEnqueueOptions.WorkIsLongRunning)]
        public async Task ShouldBeAbleToPauseActorUntilResumeFromAwaitReturningData(ActorEnqueueOptions awaiterOptions, ActorEnqueueOptions otherOptions)
        {
            // Do this repeatedly to try to expose race conditions in the pausing logic
            for (var i = 0; i < 1000; i++)
            {
                var actor = CreateActor();
                var numbers = new List<int>();

                await actor.Start();

                var tasks = new Task[4];

                tasks[0] = 
                    actor.Enqueue(
                    async () =>
                    {
                        numbers.Add(i);

                        var next = await Task.Run(() => i + 1).WhileActorPaused();

                        numbers.Add(next);
                    }, awaiterOptions);
                tasks[1] = actor.Enqueue(() => numbers.Add(i + 2), otherOptions);
                tasks[2] = actor.Enqueue(() => numbers.Add(i + 3), otherOptions);
                tasks[3] = actor.Enqueue(() => numbers.Add(i + 4), otherOptions);

                foreach (var task in tasks)
                {
                    await task;
                }

                await actor.Stop();

                numbers.Should().Equal(i, i + 1, i + 2, i + 3, i + 4);
            }
        }

        [Flags]
        private enum ActorCreateOptions
        {
            None = 0,
            Start = 0x01,
            Default = Start
        }

        private IActor CreateActor(ActorCreateOptions options = ActorCreateOptions.Default)
        {
            return CreateActor(_ => { }, options);
        }

        private IActor CreateActor(Action<IActor> setup, ActorCreateOptions options = ActorCreateOptions.Default, IActorTaskFactory actorTaskFactory = null)
        {
            var actor = new Actor(actorTaskFactory);

            setup(actor);

            _createdActors.Add(actor);

            if (options.HasFlag(ActorCreateOptions.Start))
            {
                actor.Start().AwaitingShouldCompleteIn(_waitTimeout);
            }

            return actor;
        }

        private void ShouldBeCancelled(Task task)
        {
            Expect.That(async () => await task).ShouldThrow<TaskCanceledException>();
        }

        private void MarkAlreadyStopped()
        {
            _createdActors.Clear(); // To avoid errors in TearDown when it fails to stop a second time.
        }

        private void ThrowIfWaitTimesOut(Task task)
        {
            if (!task.Wait(_waitTimeout))
            {
                throw new TimeoutException();
            }
        }

        private static IActorTaskFactory SetUpTaskFactory()
        {
            var realTaskFactory = new ActorTaskFactory();
            var taskFactory = Mock.Of<IActorTaskFactory>();

            Mock.Get(taskFactory)
                .Setup(x => x.Create(It.IsAny<Action<object>>(), It.IsAny<CancellationToken>(), It.IsAny<TaskCreationOptions>(), It.IsAny<object>()))
                .Returns<Action<object>, CancellationToken, TaskCreationOptions, object>((action, cancellationToken, taskCreationOptions, state) => realTaskFactory.Create(action, cancellationToken, taskCreationOptions, state));
            Mock.Get(taskFactory)
                .Setup(x => x.Create(It.IsAny<Func<object, int>>(), It.IsAny<CancellationToken>(), It.IsAny<TaskCreationOptions>(), It.IsAny<object>()))
                .Returns<Func<object, int>, CancellationToken, TaskCreationOptions, object>((function, cancellationToken, taskCreationOptions, state) => realTaskFactory.Create(function, cancellationToken, taskCreationOptions, state));
            Mock.Get(taskFactory)
                .Setup(x => x.Create(It.IsAny<Func<object, Task>>(), It.IsAny<CancellationToken>(), It.IsAny<TaskCreationOptions>(), It.IsAny<object>()))
                .Returns<Func<object, Task>, CancellationToken, TaskCreationOptions, object>((function, cancellationToken, taskCreationOptions, state) => realTaskFactory.Create(function, cancellationToken, taskCreationOptions, state));
            Mock.Get(taskFactory)
                .Setup(x => x.Create(It.IsAny<Func<object, Task<string>>>(), It.IsAny<CancellationToken>(), It.IsAny<TaskCreationOptions>(), It.IsAny<object>()))
                .Returns<Func<object, Task<string>>, CancellationToken, TaskCreationOptions, object>((function, cancellationToken, taskCreationOptions, state) => realTaskFactory.Create(function, cancellationToken, taskCreationOptions, state));
            Mock.Get(taskFactory)
                .Setup(x => x.FromCompleted())
                .Returns(realTaskFactory.FromCompleted);
            Mock.Get(taskFactory)
                .Setup(x => x.FromException(It.IsAny<Exception>()))
                .Returns<Exception>(realTaskFactory.FromException);
            Mock.Get(taskFactory)
                .Setup(x => x.CreateDelay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns<TimeSpan, CancellationToken>(Task.Delay);

            return taskFactory;
        }
    }
}
