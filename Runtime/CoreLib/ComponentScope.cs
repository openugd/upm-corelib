using System;
using UnityEngine;

namespace OpenUGD.Core
{
    // The rules for a component's scope, in one place, for every component in this assembly that owns one:
    // LifetimeBehaviour, ViewBehaviour and SignalMonoBehaviour through these two members, ContextBehaviour by the
    // same rules with the extra steps of a rebuildable scope.
    //
    // 1. Created in Awake, and nowhere earlier - not in a field initializer, a constructor or a property getter.
    //    Unity sends OnDestroy only to a component that has been awake, so a scope created before Awake on an object
    //    that is never activated is never ended.
    // 2. Nested in PlaySession.Lifetime, so a scope Unity never ends - its OnDestroy skipped by a subclass, say - is
    //    ended with the session instead of living on Lifetime.Eternal.
    // 3. Ended in OnDestroy.
    // 4. Read before Awake, an InvalidOperationException that says why, rather than null or a scope nothing ends.
    internal static class ComponentScope
    {
        internal static Lifetime.Definition Define(Component owner) => PlaySession.Lifetime.DefineNested(owner.name);

        // Uses no Unity API, so it is safe to build on an object Unity has never initialised.
        internal static InvalidOperationException NotAwake(Component owner, string member) =>
            new InvalidOperationException(
                $"{owner.GetType().Name}.{member} was read before Awake() ran, so there is no scope yet. Unity runs " +
                "Awake when the GameObject is first active, and never sends OnDestroy to a component that was " +
                "never awake, so a scope created any earlier could never end. Activate the GameObject first, or " +
                "read this from Start. If a subclass overrides Awake, it must call base.Awake().");
    }
}
