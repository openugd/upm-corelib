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

            // Not isActiveAndEnabled: Unity reports it false until the behaviour's OnEnable, so it would refuse the
            // coroutines started from Awake - a ContextBehaviour's whole synchronous boot runs there - which Unity
            // itself runs. Once OnEnable has run, the two checks agree.
            if (!host.enabled || !host.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    $"Cannot start a coroutine on '{host.name}': the host is inactive or disabled. " +
                    "Unity only runs coroutines on an active GameObject with an enabled Behaviour.");
            }

            var coroutine = host.StartCoroutine(new HandleKeeper(enumerator));

            // Backstop: any other reason Unity declines - it returns null rather than throwing. A first step that
            // throws ends here too, after Unity has logged the exception.
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

        // Unity returns null, not a Coroutine, for a coroutine whose first step is also its last - one that finishes
        // inside StartCoroutine. The interface promises a handle, and the body has run, so such a coroutine is kept
        // alive one more, empty, frame: Unity hands back a handle, and stopping it or waiting on it behaves as for any
        // coroutine that has just finished. A body that is still running after its first step passes through as is.
        internal sealed class HandleKeeper : IEnumerator, IDisposable
        {
            private IEnumerator _body;
            private bool _firstStep = true;

            internal HandleKeeper(IEnumerator body) => _body = body;

            public object Current { get; private set; }

            public bool MoveNext()
            {
                if (_body == null) return false;

                var first = _firstStep;
                _firstStep = false;

                if (_body.MoveNext())
                {
                    Current = _body.Current;
                    return true;
                }

                Dispose();
                Current = null;
                return first;
            }

            public void Reset() => throw new NotSupportedException("A coroutine body cannot be reset.");

            public void Dispose()
            {
                var body = _body;
                _body = null;
                (body as IDisposable)?.Dispose();
            }
        }
    }
}
