using System;
using System.Linq;
using NUnit.Framework;

namespace OpenUGD.Commands.Tests
{
    // com.openugd.commands is engine-free: its assembly definition sets noEngineReferences, and this pins what
    // the compiled assembly actually uses, so turning the flag off and reaching for UnityEngine fails here.
    [TestFixture]
    public class CommandsAssemblyTests
    {
        [Test]
        public void Assembly_UsesNoUnityAssembly_AndOnlyLifetimeAndContextFromTheFamily()
        {
            var references = typeof(CommandMap).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            CollectionAssert.IsEmpty(references.Where(n => n.StartsWith("Unity", StringComparison.Ordinal)));
            CollectionAssert.IsSubsetOf(
                references.Where(n => n.StartsWith("com.openugd", StringComparison.Ordinal)),
                new[] { "com.openugd.lifetime", "com.openugd.context" });
        }
    }
}
