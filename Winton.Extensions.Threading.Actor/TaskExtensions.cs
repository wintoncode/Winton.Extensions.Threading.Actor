using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    public static class TaskExtensions
    {
        public static Task WhileActorPaused(this Task self)
        {
            ActorTaskScheduler.CurrentActorScheduler?.WhileActorPaused(self);
            return self;
        }

        public static Task<T> WhileActorPaused<T>(this Task<T> self)
        {
            ActorTaskScheduler.CurrentActorScheduler?.WhileActorPaused(self);
            return self;
        }
    }
}
