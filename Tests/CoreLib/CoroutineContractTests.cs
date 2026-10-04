using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenUGD.Core;
using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Tests
{
    // ICoroutineProvider must throw when it cannot start a coroutine and never return null. ContextBehaviour
    // used to satisfy the interface with MonoBehaviour's inherited StartCoroutine — the compiler bridges the
    // interface to it with a private stub — and that method returns null for an inactive GameObject. Without the
    // engine, the implementation is checked by what it calls; UnityBoundaryPlayModeTests checks what it does.
    [TestFixture]
    public class CoroutineContractTests
    {
        [Test]
        public void ContextBehaviour_StartCoroutine_ThroughTheInterface_DoesNotForwardToMonoBehaviour()
        {
            var calls = ILCalls.CallsMadeBy(Target(typeof(ContextBehaviour), nameof(ICoroutineProvider.StartCoroutine)));

            CollectionAssert.DoesNotContain(calls.Select(m => m.DeclaringType), typeof(MonoBehaviour),
                "MonoBehaviour.StartCoroutine returns null for an inactive GameObject, which the interface forbids");
            Assert.IsTrue(calls.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Start"),
                "the interface goes through the one implementation of the contract CoroutineProvider uses");
        }

        [Test]
        public void ContextBehaviour_StopCoroutine_ThroughTheInterface_ToleratesADestroyedHost()
        {
            var calls = ILCalls.CallsMadeBy(Target(typeof(ContextBehaviour), nameof(ICoroutineProvider.StopCoroutine)));

            Assert.IsTrue(calls.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Stop"));
        }

        [Test]
        public void CoroutineProvider_GoesThroughTheSameImplementation()
        {
            var start = ILCalls.CallsMadeBy(Target(typeof(CoroutineProvider), nameof(ICoroutineProvider.StartCoroutine)));
            var stop = ILCalls.CallsMadeBy(Target(typeof(CoroutineProvider), nameof(ICoroutineProvider.StopCoroutine)));

            Assert.IsTrue(start.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Start"));
            Assert.IsTrue(stop.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Stop"));
        }

        // Behaviour.isActiveAndEnabled is Unity's IsAddedToManager, false until OnEnable, so a check on it refused
        // coroutines started from Awake - where ContextBehaviour runs its boot, services included -
        // although Unity runs them. Enabled and active in the hierarchy is what it means once OnEnable has run.
        [Test]
        public void CoroutineHost_ChecksEnabledAndActiveInHierarchy_NotIsActiveAndEnabled_WhichIsFalseInAwake()
        {
            var start = typeof(CoroutineHost).GetMethod(nameof(CoroutineHost.Start),
                BindingFlags.Static | BindingFlags.NonPublic);
            var calls = ILCalls.CallsMadeBy(start).Select(m => m.DeclaringType?.Name + "." + m.Name).ToList();

            CollectionAssert.DoesNotContain(calls, "Behaviour.get_isActiveAndEnabled");
            CollectionAssert.Contains(calls, "Behaviour.get_enabled");
            CollectionAssert.Contains(calls, "GameObject.get_activeInHierarchy");
        }

        // Unity's StartCoroutine returns null for a coroutine whose first step is its last, so the "never null"
        // backstop threw for a body that had run in full. The body is wrapped so that it gets a handle.
        [Test]
        public void HandleKeeper_ABodyThatEndsInItsFirstStep_RunsInFull_ThenWaitsOneEmptyStep()
        {
            var ran = 0;
            var keeper = new CoroutineHost.HandleKeeper(Body(() => ran++));

            Assert.IsTrue(keeper.MoveNext(), "still running after the first step, so Unity hands back a handle");
            Assert.AreEqual(1, ran, "the body has run in full");
            Assert.IsNull(keeper.Current, "the extra step waits one frame and yields nothing of the body's");
            Assert.IsFalse(keeper.MoveNext());
            Assert.IsFalse(keeper.MoveNext());
            Assert.AreEqual(1, ran);
        }

        [Test]
        public void HandleKeeper_ABodyThatYields_PassesThroughUnchanged()
        {
            var first = new object();
            var second = new object();
            var keeper = new CoroutineHost.HandleKeeper(Body(() => { }, first, second));

            Assert.IsTrue(keeper.MoveNext());
            Assert.AreSame(first, keeper.Current);
            Assert.IsTrue(keeper.MoveNext());
            Assert.AreSame(second, keeper.Current);
            Assert.IsFalse(keeper.MoveNext(), "no extra step for a body that was still running after its first");
        }

        [Test]
        public void HandleKeeper_AStepThatThrows_Propagates_ForUnityToLog()
        {
            var failure = new InvalidOperationException("step failed");
            var keeper = new CoroutineHost.HandleKeeper(Body(() => throw failure));

            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => keeper.MoveNext()));
        }

        private static System.Collections.IEnumerator Body(Action atTheEnd, params object[] yields)
        {
            foreach (var value in yields) yield return value;
            atTheEnd();
        }

        private static MethodInfo Target(Type type, string member)
        {
            var map = type.GetInterfaceMap(typeof(ICoroutineProvider));
            var index = Array.FindIndex(map.InterfaceMethods, m => m.Name == member);
            return map.TargetMethods[index];
        }
    }
}
