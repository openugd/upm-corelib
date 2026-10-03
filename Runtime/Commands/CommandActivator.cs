using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace OpenUGD.Commands
{
    // Builds the commands of one type registration. Everything Context.Instantiate would work out on every call is
    // worked out once, here, when the command is registered: which constructor (the one [Inject] constructor if
    // there is one, else the widest satisfiable public one, and two equally wide satisfiable ones are an error);
    // where each of its arguments comes from (the first unused value Tell offers that it can hold - the message,
    // the registration's Lifetime.Definition, the execution's Lifetime - else a service of the context); and
    // whether the type has [Inject] members, each of which must be satisfiable from the context. A context's
    // registrations are fixed once it is built, so the answers cannot change afterwards.
    //
    // Build then touches no type metadata: it fills the argument array, calls the cached constructor and, only for a
    // type that has [Inject] members, hands the instance to Context.Inject.
    internal sealed class CommandActivator
    {
        private const BindingFlags AllConstructors = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags DeclaredMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // Where a constructor argument comes from: an index into the values Tell offers, or the context.
        private const int FromMessage = 0;
        private const int FromRegistration = 1;
        private const int FromExecution = 2;
        private const int FromContext = -1;

        private static readonly object[] NoArguments = new object[0];

        private readonly Context _context;
        private readonly string _commandName;
        private readonly ConstructorInfo _constructor;
        private readonly int[] _sources;
        private readonly Type[] _contracts;
        private readonly bool _injectMembers;

        private CommandActivator(Context context, string commandName, ConstructorInfo constructor, int[] sources,
            Type[] contracts, bool injectMembers)
        {
            _context = context;
            _commandName = commandName;
            _constructor = constructor;
            _sources = sources;
            _contracts = contracts;
            _injectMembers = injectMembers;
        }

        // The activator for commandType, or null with the reason it cannot be built from these arguments and this
        // context. The parameter carries the annotation of the registration entry points, so the linker keeps the
        // constructors this reads (it is where the type stops flowing: only the ConstructorInfo is kept).
        internal static CommandActivator Create(Context context,
            [DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType, Type messageType, out string reason)
        {
            if (IsEngineObject(commandType))
            {
                reason = "it derives from UnityEngine.Object, which only Unity can create. Register a factory that " +
                         "returns the command instead.";
                return null;
            }

            var offered = new[] { messageType, typeof(Lifetime.Definition), typeof(Lifetime) };

            int[] sources = null;
            Type[] contracts = null;
            var constructor = ChooseConstructor(context, commandType, offered, ref sources, ref contracts, out reason);
            if (constructor == null) return null;

            bool hasMembers;
            reason = DescribeUnsatisfiableMembers(context, commandType, out hasMembers);
            if (reason != null) return null;

            return new CommandActivator(context, commandType.Name, constructor, sources, contracts, hasMembers);
        }

        // Builds one command. Exceptions are the caller's to collect: a constructor that throws is rethrown as
        // itself, with its own stack trace, as it would be from a factory that called it.
        internal ICommand Build(object message, Lifetime.Definition registration, Lifetime execution)
        {
            // As Context.Instantiate would: a disposed context builds nothing, even a command that needs no service.
            if (_context.Lifetime.IsTerminated)
            {
                throw new ObjectDisposedException(nameof(Context),
                    "'" + _commandName + "' is built from a context that has been disposed.");
            }

            var sources = _sources;
            var arguments = sources.Length == 0 ? NoArguments : new object[sources.Length];
            for (var i = 0; i < sources.Length; i++)
            {
                switch (sources[i])
                {
                    case FromMessage:
                        arguments[i] = message;
                        break;
                    case FromRegistration:
                        arguments[i] = registration;
                        break;
                    case FromExecution:
                        arguments[i] = execution;
                        break;
                    default:
                        object service;
                        if (!_context.TryResolve(_contracts[i], out service))
                        {
                            // Checked at registration; reachable only if the context lost the contract since.
                            throw new InvalidOperationException(
                                "'" + _commandName + "' needs '" + _contracts[i] + "', which its context no longer " +
                                "resolves.");
                        }

                        arguments[i] = service;
                        break;
                }
            }

            object instance;
            try
            {
                instance = _constructor.Invoke(arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }

            if (_injectMembers) _context.Inject(instance);
            return (ICommand)instance;
        }

        private static ConstructorInfo ChooseConstructor(Context context,
            [DynamicallyAccessedMembers(Trimming.Constructors)] Type commandType, Type[] offered, ref int[] sources,
            ref Type[] contracts, out string reason)
        {
            var constructors = commandType.GetConstructors(AllConstructors);
            var parameters = new ParameterInfo[constructors.Length][];
            for (var i = 0; i < constructors.Length; i++) parameters[i] = constructors[i].GetParameters();

            // Widest first, and stable, as Context.Instantiate orders them: equally wide constructors keep the order
            // reflection returned them in.
            for (var i = 1; i < constructors.Length; i++)
            {
                var constructor = constructors[i];
                var signature = parameters[i];
                var j = i - 1;
                for (; j >= 0 && parameters[j].Length < signature.Length; j--)
                {
                    constructors[j + 1] = constructors[j];
                    parameters[j + 1] = parameters[j];
                }

                constructors[j + 1] = constructor;
                parameters[j + 1] = signature;
            }

            var marked = -1;
            var markedCount = 0;
            for (var i = 0; i < constructors.Length; i++)
            {
                var attribute = (InjectAttribute)Attribute.GetCustomAttribute(constructors[i], typeof(InjectAttribute),
                    false);
                if (attribute == null) continue;

                if (attribute.Optional)
                {
                    reason = "a constructor is marked [Inject(Optional = true)], which has no meaning on a " +
                             "constructor; Optional applies to an injected field or property.";
                    return null;
                }

                marked = i;
                markedCount++;
            }

            if (markedCount > 1)
            {
                reason = "it has " + markedCount + " constructors marked [Inject]; exactly one may be marked.";
                return null;
            }

            string missing;
            if (marked >= 0)
            {
                if (TryPlan(context, parameters[marked], offered, out sources, out contracts, out missing))
                {
                    reason = null;
                    return constructors[marked];
                }

                reason = "its [Inject] constructor cannot be satisfied: " + missing;
                return null;
            }

            var chosen = -1;
            string firstMissing = null;
            var anyPublic = false;
            for (var i = 0; i < constructors.Length; i++)
            {
                if (!constructors[i].IsPublic) continue;
                anyPublic = true;
                if (chosen >= 0 && parameters[i].Length != parameters[chosen].Length) break;

                int[] candidateSources;
                Type[] candidateContracts;
                if (!TryPlan(context, parameters[i], offered, out candidateSources, out candidateContracts, out missing))
                {
                    firstMissing = firstMissing ?? missing;
                    continue;
                }

                if (chosen >= 0)
                {
                    reason = "it has two public constructors of " + parameters[i].Length + " parameters that can " +
                             "both be satisfied, so the choice is ambiguous. Mark the one to use with [Inject].";
                    return null;
                }

                chosen = i;
                sources = candidateSources;
                contracts = candidateContracts;
            }

            if (chosen >= 0)
            {
                reason = null;
                return constructors[chosen];
            }

            reason = anyPublic
                ? "no public constructor can be satisfied: " + firstMissing
                : "it has no public instance constructor, and none is marked [Inject]. Under IL2CPP stripping, a " +
                  "type the linker cannot trace loses its constructors: put [Inject] on the one to keep.";
            return null;
        }

        // Decides where each parameter's value comes from: the first unused offered value it can hold, else the
        // context. False, with the first parameter that has no source, when one cannot be satisfied.
        private static bool TryPlan(Context context, ParameterInfo[] parameters, Type[] offered, out int[] sources,
            out Type[] contracts, out string missing)
        {
            sources = new int[parameters.Length];
            contracts = new Type[parameters.Length];
            var used = new bool[offered.Length];

            for (var p = 0; p < parameters.Length; p++)
            {
                var contract = parameters[p].ParameterType;
                var source = FromContext;
                for (var i = 0; i < offered.Length; i++)
                {
                    if (used[i] || !contract.IsAssignableFrom(offered[i])) continue;
                    used[i] = true;
                    source = i;
                    break;
                }

                if (source == FromContext && !context.TryResolve(contract, out _))
                {
                    missing = "nothing is registered for '" + contract + "' (constructor parameter '" +
                              parameters[p].Name + "'), and it is not the message, a Lifetime.Definition or a Lifetime.";
                    sources = null;
                    contracts = null;
                    return false;
                }

                sources[p] = source;
                contracts[p] = source == FromContext ? contract : null;
            }

            missing = null;
            return true;
        }

        // Context.Inject's rules, checked now instead of on the first Tell: the [Inject] fields and properties of
        // the whole inheritance chain, each assignable, and each that is not optional resolvable from the context.
        private static string DescribeUnsatisfiableMembers(Context context, Type commandType, out bool hasMembers)
        {
            hasMembers = false;
            HashSet<string> properties = null;

            for (var current = commandType; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(DeclaredMembers))
                {
                    var attribute = InjectOf(field);
                    if (attribute == null) continue;

                    hasMembers = true;
                    var name = "[Inject] field '" + current.Name + "." + field.Name + "'";
                    if (field.IsInitOnly || field.IsLiteral) return "its " + name + " is readonly or const.";

                    var problem = DescribeMember(context, attribute, field.FieldType, name);
                    if (problem != null) return problem;
                }

                foreach (var property in current.GetProperties(DeclaredMembers))
                {
                    var attribute = InjectOf(property);
                    if (attribute == null) continue;

                    // Most-derived first: an override or a `new` declaration is the one injected.
                    if (!(properties ?? (properties = new HashSet<string>())).Add(property.Name)) continue;

                    hasMembers = true;
                    var name = "[Inject] property '" + current.Name + "." + property.Name + "'";
                    if (property.GetIndexParameters().Length != 0) return "its " + name + " is an indexer.";
                    if (property.GetSetMethod(true) == null) return "its " + name + " has no setter.";

                    var problem = DescribeMember(context, attribute, property.PropertyType, name);
                    if (problem != null) return problem;
                }
            }

            return null;
        }

        private static string DescribeMember(Context context, InjectAttribute attribute, Type contract, string name)
        {
            if (attribute.Optional)
            {
                return contract.IsValueType && Nullable.GetUnderlyingType(contract) == null
                    ? "its " + name + " is optional but of the non-nullable value type '" + contract +
                      "', so a missing service could not be told apart from an injected one."
                    : null;
            }

            if (context.TryResolve(contract, out _)) return null;

            return "nothing is registered for '" + contract + "' (" + name + "). [Inject] members are filled from " +
                   "the context only: take the message, the registration or the execution's Lifetime as a " +
                   "constructor parameter instead.";
        }

        private static InjectAttribute InjectOf(MemberInfo member) =>
            (InjectAttribute)Attribute.GetCustomAttribute(member, typeof(InjectAttribute), true);

        // By name: this assembly has no engine reference, so the type itself cannot be named.
        private static bool IsEngineObject(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.FullName == "UnityEngine.Object") return true;
            }

            return false;
        }
    }
}
