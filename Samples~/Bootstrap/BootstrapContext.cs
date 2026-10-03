using System;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Core;
using OpenUGD.Logging;
using OpenUGD.Utils;

namespace OpenUGD.Samples.Bootstrap
{
    /// <summary>
    /// The entry point: put it on an empty root GameObject and press Play. It builds a context with two services,
    /// logs through the session's <see cref="GameLog"/>, and reports the score until play mode ends.
    /// </summary>
    public sealed class BootstrapContext : ContextBehaviour
    {
        // Runs from Awake. The context is built under Lifetime, so it ends with this GameObject - or with the play
        // session, which ends first when play mode is exited.
        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            var builder = Context.CreateBuilder(Lifetime);
            builder.Services.AddInstance<ILog>(GameLog.Root.WithTag(nameof(BootstrapContext)));
            builder.Services.AddInstance<ICoroutineProvider>(this);
            builder.Services.Add<ScoreService>().As<IScore>();
            builder.Services.Add<AutoScorer>();

            // Validates the whole graph before constructing anything, then boots: every AwakeAsync, then every
            // InitializeAsync.
            return builder.BuildAsync(cancellationToken);
        }

        // Runs once the boot has finished; Context is set from here on.
        protected override void OnStarted(Context context)
        {
            var log = context.Resolve<ILog>();
            var score = context.Resolve<IScore>();

            // Subscriptions end with the context: no unsubscribe code anywhere.
            score.Changed.Subscribe(context.Lifetime, value => log.Info($"score {value}"));
            OnQuit.Subscribe(context.Lifetime, () => log.Info($"quitting with score {score.Value}"));
            context.Lifetime.AddAction(() => log.Info("context disposed"));

            log.Info("started");
        }

        // The default logs the exception; a game would show an error screen here as well.
        protected override void OnStartFailed(Exception exception)
        {
            base.OnStartFailed(exception);
            GameLog.Root?.Fatal($"{name} failed to start: {exception.Message}");
        }
    }
}
