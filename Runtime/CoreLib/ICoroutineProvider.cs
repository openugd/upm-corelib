using System.Collections;
using UnityEngine;

namespace OpenUGD.Utils
{
    /// <summary>
    /// The ability to run a Unity coroutine, without owning a <see cref="MonoBehaviour"/> to run it on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Coroutines are the one piece of Unity scheduling that cannot be reached from a plain C# object: they
    /// require a live <see cref="MonoBehaviour"/>. This interface is the seam that lets a service, a
    /// presenter or a test double schedule one without inheriting from the engine — the engine boundary is
    /// one interface wide, and everything above it stays testable.
    /// </para>
    /// <para>
    /// <b>Never returns <c>null</c>.</b> An implementation that cannot start the coroutine must throw.
    /// Returning <c>null</c> would hand the caller a "success" it cannot distinguish from a silent no-op, and
    /// callers routinely discard the return value. That includes a coroutine that finishes inside the call — its
    /// first step is its last — for which Unity's own <c>StartCoroutine</c> returns <c>null</c>: the
    /// implementations in this package return a handle for it too.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> A coroutine is bound to the host object, not to a <see cref="Lifetime"/>. It stops
    /// when the host <see cref="GameObject"/> is deactivated or destroyed. To bind one to a scope, terminate
    /// it yourself: <c>lifetime.AddAction(() =&gt; provider.StopCoroutine(coroutine))</c>.
    /// </para>
    /// </remarks>
    public interface ICoroutineProvider
    {
        /// <summary>
        /// Starts <paramref name="enumerator"/> as a Unity coroutine.
        /// </summary>
        /// <param name="enumerator">The coroutine body. Must not be <c>null</c>.</param>
        /// <returns>
        /// The running coroutine, for a later <see cref="StopCoroutine"/>. Never <c>null</c>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="enumerator"/> is
        /// <c>null</c>.</exception>
        /// <exception cref="System.InvalidOperationException">
        /// The host cannot run coroutines — it has been destroyed, or its <see cref="GameObject"/> is
        /// inactive. The implementations in this package also refuse a disabled host
        /// (<see cref="Behaviour.enabled"/> is <c>false</c>), and accept one in its <c>Awake</c>, as Unity does.
        /// </exception>
        Coroutine StartCoroutine(IEnumerator enumerator);

        /// <summary>
        /// Stops a coroutine previously returned by <see cref="StartCoroutine"/>.
        /// </summary>
        /// <remarks>
        /// Stopping a coroutine that has already finished, or whose host has been destroyed, is a no-op: the
        /// requested postcondition — that the coroutine is not running — already holds. This is not a silent
        /// failure, it is the operation succeeding trivially.
        /// </remarks>
        /// <param name="coroutine">The coroutine to stop. Must not be <c>null</c>.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="coroutine"/> is
        /// <c>null</c>.</exception>
        void StopCoroutine(Coroutine coroutine);
    }
}
