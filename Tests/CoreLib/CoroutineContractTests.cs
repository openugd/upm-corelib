using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenUGD.Core;
using OpenUGD.Utils;
using UnityEngine;

namespace OpenUGD.Tests
{
    // CC-8: ICoroutineProvider must throw when it cannot start a coroutine and never return null. ContextBehaviour
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

        private static MethodInfo Target(Type type, string member)
        {
            var map = type.GetInterfaceMap(typeof(ICoroutineProvider));
            var index = Array.FindIndex(map.InterfaceMethods, m => m.Name == member);
            return map.TargetMethods[index];
        }
    }
}
