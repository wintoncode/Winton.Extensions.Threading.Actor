# Usage

## Basic usage of an actor

Let's take as an example a cache of employee records that exist in a database.

Say we have an interface to a database defined as

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Example
{
    public interface IEmployeeDatabase : IDisposable
    {
        Task<IEnumerable<Employee>> GetAll();

        Task<Employee> GetEmployee(int payrollId);
    }
}
```

where an employee record is defined as

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Example
{
    public sealed class Employee
    {
        public Employee(int payrollId, string name, string department)
        {
            PayrollId = payrollId;
            Name = name;
            Department = department;
        }

        public int PayrollId { get; }

        public string Name { get; }

        public string Department { get; }
    }
}
```

Now, let's assume that we want to cache the results of database retrievals for performance reasons but that the cache
can be accessed from multiple threads concurrently.
We can define a class called `EmployeeCache` to achieve this with the class maintaining a dictionary of employee records as a simple cache.
We could protect access to that dictionary using the usual locking mechanisms but let's say instead that we want to implement `EmployeeCache`
as an actor.
That would be done thus:
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Example
{
    public sealed class EmployeeCache : IDisposable
    {
        private readonly IActor _actor;
        private readonly IEmployeeDatabase _database;
        private readonly Dictionary<int, Employee> _cache = new Dictionary<int, Employee>();

        private bool _disposed = false;

        public EmployeeCache(IEmployeeDatabase database)
        {
            _database = database;

            // The actor needs to be created and explicitly started.
            _actor = new Actor();
            _actor.Start();
        }

        public Task Clear()
        {
            return _actor.Enqueue(() => _cache.Clear());
        }

        public Task<Employee> GetEmployee(int payrollId)
        {
            return _actor.Enqueue(
                async () =>
                {
                    if (!_cache.ContainsKey(payrollId))
                    {
                        _cache[payrollId] = await _database.GetEmployee(payrollId);
                    }

                    return _cache[payrollId];
                });
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
```

As you can see, effectively all code that manipulates or uses data members is passed as a delegate to one of the overloads of the `Enqueue`
method present on the `IActor` interface.
Each delegate is executed in the order in which it is enqueued.

Note that the usage pattern we have here is to wrap an `IActor` instance in a class with a well-defined interface (here `EmployeeCache`) rather
than exposing the `IActor` instance directly.
This has the effect of turning `EmployeeCache` into an actor and, of course, displays much better encapsulation.

The first method, `Clear`, simply enqueues a delegate to call `Clear` on the internal dictionary `_cache`.

The second method, `GetEmployee`, checks for the presence on an employee (identified by the payroll ID) in the cache and returns that if present.
Otherwise, a call to the database is awaited to retrieve the employee record which is in turn cached.
There are a number of things to note about this method:

1. The call to `_database.GetEmployee` is performed on a different thread to that processing the actor's current queue of work so that
the actor is not blocked.
1. When the database returns its result, the handling of the result (i.e. ```_cache[payroll] = ```) is scheduled on the actor's work queue. That is,
`Enqueue` is called implicitly with the work that forms the continuation from the `await`.
   * An important thing to note here is that this means that before the database returns the employee requested, the cache's actor is free to field calls
to `GetEmployee` for different employee ids or _the same_ employee id.
In the latter case a second call to the database will occur.
The code could be engineered to prevent this if desired but let's assume we don't mind the odd extra call to the database.
   * If we did want to prevent this one way would be to pause the actor whilst we wait for the database to return. In this particular case that would probably be a little heavy-handed but a means of doing this is described below.
1. Finally, if the call to the database causes an exception because, say, no employee exists for the given `payrollId` then that exception
will be passed up to the code that called into the `EmployeeCache` object.

### Pausing the actor during an `await`

Let's say you wanted to ensure only one call to the database to get an employee.
A rather blunt way of doing that would be to essentially pause the actor until the database call returns.
You'd achieve that using the `WhileActorPaused` extension:

```csharp
        public Task<Employee> GetEmployee(int payrollId)
        {
            return _actor.Enqueue(
                async () =>
                {
                    if (!_cache.ContainsKey(payrollId))
                    {
                        _cache[payrollId] = await _database.GetEmployee(payrollId).WhileActorPaused();
                    }

                    return _cache[payrollId];
                });
        }
```

Essentially this will prevent the actor from doing anything at all until the task returned by `_database.GetEmployee(payrollId)` completes and the first thing processed by the actor when it resumes will be the continuation after the call (i.e. `_cache[payrollId] = ...`).

This device should be used with caution - and would most likely be inappropriate in the case of this example - but could be useful in some circumstances.
Issues can arise with an actor being paused permanently if library code does not use `ConfigureAwait(false)` when awaiting.
For instance, if an implementation of `IEmployeeDatabase.GetEmployee` itself awaited something without using `ConfigureAwait(false)`, then it's likely that the continuation of that await would be scheduled on the actor's work queue.
But, as the actor has been paused, that continuation will never be executed.
Consequently, the continuation of `await _database.GetEmployee(payrollId).WhileActorPaused()` will never be scheduled on the actor and it's the scheduling of this that unpauses the actor.

## Specifying work to do when the actor starts

Sometimes it's useful to to specify work that the actor must do when it starts prior to processing any more work.
This work can be specified before the actor is started by either setting the `IActor.StartWork` property or using one of the
`WithStartWork` extensions to `IActor`.

Let's say in our example that we want to pre-populate the cache by calling `IEmployeeDatabase.GetAll` at start-up.
To do this we change the `EmployeeCache` constructor thus:

```csharp
        public EmployeeCache(IEmployeeDatabase database)
        {
            _database = database;

            // The actor needs to be created and explicitly started.
            _actor =
                new Actor()
                    .WithStartWork(async () =>
                                   {
                                       foreach (var employee in await _database.GetAll())
                                       {
                                           _cache[employee.PayrollId] = employee;
                                       }
                                   });
            _actor.Start();
        }
```

Work run at start-up is guaranteed to complete before any work enqueued on the actor is processed.
Note that this extends to asynchronous work as above - nothing else will be processed by the actor until all the employee records
returned by `GetAll` are added to the employee dictionary.
This behaviour is different to asynchronous work enqueued in the normal manner.

Finally, if the start work fails then the actor will be stopped and no more work will be processed.

## Stopping an actor

An actor can be explicitly stopped if desired.
Calling `IActor.Stop` will mean that all work already enqueued will be processed but that all work enqueued
thereafter will be cancelled once the outstanding work has been processed.

Currently, our example `EmployeeCache` doesn't really do anything when it is disposed so let's have it stop the actor:

```csharp
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _actor.Stop().Wait();
                }
            }
        }
```

`IActor.Stop` returns a `Task` which here we perform a blocking wait on.
If we didn't wait then we could exit `Dispose` with an object that isn't truly disposed because the actor could still be processing
work.
A preferable pattern to using `Dispose` - especially when you have multiple objects using the actor pattern - would be to provide
an asynchronous shutdown method.
For example we could have:

```csharp
    public sealed class EmployeeCache
    {
        ...

        public Task Shutdown()
        {
            return _actor.Stop();
        }

        ...
    }
```

If you had multiple objects using the actor pattern, each with their own version of `Shutdown`, then you could call `Shutdown` on each before
using `Task.WaitAll` or similar to block until shutdown has completed for all.

### Specifying stop work

As with starting an actor, work can be specified to be done when it is stopped though this can only be synchronous work.

Let's say that in our example, it is the responsibility of the `EmployeeCache` object to dispose of the database object
when it is itself disposed.
You wouldn't want to do that directly in `EmployeeCache.Dispose` because you could be disposing of the database object whilst
the actor is still using it.
Instead we can schedule this to be done once the actor has completed all enqueued work by specifying "stop work"
prior to starting the actor:

```csharp
        public EmployeeCache(IEmployeeDatabase database)
        {
            _database = database;

            // The actor needs to be created and explicitly started.
            _actor =
                new Actor()
                    .WithStartWork(async () =>
                                   {
                                       foreach (var employee in await _database.GetAll())
                                       {
                                           _cache[employee.PayrollId] = employee;
                                       }
                                   })
                    .WithStopWork(() => _database.Dispose());

            _actor.Start();
        }
```

## Scheduling repeated work

Occasionally there is a need to periodically schedule work on an actor.
This can be done using the extensions in the `ActorSchedulingExtensions` class.

A scheduler could be used in our example to clear cache every hour to prevent long-term build-up.
The code below shows the `EmployeeCache` with a scheduler (`_actorWorkScheduler`) added:

```csharp
namespace Example
{
    public sealed class EmployeeCache : IDisposable
    {
        private readonly IActor _actor;
        private readonly IEmployeeDatabase _database;
        private readonly Dictionary<int, Employee> _cache = new Dictionary<int, Employee>();
        private readonly IActorWorkScheduler _actorWorkScheduler;

        private bool _disposed = false;

        public EmployeeCache(IEmployeeDatabase database)
        {
            _database = database;

            // The actor needs to be created and explicitly started.
            _actor =
                new Actor()
                    .WithStartWork(async () =>
                                   {
                                       foreach (var employee in await _database.GetAll())
                                       {
                                           _cache[employee.PayrollId] = employee;
                                       }
                                   })
                    .WithStopWork(() => _database.Dispose());

            _actorWorkScheduler = _actor.CreateWorkScheduler();

            _actorWorkScheduler.Schedule(() => _cache.Clear(),
                                         TimeSpan.FromHours(1),
                                         ActorScheduleOptions.Default,
                                         // Error handler.
                                         x => Console.WriteLine($"Error: {x}"));

            _actor.Start();
        }

        public Task Clear()
        {
            return _actor.Enqueue(() => _cache.Clear());
        }

        public Task<Employee> GetEmployee(int payrollId)
        {
            return _actor.Enqueue(
                async () =>
                {
                    if (!_cache.ContainsKey(payrollId))
                    {
                        _cache[payrollId] = await _database.GetEmployee(payrollId);
                    }

                    return _cache[payrollId];
                });
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _actorWorkScheduler.CancelCurrent();
                    _actor.Stop().Wait();
                }
            }
        }
    }
}
```

Notes:
* It is simpler to use the `Schedule` methods on `ActorSchedulingExtensions` than the `IActorWorkScheduler.Schedule` overloads
as the former have error handling functionality. The `IActorWorkScheduler.Schedule` overloads require the user to reschedule
should the scheduled work fail, though that might be what you want.
* Any call to a `Schedule` method will cancel the previous schedule for that scheduler if there was one.
To have multiple concurrent schedules you need multiple schedulers.
* Although the work specified will be enqueued on the actor at the interval specified this does not mean the actor
will actually execute that work at that interval - the actual execution interval will vary depending on how busy the actor is.
In addition, the interval is timed from when the scheduled work last completed rather than from when it was enqueued.
