using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace OpenUGD.Commands.Tests
{
    /// <summary>
    /// The contract with Unity's linker, checked by reflection so that level 1 catches a regression without
    /// running the linker. A command is built by <c>Context.Instantiate</c>, by reflection, from a type the
    /// mapper stored at registration; nothing else in a player names its constructor. Every entry point a
    /// command type passes through therefore carries <c>[DynamicallyAccessedMembers]</c> for its
    /// constructors, or a stripped player loses them and every <c>Tell</c> fails. The linker matches the
    /// attribute by name, so these tests do too.
    /// </summary>
    [TestFixture]
    public class CommandStrippingAnnotationTests
    {
        private const string Dam = "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute";

        // PublicParameterlessConstructor | PublicConstructors | NonPublicConstructors, the .NET values, which is
        // what Context.Instantiate's own parameter asks for.
        private const int Constructors = 0x0001 | 0x0002 | 0x0004;

        [Test]
        public void ICommandMapperRegisterCommand_KeepsTheConstructorsOfTheTypeItIsGiven()
        {
            var method = typeof(ICommandMapper).GetMethod(nameof(ICommandMapper.RegisterCommand),
                new[] { typeof(Type), typeof(bool) });

            AssertKeepsConstructors(method.GetParameters()[0].GetCustomAttributesData(), method.ToString());
        }

        [Test]
        public void CommandMapperRegisterCommand_KeepsTheConstructorsOfTheTypeItIsGiven()
        {
            // An implementation must repeat the interface's annotation, or the linker reports a mismatch.
            var method = typeof(CommandMapper).GetMethod(nameof(CommandMapper.RegisterCommand),
                new[] { typeof(Type), typeof(bool) });

            AssertKeepsConstructors(method.GetParameters()[0].GetCustomAttributesData(), method.ToString());
        }

        [TestCase(nameof(CommandMapperExtensions.RegisterCommand), 1)]
        [TestCase(nameof(CommandMapperExtensions.Map), 2)]
        public void GenericEntryPoints_KeepTheConstructorsOfTheirCommandTypeArgument(string name, int arity)
        {
            var method = typeof(CommandMapperExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == name && m.GetGenericArguments().Length == arity);
            var command = method.GetGenericArguments().Single(t => t.Name == "TCommand");

            AssertKeepsConstructors(command.GetCustomAttributesData(), method + ", " + command);
        }

        [Test]
        public void TheStoredCommandType_KeepsItsAnnotation_SoTheLinkerCanFollowItToInstantiate()
        {
            var field = typeof(CommandMapper).GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                .Single(f => f.FieldType == typeof(Type));

            AssertKeepsConstructors(field.GetCustomAttributesData(), field.DeclaringType + "." + field.Name);
        }

        private static void AssertKeepsConstructors(IList<CustomAttributeData> data, string where)
        {
            var dam = data.SingleOrDefault(a => a.AttributeType.FullName == Dam);

            Assert.IsNotNull(dam, where + " has no [DynamicallyAccessedMembers].");
            var value = Convert.ToInt32(dam.ConstructorArguments[0].Value);
            Assert.AreEqual(Constructors, value & Constructors,
                where + " must keep public and non-public constructors, as Context.Instantiate asks.");
        }
    }
}
