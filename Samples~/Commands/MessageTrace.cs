using OpenUGD.Commands;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Commands
{
    /// <summary>A listener that hears every message, whatever its type, after the commands mapped to it.</summary>
    public sealed class MessageTrace : ITellMessage
    {
        private readonly ILog _log;

        public MessageTrace(ILog log) => _log = log.WithTag("Trace");

        public void Tell(object message) => _log.Verbose(message.GetType().Name);
    }
}
