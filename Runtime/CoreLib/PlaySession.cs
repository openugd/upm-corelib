using UnityEngine;

namespace OpenUGD.Core
{
    /// <summary>
    /// The scope of one play session: a <see cref="OpenUGD.Lifetime"/> nested in
    /// <see cref="OpenUGD.Lifetime.Eternal"/> that ends when the application quits — in the editor, when play mode
    /// is exited — and is created afresh when the next session starts. Nest application-long things here, not in
    /// <see cref="OpenUGD.Lifetime.Eternal"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> <see cref="OpenUGD.Lifetime.Eternal"/> is a static field, so with domain reload disabled
    /// (<i>Enter Play Mode Options</i>) it survives from one play session to the next together with every scope
    /// still nested in it: a context, a subscription or a service rooted there keeps running into the next
    /// session, or into edit mode. Everything nested in this lifetime instead ends with the session, and the next
    /// session starts clean whether or not the domain was reloaded (audit UH-11, LS-19, CX-21).
    /// </para>
    /// <para>
    /// <b>Who uses it.</b> <see cref="ContextBehaviour"/>, <see cref="Presenters.ViewBehaviour"/> and
    /// <see cref="Utils.Components.SignalMonoBehaviour"/> nest the scope they create here, so a component that Unity never destroys — one that was never activated, or
    /// whose <c>OnDestroy</c> a subclass skipped — is reaped with the session instead of staying on
    /// <see cref="OpenUGD.Lifetime.Eternal"/> for the rest of the process. Pass it to
    /// <c>Context.CreateBuilder</c> for a context that is not owned by a <see cref="ContextBehaviour"/>.
    /// </para>
    /// <para>
    /// <b>When a session starts.</b> At <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>, the
    /// first point Unity runs code in a session, before any scene object is awake: when a player starts, and
    /// each time the editor enters play mode. A session still alive at that point — one that was never ended,
    /// or the edit-mode session below — is ended first.
    /// </para>
    /// <para>
    /// <b>When it ends.</b> At <see cref="Application.quitting"/>, which Unity raises when a player quits and,
    /// in the editor, when play mode is exited, before the scene's objects are destroyed. So the scopes nested
    /// here end then, newest first, while every object is still alive; a component's own <c>OnDestroy</c> later
    /// finds its scope already ended, and ending it again does nothing. Entering edit mode ends it as a backstop. Unity does not raise
    /// <see cref="Application.quitting"/> when a player is killed or crashes, and on platforms whose
    /// applications are suspended rather than quit.
    /// </para>
    /// <para>
    /// <b>Between sessions.</b> From the end of a session until the next one starts, this property returns the
    /// ended lifetime, so a scope created during the shutdown is born terminated rather than surviving into the
    /// next session. In the editor outside play mode it returns an <i>edit-mode session</i>, created on first
    /// use, which ends when play mode starts — so a component that wakes in edit mode (<c>[ExecuteAlways]</c>)
    /// has its scope ended when play mode starts.
    /// </para>
    /// <para>
    /// <b>Threads.</b> Safe to read from any thread. The session starts and ends on Unity's main thread.
    /// </para>
    /// </remarks>
    public static class PlaySession
    {
        private static readonly object Gate = new object();
        private static Lifetime.Definition _current;

        /// <summary>
        /// The current play session's lifetime. Never <c>null</c>; terminated from the moment the session ends
        /// until the next one starts.
        /// </summary>
        public static Lifetime Lifetime
        {
            get
            {
                lock (Gate)
                {
                    return (_current ??= Define()).Lifetime;
                }
            }
        }

        // Starts a new session, ending the one before it if it is still alive. The SubsystemRegistration hook, and
        // what tests call to stand in for it.
        internal static void Begin()
        {
            Lifetime.Definition previous;
            lock (Gate)
            {
                previous = _current;
                _current = Define();
            }

            // Outside the lock: terminating runs user code, which may read Lifetime.
            previous?.Terminate();
        }

        // Ends the current session. Until Begin, Lifetime returns the ended lifetime.
        internal static void End()
        {
            Lifetime.Definition current;
            lock (Gate)
            {
                current = _current;
            }

            current?.Terminate();
        }

        // Ends the current session and forgets it, so the next read opens a fresh (edit-mode) session.
        internal static void EndAndForget()
        {
            Lifetime.Definition current;
            lock (Gate)
            {
                current = _current;
                _current = null;
            }

            current?.Terminate();
        }

        private static Lifetime.Definition Define() => OpenUGD.Lifetime.Eternal.DefineNested(nameof(PlaySession));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            // Static event handlers survive a play session when the domain is not reloaded: remove before adding,
            // so there is exactly one.
            Application.quitting -= End;
            Application.quitting += End;
            Begin();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            // Application.quitting has normally ended the session already; this catches a session it did not end,
            // and lets edit mode open a session of its own.
            if (change == UnityEditor.PlayModeStateChange.EnteredEditMode) EndAndForget();
        }
#endif
    }
}
