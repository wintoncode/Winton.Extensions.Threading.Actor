// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Winton.Extensions.Threading.Actor.Internal;
using Winton.Extensions.Threading.Actor.Tests.Utilities;
using Xunit;

namespace Winton.Extensions.Threading.Actor.Tests.Unit.Internal
{
    public sealed class ActorTaskSchedulerTests
    {
        public enum LateScheduleType
        {
            WhilstTerminalTaskProcessing,
            AfterTerminalTaskComplete
        }

        private readonly ActorId _actorId = ActorId.NewId();
        private readonly IActorTaskFactory _actorTaskFactory = new ActorTaskFactory();
        private readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(10);
        private readonly IWorkItemQueuer _workItemQueuer;

        private ActorTaskScheduler _scheduler;

        public ActorTaskSchedulerTests()
        {
            _workItemQueuer = Mock.Of<IWorkItemQueuer>();

            Mock.Get(_workItemQueuer)
                .Setup(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()))
                .Callback<Action>(LaunchOnThreadPoolThread);
            Mock.Get(_workItemQueuer)
                .Setup(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()))
                .Callback<Action>(LaunchOnNonThreadPoolThread);

            _scheduler = new ActorTaskScheduler(_actorId, _workItemQueuer, _actorTaskFactory);
        }

        [Fact]
        public void ForLongRunningTasksShouldUseActiveThreadIfNotFromThreadPool()
        {
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.Create(() => { ThrowIfWaitTimesOut(barrier.Task); }, CancellationToken.None, TaskCreationOptions.LongRunning);
            var task2 = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.LongRunning);

            task1.Start(_scheduler);
            task2.Start(_scheduler);
            barrier.SetResult(true);
            ThrowIfWaitTimesOut(task2);

            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Never)).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ForLongRunningTasksShouldUseNonThreadPoolThreadIfActiveThreadFromThreadPool()
        {
            var barrier = new TaskCompletionSource<bool>();
            var shortTask = _actorTaskFactory.Create(() => { ThrowIfWaitTimesOut(barrier.Task); }, CancellationToken.None, TaskCreationOptions.None);
            var longTask = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.LongRunning);

            shortTask.Start(_scheduler);
            longTask.Start(_scheduler);
            barrier.SetResult(true);

            longTask.AwaitingShouldCompleteIn(_waitTimeout);

            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ForLongRunningTasksShouldUseNonThreadPoolThreadIfNoActiveThread()
        {
            var task = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.LongRunning);
            task.Start(_scheduler);
            ThrowIfWaitTimesOut(task);
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Never)).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ForLongRunningTasksThreadShouldBeMarkedAsActorThreadDuringExecution()
        {
            Actor.CurrentId.Should().Be(ActorId.None);
            var actorIdOnWorkThread = new ActorId();
            var task = _actorTaskFactory.Create(() => { actorIdOnWorkThread = Actor.CurrentId; }, CancellationToken.None, TaskCreationOptions.None);
            task.Start(_scheduler);
            ThrowIfWaitTimesOut(task);
            Actor.CurrentId.Should().Be(ActorId.None);
            actorIdOnWorkThread.Should().Be(_actorId);
        }

        [Fact]
        public void ForNonLongRunningTasksShouldUseNewThreadPoolThreadIfActiveThreadFromThreadPool()
        {
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.Create(() => { ThrowIfWaitTimesOut(barrier.Task); }, CancellationToken.None, TaskCreationOptions.None);
            var task2 = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.None);

            task1.Start(_scheduler);
            task2.Start(_scheduler);
            barrier.SetResult(true);
            ThrowIfWaitTimesOut(task2);

            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Exactly(2))).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Never)).ShouldNotThrow();
        }

        [Fact]
        public void ForNonLongRunningTasksShouldUseActiveThreadIfNotFromThreadPool()
        {
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.Create(() => { ThrowIfWaitTimesOut(barrier.Task); }, CancellationToken.None, TaskCreationOptions.LongRunning);
            var task2 = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.None);

            task1.Start(_scheduler);
            task2.Start(_scheduler);
            barrier.SetResult(true);
            ThrowIfWaitTimesOut(task2);

            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Never)).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
        }

        [Fact]
        public void ForNonLongRunningTasksShouldUseThreadPoolThreadIfNoActiveThread()
        {
            var task = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.None);
            task.Start(_scheduler);
            ThrowIfWaitTimesOut(task);
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Once)).ShouldNotThrow();
            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnNonThreadPoolThread(It.IsAny<Action>()), Times.Never)).ShouldNotThrow();
        }

        [Fact]
        public void ForNonLongRunningTasksThreadShouldBeMarkedAsActorThreadDuringExecution()
        {
            Actor.CurrentId.Should().Be(ActorId.None);
            var actorIdOnWorkThread = new ActorId();
            var task = _actorTaskFactory.Create(() => { actorIdOnWorkThread = Actor.CurrentId; }, CancellationToken.None, TaskCreationOptions.None);
            task.Start(_scheduler);
            ThrowIfWaitTimesOut(task);
            Actor.CurrentId.Should().Be(ActorId.None);
            actorIdOnWorkThread.Should().Be(_actorId);
        }

        [Theory]
        [InlineData(TaskCreationOptions.None, LateScheduleType.WhilstTerminalTaskProcessing)]
        [InlineData(TaskCreationOptions.None, LateScheduleType.AfterTerminalTaskComplete)]
        [InlineData(TaskCreationOptions.LongRunning, LateScheduleType.WhilstTerminalTaskProcessing)]
        [InlineData(TaskCreationOptions.LongRunning, LateScheduleType.AfterTerminalTaskComplete)]
        public void ShouldBeAbleToTerminateSchedulerSuchThatNoFurtherTasksAreExecuted(TaskCreationOptions terminalTaskCreationOptions, LateScheduleType lateScheduleType)
        {
            var barrier = new TaskCompletionSource<bool>();
            var terminalTask =
                _actorTaskFactory.Create(() =>
                                         {
                                             ThrowIfWaitTimesOut(barrier.Task);
                                             _scheduler.Terminate();
                                         }, CancellationToken.None, terminalTaskCreationOptions);
            var lateTask = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.None);

            terminalTask.Start(_scheduler);

            if (lateScheduleType == LateScheduleType.WhilstTerminalTaskProcessing)
            {
                lateTask.Start(_scheduler);
            }

            barrier.SetResult(true);

            terminalTask.AwaitingShouldCompleteIn(_waitTimeout);

            if (lateScheduleType == LateScheduleType.AfterTerminalTaskComplete)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                lateTask.Start(_scheduler);
            }

            lateTask.Wait(TimeSpan.FromSeconds(2)).Should().BeFalse();
        }

        [Fact]
        public void ShouldFailMiserablyIfTryToScheduleTaskThatIsNotAnActorTask()
        {
            var offensiveTask = new Task(() => { });
            Expect.That(() => offensiveTask.Start(_scheduler))
                  .ShouldThrow<TaskSchedulerException>()
                  .WithInnerException<InvalidOperationException>()
                  .WithInnerMessage("Task is not an actor task.");
        }

        [Fact]
        public async Task ShouldProcessEntiretyOfAnAsyncAction()
        {
            _scheduler = new ActorTaskScheduler(_actorId, new WorkItemQueuer(), _actorTaskFactory);

            var offActorWorkIds = new List<ActorId>();
            var actorWorkIds = new List<ActorId>();

            var asyncFunction = (Func<Task>)(
                                                async () =>
                                                {
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(() => offActorWorkIds.Add(Actor.CurrentId), LaunchType.DefaultScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult(true);
                                                                 }, LaunchType.DefaultScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult("hello");
                                                                 }, LaunchType.DefaultScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(() => offActorWorkIds.Add(Actor.CurrentId), LaunchType.NewThread);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult(true);
                                                                 }, LaunchType.NewThread);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult("hello");
                                                                 }, LaunchType.NewThread);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(() => offActorWorkIds.Add(Actor.CurrentId), LaunchType.CurrentScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult(true);
                                                                 }, LaunchType.CurrentScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                    await Launch(x =>
                                                                 {
                                                                     offActorWorkIds.Add(Actor.CurrentId);
                                                                     x.SetResult("hello");
                                                                 }, LaunchType.CurrentScheduler);
                                                    actorWorkIds.Add(Actor.CurrentId);
                                                });

            var task = _actorTaskFactory.Create(asyncFunction, CancellationToken.None, TaskCreationOptions.None);

            task.Start(_scheduler);

            await await task;

            actorWorkIds.Should().OnlyContain(x => x == _actorId);
            offActorWorkIds.Should().OnlyContain(x => x == ActorId.None);
        }

        [Fact]
        public void ShouldYieldCurrentThreadAndRerequestFromThreadPoolIfHaveProcessedNonLongRunningTasksContinuouslyForMoreThanAGivenTimePeriod()
        {
            var fixableUtcTimeSource = new FixableTimeSource();
            _scheduler = new ActorTaskScheduler(_actorId, Mock.Get(_workItemQueuer).Object, _actorTaskFactory);

            var taskStartBarrier = new TaskCompletionSource<bool>();
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.Create(() =>
                                                 {
                                                     taskStartBarrier.SetResult(true);
                                                     ThrowIfWaitTimesOut(barrier.Task);
                                                 }, CancellationToken.None, TaskCreationOptions.None);
            var task2 = _actorTaskFactory.Create(() => { }, CancellationToken.None, TaskCreationOptions.None);

            task1.Start(_scheduler);
            task2.Start(_scheduler);
            ThrowIfWaitTimesOut(taskStartBarrier.Task);
            fixableUtcTimeSource.Increment(TimeSpan.FromMilliseconds(250));
            barrier.SetResult(true);

            task2.AwaitingShouldCompleteIn(_waitTimeout);

            Expect.That(() => Mock.Get(_workItemQueuer).Verify(x => x.QueueOnThreadPoolThread(It.IsAny<Action>()), Times.Exactly(2))).ShouldNotThrow();
        }

        private static void LaunchOnNonThreadPoolThread(Action action)
        {
            var thread = new Thread(_ => action());
            thread.Start();
        }

        private static void LaunchOnThreadPoolThread(Action action)
        {
            ThreadPool.QueueUserWorkItem(_ => action(), null);
        }

        private enum LaunchType
        {
            NewThread,
            DefaultScheduler,
            CurrentScheduler
        }

        private static Task Launch(Action action, LaunchType launchType)
        {
            switch (launchType)
            {
                case LaunchType.DefaultScheduler:
                    return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                case LaunchType.CurrentScheduler:
                    return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Current);
                case LaunchType.NewThread:
                    var taskCompletionSource = new TaskCompletionSource<object>();
                    new Thread(_ =>
                               {
                                   action();
                                   taskCompletionSource.SetResult(null);
                               }).Start();
                    return taskCompletionSource.Task;
                default:
                    throw new InvalidOperationException($"Unexpected launch type: '{launchType}'.");
            }
        }

        private static Task Launch(Action<TaskCompletionSource<bool>> action, LaunchType launchType)
        {
            var promise = new TaskCompletionSource<bool>();
            Launch(() => action(promise), launchType);
            return promise.Task;
        }

        private static Task<string> Launch(Action<TaskCompletionSource<string>> action, LaunchType launchType)
        {
            var promise = new TaskCompletionSource<string>();
            Launch(() => action(promise), launchType);
            return promise.Task;
        }

        private void ThrowIfWaitTimesOut(Task task)
        {
            if (!task.Wait(_waitTimeout))
            {
                throw new TimeoutException();
            }
        }
    }
}
