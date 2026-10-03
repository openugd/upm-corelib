using System;
using System.Collections.Generic;
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
            var calls = CallsMadeBy(Target(typeof(ContextBehaviour), nameof(ICoroutineProvider.StartCoroutine)));

            CollectionAssert.DoesNotContain(calls.Select(m => m.DeclaringType), typeof(MonoBehaviour),
                "MonoBehaviour.StartCoroutine returns null for an inactive GameObject, which the interface forbids");
            Assert.IsTrue(calls.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Start"),
                "the interface goes through the one implementation of the contract CoroutineProvider uses");
        }

        [Test]
        public void ContextBehaviour_StopCoroutine_ThroughTheInterface_ToleratesADestroyedHost()
        {
            var calls = CallsMadeBy(Target(typeof(ContextBehaviour), nameof(ICoroutineProvider.StopCoroutine)));

            Assert.IsTrue(calls.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Stop"));
        }

        [Test]
        public void CoroutineProvider_GoesThroughTheSameImplementation()
        {
            var start = CallsMadeBy(Target(typeof(CoroutineProvider), nameof(ICoroutineProvider.StartCoroutine)));
            var stop = CallsMadeBy(Target(typeof(CoroutineProvider), nameof(ICoroutineProvider.StopCoroutine)));

            Assert.IsTrue(start.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Start"));
            Assert.IsTrue(stop.Any(m => m.DeclaringType?.Name == "CoroutineHost" && m.Name == "Stop"));
        }

        private static MethodInfo Target(Type type, string member)
        {
            var map = type.GetInterfaceMap(typeof(ICoroutineProvider));
            var index = Array.FindIndex(map.InterfaceMethods, m => m.Name == member);
            return map.TargetMethods[index];
        }

        // The methods a method body calls, read from its IL: every call or callvirt operand that is a method token.
        private static List<MethodBase> CallsMadeBy(MethodInfo method)
        {
            const byte call = 0x28, callvirt = 0x6F;
            var il = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
            var calls = new List<MethodBase>();
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != call && il[i] != callvirt) continue;

                var token = BitConverter.ToInt32(il, i + 1);
                var table = token >> 24;
                if (table != 0x06 && table != 0x0A && table != 0x2B) continue; // MethodDef, MemberRef, MethodSpec

                try
                {
                    calls.Add(method.Module.ResolveMethod(token));
                    i += 4;
                }
                catch (ArgumentException)
                {
                    // not a method token after all: an operand byte that happened to look like an opcode
                }
            }

            return calls;
        }
    }
}
