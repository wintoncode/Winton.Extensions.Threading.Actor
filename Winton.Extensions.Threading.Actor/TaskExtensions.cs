using System;
using System.Threading.Tasks;
using Winton.Extensions.Threading.Actor.Internal;

namespace Winton.Extensions.Threading.Actor
{
    public static class TaskExtensions
    {
        public static ActorPauseAwaitable WhileActorPaused(this Task self)
        {
            MustBeOnActorThread();
            return ActorTaskScheduler.CurrentActorScheduler.WhileActorPaused(self);
        }

        public static ActorPauseAwaitable<T> WhileActorPaused<T>(this Task<T> self)
        {
            MustBeOnActorThread();
            return ActorTaskScheduler.CurrentActorScheduler.WhileActorPaused(self);
        }

        private static void MustBeOnActorThread()
        {
            if (Actor.CurrentId == default(ActorId))
            {
                throw new InvalidOperationException("This should only be called on the actor's thread.");
            }
        }
    }
}
