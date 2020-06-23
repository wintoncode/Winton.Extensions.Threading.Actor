using System;
using System.Threading.Tasks;
using FluentAssertions;

namespace Winton.Extensions.Threading.Actor.Tests.Utilities
{
    public static class FluentTaskExtensions
    {
        public static void AwaitingShouldCompleteIn(this Task self, TimeSpan waitTime, string because = "", params object[] becauseArgs)
        {
            Action action = self.Wait;
            action.ExecutionTime().Should().BeLessThan(waitTime, because, becauseArgs);
        }

        public static AndConstraint<T> AwaitingShouldCompleteIn<T>(this Task<T> self, TimeSpan waitTime, string because = "", params object[] becauseArgs)
        {
            Action action = self.Wait;
            action.ExecutionTime().Should().BeLessThan(waitTime, because, becauseArgs);
            return new AndConstraint<T>(self.Result);
        }
    }
}