using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace OpenUGD.Commands
{
    // Answers, at registration, the question Context.Instantiate would otherwise answer on the first Tell: can this
    // command be built here? It applies Instantiate's rules - the one [Inject] constructor if there is one, else the
    // widest satisfiable public constructor; each parameter takes the first unused offered argument it can hold,
    // else a service resolved from the context - to the argument types Tell offers: the message, the registration's
    // Lifetime.Definition and the execution's Lifetime. A context's registrations are fixed once it is built, so the
    // answer cannot change afterwards.
    internal static class CommandConstructors
    {
        private const BindingFlags Constructors = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Null when commandType can be built from these arguments and this context; otherwise why not.
        internal static string DescribeUnsatisfiable(Context context,
            [DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType, Type messageType)
        {
            var offered = new[] { messageType, typeof(Lifetime.Definition), typeof(Lifetime) };

            // Widest first, as Context.Instantiate tries them.
            var constructors = commandType.GetConstructors(Constructors);
            Array.Sort(constructors, (a, b) => b.GetParameters().Length.CompareTo(a.GetParameters().Length));

            ConstructorInfo marked = null;
            var markedCount = 0;
            foreach (var constructor in constructors)
            {
                if (constructor.GetCustomAttribute<InjectAttribute>(false) == null) continue;
                marked = constructor;
                markedCount++;
            }

            if (markedCount > 1)
                return "it has " + markedCount + " constructors marked [Inject]; exactly one may be marked.";
            if (marked != null)
            {
                var missing = Unresolvable(context, marked, offered);
                return missing == null ? null : "its [Inject] constructor cannot be satisfied: " + missing;
            }

            string first = null;
            var anyPublic = false;
            foreach (var constructor in constructors)
            {
                if (!constructor.IsPublic) continue;
                anyPublic = true;

                var missing = Unresolvable(context, constructor, offered);
                if (missing == null) return null;
                first = first ?? missing;
            }

            return anyPublic
                ? "no public constructor can be satisfied: " + first
                : "it has no public instance constructor, and none is marked [Inject]. Under IL2CPP stripping, a " +
                  "type the linker cannot trace loses its constructors: put [Inject] on the one to keep.";
        }

        // Null when every parameter is satisfied; otherwise the first that is not.
        private static string Unresolvable(Context context, ConstructorInfo constructor, Type[] offered)
        {
            var used = new bool[offered.Length];
            foreach (var parameter in constructor.GetParameters())
            {
                var contract = parameter.ParameterType;
                var matched = false;
                for (var i = 0; i < offered.Length; i++)
                {
                    if (used[i] || !contract.IsAssignableFrom(offered[i])) continue;
                    used[i] = true;
                    matched = true;
                    break;
                }

                if (matched || context.TryResolve(contract, out _)) continue;

                return "nothing is registered for '" + contract + "' (constructor parameter '" + parameter.Name +
                       "'), and it is not the message, a Lifetime.Definition or a Lifetime.";
            }

            return null;
        }
    }
}
