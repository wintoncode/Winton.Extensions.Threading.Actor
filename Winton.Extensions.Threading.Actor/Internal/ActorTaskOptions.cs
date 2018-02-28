using System.Threading.Tasks;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal static class ActorTaskOptions
    {
        public static TaskCreationOptions GetTaskCreationOptions(ActorEnqueueOptions enqueueOptions) => enqueueOptions.HasFlag(ActorEnqueueOptions.WorkIsLongRunning) ? TaskCreationOptions.LongRunning : TaskCreationOptions.None;
    }
}
