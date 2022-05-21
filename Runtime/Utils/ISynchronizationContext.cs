using System.Threading;

namespace OpenUGD.Utils
{
    public interface ISynchronizationContext
    {
        void Send(SendOrPostCallback action, object state);
        void Post(SendOrPostCallback action, object state);
    }

    public class SynchronizationContextWrapper : ISynchronizationContext
    {
        public SynchronizationContextWrapper(SynchronizationContext context) => Context = context;

        public SynchronizationContext Context { get; }

        public void Send(SendOrPostCallback action, object state) => Context.Send(action, state);

        public void Post(SendOrPostCallback action, object state) => Context.Post(action, state);
    }
}
