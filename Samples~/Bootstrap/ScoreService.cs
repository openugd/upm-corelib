using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Logging;

namespace OpenUGD.Samples.Bootstrap
{
    /// <summary>
    /// A service: a plain class the container constructs, injects through its constructor and drives through the
    /// two boot phases it opts into. No base class.
    /// </summary>
    public sealed class ScoreService : IScore, IAwakeService, IInitializeService
    {
        private readonly ILog _log;
        private readonly Signal<int> _changed;

        // The container supplies the context's own Lifetime; everything else is resolved from the registrations.
        // The signal is created here, so it exists before anyone can subscribe.
        public ScoreService(ILog log, Lifetime lifetime)
        {
            _log = log.WithTag(nameof(ScoreService));
            _changed = new Signal<int>(lifetime);
        }

        public int Value { get; private set; }

        public ISignal<int> Changed => _changed;

        // Awake: make your own state ready. Other services may not be initialized yet.
        public Task AwakeAsync(CancellationToken cancellationToken)
        {
            Value = 0;
            _log.Info("awake");
            return Task.CompletedTask;
        }

        // Initialize: every service is awake, so this is where services start talking to each other.
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            _log.Info("initialized");
            return Task.CompletedTask;
        }

        public void Add(int points)
        {
            Value += points;

            // The argument of a write is evaluated even when the write is dropped: ask first if it is costly.
            if (_log.IsEnabled(LogFlags.Debug)) _log.Debug($"+{points} -> {Value}");

            _changed.Fire(Value);
        }
    }
}
