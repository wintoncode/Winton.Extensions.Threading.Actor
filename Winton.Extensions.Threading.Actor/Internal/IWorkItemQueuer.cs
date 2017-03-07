using System;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal interface IWorkItemQueuer
    {
        void QueueOnThreadPoolThread(Action work);

        void QueueOnNonThreadPoolThread(Action work);
    }
}
