using System;
using NUnit.Framework;

namespace OpenUGD.Logging.Tests
{
    // CC-14: UseUnityConsole was documented as throwing NullReferenceException for a null argument, "neither is
    // checked" - a defect written up as behaviour. It checks its arguments now. No record is written, so nothing
    // reaches UnityEngine.Debug and no Unity runtime is needed.
    [TestFixture]
    public class UnityLogSinkTests
    {
        [Test]
        public void UseUnityConsole_RejectsANullRoot()
        {
            var definition = Lifetime.Eternal.DefineNested();
            try
            {
                var thrown = Assert.Throws<ArgumentNullException>(
                    () => UnityLogSinkExtensions.UseUnityConsole(null, definition.Lifetime));
                Assert.AreEqual("logger", thrown.ParamName);
            }
            finally
            {
                definition.Terminate();
            }
        }

        [Test]
        public void UseUnityConsole_RejectsANullLifetime()
        {
            var thrown = Assert.Throws<ArgumentNullException>(() => new LogRoot().UseUnityConsole(null));

            Assert.AreEqual("lifetime", thrown.ParamName);
        }
    }
}
