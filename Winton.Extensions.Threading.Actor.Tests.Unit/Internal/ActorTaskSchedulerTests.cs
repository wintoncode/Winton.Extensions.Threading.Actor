﻿// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
        private readonly ActorTaskFactory _actorTaskFactory;
        private readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(10);
        private readonly ActorTaskScheduler _scheduler;

        public ActorTaskSchedulerTests()
        {
            _scheduler = new ActorTaskScheduler(_actorId);
            _actorTaskFactory = new ActorTaskFactory(_scheduler);
        }

        [Theory]
        [InlineData(ActorTaskTraits.Resuming)]
        [InlineData(ActorTaskTraits.CriticalResumer)]
        internal async Task ShouldBeAbleToResumeInitiallyPausedScheduler(ActorTaskTraits resumingTaskTraits)
        {
            var count = 0;
            var task1 = _actorTaskFactory.StartNew(() => ++count, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);
            var task2 = _actorTaskFactory.StartNew(() => ++count, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            Task.WhenAny(task1, task2).Wait(TimeSpan.FromSeconds(1)).Should().BeFalse("tasks should not have been executed if the scheduler is paused");

            var resumer = _actorTaskFactory.StartNew(() => ++count, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, resumingTaskTraits);

            (await resumer).Should().Be(1);
            (await task1).Should().Be(2);
            (await task2).Should().Be(3);
        }

        [Fact]
        public async Task FailureOfCriticalTaskShouldTerminateSchedulerWithSubsequentTasksBeingCancelled()
        {
            UnpauseScheduler();

            var task1 = _actorTaskFactory.StartNew(new Action(() => throw new Exception("oh dear")), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            await task1.Awaiting(x => x).Should().ThrowAsync<Exception>().WithMessage("oh dear");

            _scheduler.TerminatedTask.Wait(TimeSpan.FromMilliseconds(250)).Should().BeFalse();

            var task2 = _actorTaskFactory.StartNew(new Action(() => throw new Exception("no!!!")), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.Critical);
            var task3 = _actorTaskFactory.StartNew(new Action(() => throw new Exception("shouldn't hit this")), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            await task2.Awaiting(x => x).Should().ThrowAsync<Exception>().WithMessage("no!!!");

            await ThrowIfWaitTimesOut(_scheduler.TerminatedTask);

            await task3.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();

            var task4 = _actorTaskFactory.StartNew(() => throw new Exception("shouldn't hit this either"), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            await task4.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();
        }

        [Theory]
        [InlineData(TaskStatus.Canceled)]
        [InlineData(TaskStatus.Faulted)]
        [InlineData(TaskStatus.RanToCompletion)]
        public async Task SchedulingATerminalTaskShouldTerminateSchedulerWithSubsequentTasksBeingCancelled(TaskStatus terminalWorkOutcomeType)
        {
            UnpauseScheduler();

            void TerminalWork()
            {
                switch (terminalWorkOutcomeType)
                {
                    case TaskStatus.Canceled:
                        throw new TaskCanceledException();
                    case TaskStatus.Faulted:
                        throw new Exception("oh dear");
                    case TaskStatus.RanToCompletion:
                        break;
                }
            }

            var task1 = _actorTaskFactory.StartNew(TerminalWork, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.Terminal);
            var task2 = _actorTaskFactory.StartNew(() => throw new Exception("shouldn't hit this"), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            switch (terminalWorkOutcomeType)
            {
                case TaskStatus.Canceled:
                    await task1.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();
                    await _scheduler.TerminatedTask.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();
                    break;
                case TaskStatus.Faulted:
                    await task1.Awaiting(x => x).Should().ThrowAsync<Exception>().WithMessage("oh dear");
                    await _scheduler.TerminatedTask.Awaiting(x => x).Should().ThrowAsync<Exception>().WithMessage("oh dear");
                    break;
                case TaskStatus.RanToCompletion:
                    await task1.Awaiting(x => x).Should().NotThrowAsync();
                    await _scheduler.TerminatedTask.Awaiting(x => x).Should().NotThrowAsync();
                    break;
            }

            await task2.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();

            var task3 = _actorTaskFactory.StartNew(() => throw new Exception("shouldn't hit this either"), CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            await task3.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task ForLongRunningTasksShouldUseActiveThreadIfNotFromThreadPool()
        {
            UnpauseScheduler();

            var task1ThreadId = 0;
            var task2ThreadId = 0;
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.StartNew(
                async () =>
                {
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                    await ThrowIfWaitTimesOut(barrier.Task);
                    task1ThreadId = Thread.CurrentThread.ManagedThreadId;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, ActorEnqueueOptions.Default, ActorTaskTraits.None);
            var task2 = _actorTaskFactory.StartNew(
                () =>
                {
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                    task2ThreadId = Thread.CurrentThread.ManagedThreadId;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            barrier.SetResult(true);
            await ThrowIfWaitTimesOut(task1);
            await ThrowIfWaitTimesOut(task2);

            task1ThreadId.Should().NotBe(0);
            task2ThreadId.Should().Be(task1ThreadId);
        }

        [Fact]
        public async Task ForLongRunningTasksShouldUseNonThreadPoolThreadIfActiveThreadFromThreadPool()
        {
            UnpauseScheduler();

            var shortTaskThreadId = 0;
            var longTaskThreadId = 0;
            var barrier = new TaskCompletionSource<bool>();
            var shortTask = _actorTaskFactory.StartNew(
                async () =>
                {
                    ActorThreadAssertions.CurrentThreadShouldBeThreadPoolThread();
                    await ThrowIfWaitTimesOut(barrier.Task);
                    shortTaskThreadId = Thread.CurrentThread.ManagedThreadId;
                }, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.None);
            var longTask = _actorTaskFactory.StartNew(
                () =>
                {
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                    longTaskThreadId = Thread.CurrentThread.ManagedThreadId;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, ActorEnqueueOptions.Default, ActorTaskTraits.None);

            barrier.SetResult(true);

            await ThrowIfWaitTimesOut(shortTask);
            await ThrowIfWaitTimesOut(longTask);

            shortTaskThreadId.Should().NotBe(0);
            longTaskThreadId.Should().NotBe(0);
            longTaskThreadId.Should().NotBe(shortTaskThreadId);
        }

        [Fact]
        public async Task ForLongRunningTasksShouldUseNonThreadPoolThreadIfNoActiveThread()
        {
            UnpauseScheduler();

            var task = _actorTaskFactory.StartNew(ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread, CancellationToken.None, TaskCreationOptions.LongRunning);

            await ThrowIfWaitTimesOut(task);
        }

        [Fact]
        public async Task ForLongRunningTasksThreadShouldBeMarkedAsActorThreadDuringExecution()
        {
            UnpauseScheduler();

            Actor.CurrentId.Should().Be(ActorId.None);
            var actorIdOnWorkThread = new ActorId();
            var task = _actorTaskFactory.StartNew(() => { actorIdOnWorkThread = Actor.CurrentId; }, CancellationToken.None, TaskCreationOptions.None);

            await ThrowIfWaitTimesOut(task);
            Actor.CurrentId.Should().Be(ActorId.None);
            actorIdOnWorkThread.Should().Be(_actorId);
        }

        [Fact]
        public async Task ForNonLongRunningTasksShouldUseThreadPoolThreads()
        {
            UnpauseScheduler();

            // It would be nice to check here that a new queue on thread pool occurs for
            // each task rather than using the same thread. However, that's not possible
            // as you will probably get the same thread anyway.
            var task1ThreadId = 0;
            var task2ThreadId = 0;
            var barrier1 = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.StartNew(
                async () =>
                {
                    task1ThreadId = Thread.CurrentThread.ManagedThreadId;
                    ActorThreadAssertions.CurrentThreadShouldBeThreadPoolThread();
                    await ThrowIfWaitTimesOut(barrier1.Task);
                }, CancellationToken.None, TaskCreationOptions.None);
            var task2 = _actorTaskFactory.StartNew(
                () =>
                {
                    task2ThreadId = Thread.CurrentThread.ManagedThreadId;
                    ActorThreadAssertions.CurrentThreadShouldBeThreadPoolThread();
                }, CancellationToken.None, TaskCreationOptions.None);

            barrier1.SetResult(true);

            await ThrowIfWaitTimesOut(task1);
            await ThrowIfWaitTimesOut(task2);

            task1ThreadId.Should().NotBe(0);
            task2ThreadId.Should().NotBe(0);
        }

        [Fact]
        public async Task ForNonLongRunningTasksShouldUseActiveThreadIfNotFromThreadPool()
        {
            UnpauseScheduler();

            var task1ThreadId = 0;
            var task2ThreadId = 0;
            var task3ThreadId = 0;
            var barrier = new TaskCompletionSource<bool>();
            var task1 = _actorTaskFactory.StartNew(
                async () =>
                {
                    task1ThreadId = Thread.CurrentThread.ManagedThreadId;
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                    await ThrowIfWaitTimesOut(barrier.Task);
                }, CancellationToken.None, TaskCreationOptions.LongRunning);
            var task2 = _actorTaskFactory.StartNew(
                () =>
                {
                    task2ThreadId = Thread.CurrentThread.ManagedThreadId;
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                }, CancellationToken.None, TaskCreationOptions.None);
            var task3 = _actorTaskFactory.StartNew(
                () =>
                {
                    task3ThreadId = Thread.CurrentThread.ManagedThreadId;
                    ActorThreadAssertions.CurrentThreadShouldNotBeThreadPoolThread();
                }, CancellationToken.None, TaskCreationOptions.None);

            barrier.SetResult(true);

            await ThrowIfWaitTimesOut(task1);
            await ThrowIfWaitTimesOut(task2);
            await ThrowIfWaitTimesOut(task3);

            task1ThreadId.Should().NotBe(0);
            task2ThreadId.Should().Be(task1ThreadId);
            task3ThreadId.Should().Be(task1ThreadId);
        }

        [Fact]
        public async Task ForNonLongRunningTasksThreadShouldBeMarkedAsActorThreadDuringExecution()
        {
            UnpauseScheduler();

            Actor.CurrentId.Should().Be(ActorId.None);
            var actorIdOnWorkThread = new ActorId();
            var task = _actorTaskFactory.StartNew(() => { actorIdOnWorkThread = Actor.CurrentId; }, CancellationToken.None, TaskCreationOptions.None);
            await ThrowIfWaitTimesOut(task);
            Actor.CurrentId.Should().Be(ActorId.None);
            actorIdOnWorkThread.Should().Be(_actorId);
        }

        [Theory]
        [InlineData(TaskCreationOptions.None, LateScheduleType.WhilstTerminalTaskProcessing)]
        [InlineData(TaskCreationOptions.None, LateScheduleType.AfterTerminalTaskComplete)]
        [InlineData(TaskCreationOptions.LongRunning, LateScheduleType.WhilstTerminalTaskProcessing)]
        [InlineData(TaskCreationOptions.LongRunning, LateScheduleType.AfterTerminalTaskComplete)]
        public async Task ShouldBeAbleToTerminateSchedulerSuchThatNoFurtherTasksAreExecuted(TaskCreationOptions terminalTaskCreationOptions, LateScheduleType lateScheduleType)
        {
            UnpauseScheduler();

            var barrier = new TaskCompletionSource<bool>();
            var terminalTask =
                _actorTaskFactory.StartNew(async () =>
                                         {
                                             await ThrowIfWaitTimesOut(barrier.Task);
                                         }, CancellationToken.None, terminalTaskCreationOptions, ActorEnqueueOptions.Default, ActorTaskTraits.Terminal);

            Task lateTask = default;

            if (lateScheduleType == LateScheduleType.WhilstTerminalTaskProcessing)
            {
                lateTask = _actorTaskFactory.StartNew(() => { }, CancellationToken.None, TaskCreationOptions.None);
            }

            barrier.SetResult(true);

            terminalTask.AwaitingShouldCompleteIn(_waitTimeout);

            if (lateScheduleType == LateScheduleType.AfterTerminalTaskComplete)
            {
                lateTask = _actorTaskFactory.StartNew(() => { }, CancellationToken.None, TaskCreationOptions.None);
            }

            await lateTask.Awaiting(x => x).Should().ThrowAsync<TaskCanceledException>();
        }

        [Fact]
        public async Task ShouldProcessEntiretyOfAnAsyncAction()
        {
            UnpauseScheduler();

            var offActorWorkIds = new List<ActorId>();
            var actorWorkIds = new List<ActorId>();

            async Task AsyncFunction()
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
            }

            var task = _actorTaskFactory.StartNew(AsyncFunction, CancellationToken.None, TaskCreationOptions.None);

            await await task;

            actorWorkIds.Should().OnlyContain(x => x == _actorId);
            offActorWorkIds.Should().OnlyContain(x => x == ActorId.None);
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

        private async Task ThrowIfWaitTimesOut(Task task)
        {
            var timeout = Task.Delay(_waitTimeout);

            async Task Action(Task<Task> x)
            {
                var firstToFinish = await x.ConfigureAwait(false);

                if (firstToFinish == timeout)
                {
                    throw new TimeoutException("Timed out awaiting task completion.");
                }

                await firstToFinish.ConfigureAwait(false);
            }

            await Task.WhenAny(timeout, task).Awaiting(Action).Should().NotThrowAsync();
        }

        private void UnpauseScheduler()
        {
            _actorTaskFactory.StartNew(() => { }, CancellationToken.None, TaskCreationOptions.None, ActorEnqueueOptions.Default, ActorTaskTraits.Resuming).Wait();
        }
    }
}
