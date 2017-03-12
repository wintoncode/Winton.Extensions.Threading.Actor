# Winton.Extensions.Threading.Actor

A lightweight implementation of the actor pattern designed to integrate with C#'s `async`/`await` keywords.
It is a richer version of the implementation outlined on [Winton's Tech Blog](https://tech.winton.com/blog/2017/03/a-tpl-actor-pattern).

## Overview

Actors are objects that maintain a queue of work items and process that queue sequentially on a dedicated thread of
execution.
External agents pass work to an actor via messages.
If required, messages can be used by an actor to transmit results in response to these messages.
All interaction with actors is asynchronous.
This is implemented here by viewing an inbound message as a delegate and an outbound response message as
a `Task` or `Task<T>`.

An important motivation for using actors is to avoid the sharing of data between threads within a process and
so avoid the pitfalls such as deadlocks of synchronising access to that data.
Rather than share data, a single entity (the actor) which carries out all actions in series is
given responsibility for maintaining that data.
A program can then be built as a set of interacting actors.
Here execution flow is well-defined in that the scope of a single thread of execution is bounded to
a single actor instance rather than it being the case that a thread of execution can wander unbounded
through application code.
In effect a single thread has a particular responsibility and does not stray beyond that responsibility.

This library provides an implementation of an actor that allows work to be enqueued and handled sequentially.
The actor can be started and stopped - though not paused - and support exists for specifying work to be done at both
those state changes.
In addition, a scheduler is provided to allow for the periodic scheduling of work on an actor.

## Documentation

The usage and API of this library are best illustrated via examples.
These can be found in the [USAGE](USAGE.md) file.

## Supported platforms

The following platforms are supported:

- .NET Core
- .NET 4.5.1

## Installation

The easiest way to install this library is to add a NuGet dependency on `Winton.Extensions.Threading.Actor` to your
library. 

## Building

Currently version `1.0` of the [.NET Core build tools](https://docs.microsoft.com/en-us/dotnet/articles/core/tools/) is required to build the source code.
To build from Visual Studio you'll need VS 2017.

Assuming you have the .NET Core build tools installed, building the library from the command-line just involves:

```
dotnet restore
dotnet build
```

The tests use [xUnit.net](https://xunit.github.io/).
To run them from the command-line use:

```
dotnet test Winton.Extensions.Threading.Actor.Tests.Unit/Winton.Extensions.Threading.Actor.Tests.Unit.csproj

```
