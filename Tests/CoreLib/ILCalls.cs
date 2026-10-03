using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpenUGD.Tests
{
    // Reads what a method body calls and which static fields it loads, from its IL. Lets level 1, which has no Unity
    // runtime, pin what Unity-bound code is wired to.
    internal static class ILCalls
    {
        private const byte Call = 0x28, Callvirt = 0x6F, Ldsfld = 0x7E;

        internal static List<MethodBase> CallsMadeBy(MethodBase method)
        {
            var calls = new List<MethodBase>();
            foreach (var token in Operands(method, Call, Callvirt))
            {
                var table = token >> 24;
                if (table != 0x06 && table != 0x0A && table != 0x2B) continue; // MethodDef, MemberRef, MethodSpec

                try
                {
                    calls.Add(method.Module.ResolveMethod(token));
                }
                catch (ArgumentException)
                {
                    // an operand byte that happened to look like an opcode
                }
            }

            return calls;
        }

        internal static List<FieldInfo> StaticFieldsLoadedBy(MethodBase method)
        {
            var fields = new List<FieldInfo>();
            foreach (var token in Operands(method, Ldsfld))
            {
                var table = token >> 24;
                if (table != 0x04 && table != 0x0A) continue; // FieldDef, MemberRef

                try
                {
                    fields.Add(method.Module.ResolveField(token));
                }
                catch (ArgumentException)
                {
                }
            }

            return fields;
        }

        // Every method and constructor body of a type and of its nested types (lambdas, iterators and async state
        // machines live in compiler-generated nested types).
        internal static IEnumerable<MethodBase> Bodies(Type type)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                     BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all)) yield return method;
            foreach (var constructor in type.GetConstructors(all)) yield return constructor;
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var body in Bodies(nested))
                yield return body;
        }

        private static IEnumerable<int> Operands(MethodBase method, params byte[] opcodes)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null) yield break;

            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (Array.IndexOf(opcodes, il[i]) < 0) continue;
                yield return BitConverter.ToInt32(il, i + 1);
            }
        }
    }
}
