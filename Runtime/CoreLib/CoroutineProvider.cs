using System;
using System.Collections;
using UnityEngine;

namespace OpenUGD.Utils
{
    /// <summary>
    /// The default <see cref="ICoroutineProvider"/>: an adapter that runs coroutines on a
    /// <see cref="MonoBehaviour"/> you already have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this when the host <see cref="MonoBehaviour"/> is not yours to modify. When it is, implement
    /// <see cref="ICoroutineProvider"/> on it directly — <see cref="MonoBehaviour"/>'s own
    /// <c>StartCoroutine</c>/<c>StopCoroutine</c> already satisfy the interface, so it costs one base-list
    /// entry and no code.
    /// </para>
    /// <para>
    /// <b>Breaking changes in 2.0.0.</b> <see cref="StartCoroutine"/> now throws instead of returning
    /// <c>null</c> when the host cannot run coroutines, and the host check is
    /// <see cref="Behaviour.isActiveAndEnabled"/> rather than <c>gameObject.activeSelf</c> — the old check
    /// read only the host's own flag and ignored its parents, so a host under a deactivated parent passed the
    /// check and then threw from inside Unity. The class is also <c>sealed</c>.
    /// </para>
    /// </remarks>
    public sealed class CoroutineProvider : ICoroutineProvider
    {
        private readonly MonoBehaviour _monoBehaviour;

        /// <summary>
        /// Creates a provider that runs coroutines on <paramref name="monoBehaviour"/>.
        /// </summary>
        /// <param name="monoBehaviour">The host. Must not be <c>null</c> or destroyed.</param>
        /// <exception cref="ArgumentNullException"><paramref name="monoBehaviour"/> is <c>null</c> or
        /// destroyed.</exception>
        public CoroutineProvider(MonoBehaviour monoBehaviour)
        {
            // Unity's overloaded operator== reports a destroyed object as null, which is what we want here.
            if (monoBehaviour == null)
            {
                throw new ArgumentNullException(nameof(monoBehaviour), "Host MonoBehaviour cannot be null or destroyed.");
            }

            _monoBehaviour = monoBehaviour;
        }

        /// <inheritdoc/>
        public Coroutine StartCoroutine(IEnumerator enumerator)
        {
            if (enumerator == null)
            {
                throw new ArgumentNullException(nameof(enumerator), "Coroutine body cannot be null.");
            }

            if (_monoBehaviour == null)
            {
                throw new InvalidOperationException(
                    "Cannot start a coroutine: the host MonoBehaviour has been destroyed.");
            }

            if (!_monoBehaviour.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"Cannot start a coroutine on '{_monoBehaviour.name}': the host is inactive or disabled. " +
                    "Unity only runs coroutines on an active GameObject with an enabled Behaviour.");
            }

            return _monoBehaviour.StartCoroutine(enumerator);
        }

        /// <inheritdoc/>
        public void StopCoroutine(Coroutine coroutine)
        {
            if (coroutine == null)
            {
                throw new ArgumentNullException(nameof(coroutine), "Coroutine cannot be null.");
            }

            // A destroyed host has already stopped everything it was running, so the postcondition holds.
            if (_monoBehaviour == null)
            {
                return;
            }

            _monoBehaviour.StopCoroutine(coroutine);
        }
    }
}
