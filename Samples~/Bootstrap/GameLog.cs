using OpenUGD.Core;
using OpenUGD.Logging;
using UnityEngine;

namespace OpenUGD.Samples.Bootstrap
{
    /// <summary>
    /// The application's log: one <see cref="LogRoot"/> per play session, written to the Unity console, and
    /// detached when the session ends.
    /// </summary>
    /// <remarks>
    /// Rooted at <see cref="PlaySession.Lifetime"/> rather than at a <c>ContextBehaviour</c>, because the log
    /// outlives any one context: every context of the session derives its loggers from this root. The root is
    /// created again at the start of every session, not once by a field initializer, so it stays correct with
    /// domain reload disabled (<i>Enter Play Mode Options</i>), where a static field outlives the session.
    /// </remarks>
    public static class GameLog
    {
        /// <summary>
        /// This session's root. Set before any scene object wakes; <c>null</c> in edit mode before the first
        /// session.
        /// </summary>
        public static LogRoot Root { get; private set; }

        // AfterAssembliesLoaded: after PlaySession has started the session, before the first scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void OnSessionStart()
        {
            var session = PlaySession.Lifetime;
            var root = new LogRoot("Game");

            // Quieter in a player: drop Verbose and Debug at the root, which filters the whole tree.
            if (!Application.isEditor) root.Flag = LogFlags.All & ~(LogFlags.Verbose | LogFlags.Debug);

            root.UseUnityConsole(session);
            session.AddAction(root.Dispose);
            Root = root;
        }
    }
}
