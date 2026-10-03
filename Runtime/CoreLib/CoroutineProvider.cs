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
    /// Use this to run coroutines on any <see cref="MonoBehaviour"/>, yours or not. Implementing
    /// <see cref="ICoroutineProvider"/> on a <see cref="MonoBehaviour"/> of your own is not free: its
    /// inherited <c>StartCoroutine</c> compiles against the interface but breaks the contract — Unity
    /// returns <c>null</c> for an inactive GameObject, after logging an error — so the type has to implement
    /// the interface explicitly, with the checks this class makes. <c>ContextBehaviour</c> does; anything
    /// else can hand out <c>new CoroutineProvider(this)</c> instead.
    /// </para>
    /// <para>
    /// <b>Breaking changes in 2.0.0.</b> <see cref="StartCoroutine"/> now throws instead of returning
    /// <c>null</c> when the host cannot run coroutines, and the host check is that the host is
    /// <see cref="Behaviour.enabled"/> and its GameObject <see cref="GameObject.activeInHierarchy"/>, rather
    /// than <c>gameObject.activeSelf</c> — the old check read only the host's own flag and ignored its parents,
    /// so a host under a deactivated parent passed the check and then threw from inside Unity. The check is not
    /// <see cref="Behaviour.isActiveAndEnabled"/>, which Unity reports <c>false</c> until the host's
    /// <c>OnEnable</c>, so it would refuse a coroutine started from <c>Awake</c>. The class is also
    /// <c>sealed</c>.
    /// </para>
    /// <para>
    /// <b>A wrapped body.</b> The coroutine Unity runs wraps <c>enumerator</c>, so that a body that finishes in
    /// its first step still gets a handle (see <see cref="ICoroutineProvider"/>). Stop it with
    /// <see cref="StopCoroutine"/> or <c>StopAllCoroutines</c>; <c>MonoBehaviour.StopCoroutine(IEnumerator)</c>
    /// with the original enumerator does not find it.
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
        public Coroutine StartCoroutine(IEnumerator enumerator) => CoroutineHost.Start(_monoBehaviour, enumerator);

        /// <inheritdoc/>
        public void StopCoroutine(Coroutine coroutine) => CoroutineHost.Stop(_monoBehaviour, coroutine);
    }
}
