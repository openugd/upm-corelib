using System;
using System.Linq;
using NUnit.Framework;

namespace OpenUGD.Presenters.Tests
{
    // com.openugd.presenters is engine-free and container-free: its assembly definition sets noEngineReferences
    // and references only com.openugd.lifetime, and this pins what the compiled assembly actually uses, so
    // reaching for UnityEngine or for com.openugd.context again fails here.
    [TestFixture]
    public class PresentersAssemblyTests
    {
        [Test]
        public void Assembly_UsesNoUnityAssembly_AndOnlyLifetimeFromTheFamily()
        {
            var references = typeof(Presenter).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            CollectionAssert.IsEmpty(references.Where(n => n.StartsWith("Unity", StringComparison.Ordinal)));
            CollectionAssert.IsSubsetOf(
                references.Where(n => n.StartsWith("com.openugd", StringComparison.Ordinal)),
                new[] { "com.openugd.lifetime" },
                "construction and injection go through IPresenterFactory; the container adapter lives in corelib");
        }
    }
}
