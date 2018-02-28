using System.Threading;
using FluentAssertions;

namespace Winton.Extensions.Threading.Actor.Tests.Utilities
{
    public static class ActorThreadAssertions
    {
        public static void CurrentThreadShouldBeThreadPoolThread()
        {
#if !NETSTANDARD1_3
            Thread.CurrentThread.IsThreadPoolThread.Should().BeTrue("should be on thread pool thread");
#endif
            Thread.CurrentThread.Name.Should().NotBe("ActorWorker", "should be on thread pool thread");
        }

        public static void CurrentThreadShouldNotBeThreadPoolThread()
        {
#if !NETSTANDARD1_3
            Thread.CurrentThread.IsThreadPoolThread.Should().BeFalse("should not be on thread pool thread");
#endif
            Thread.CurrentThread.Name.Should().Be("ActorWorker", "should not be on thread pool thread");
        }
    }
}
