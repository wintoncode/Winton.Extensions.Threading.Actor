using System;
using System.Threading;

namespace Winton.Extensions.Threading.Actor.Internal
{
    internal sealed class WorkItemQueuer : IWorkItemQueuer
    {
        public void QueueOnThreadPoolThread(Action work)
        {
            ThreadPool.QueueUserWorkItem(_ => work(), null);
        }

        public void QueueOnNonThreadPoolThread(Action work)
        {
            var thread = new Thread(_ => work())
                         {
                             IsBackground = true
                         };
            thread.Start();
        }
    }
}
