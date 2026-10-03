using System;
using System.Collections;
using UnityEngine;

namespace OpenUGD.Utils
{
    // The ICoroutineProvider contract over a MonoBehaviour, written once: CoroutineProvider and ContextBehaviour both
    // implement the interface through here, so "throw, never return null" cannot drift between them.
    // MonoBehaviour.StartCoroutine itself returns null (and logs an error) for an inactive GameObject, which is exactly
    // the silent no-op the interface forbids.
    internal static class CoroutineHost
    {
        internal static Coroutine Start(MonoBehaviour host, IEnumerator enumerator)
        {
            if (enumerator == null)
            {
                throw new ArgumentNullException(nameof(enumerator), "Coroutine body cannot be null.");
            }

            // Unity's overloaded operator== reports a destroyed object as null.
            if (host == null)
            {
                throw new InvalidOperationException(
                    "Cannot start a coroutine: the host MonoBehaviour has been destroyed.");
            }

            if (!host.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"Cannot start a coroutine on '{host.name}': the host is inactive or disabled. " +
                    "Unity only runs coroutines on an active GameObject with an enabled Behaviour.");
            }

            var coroutine = host.StartCoroutine(enumerator);

            // Backstop: any other reason Unity declines — it returns null rather than throwing.
            if (coroutine == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not start the coroutine on '{host.name}' (MonoBehaviour.StartCoroutine returned " +
                    "null); see the console for Unity's reason.");
            }

            return coroutine;
        }

        internal static void Stop(MonoBehaviour host, Coroutine coroutine)
        {
            if (coroutine == null)
            {
                throw new ArgumentNullException(nameof(coroutine), "Coroutine cannot be null.");
            }

            // A destroyed host has already stopped everything it was running, so the postcondition holds.
            if (host == null)
            {
                return;
            }

            host.StopCoroutine(coroutine);
        }
    }
}
