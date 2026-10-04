using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenUGD.Core;
using UnityEngine;

namespace OpenUGD.Tests
{
    // With domain reload disabled, Lifetime.Eternal survives from one play session to the next
    // with everything nested in it. PlaySession is the scope that ends with the session and starts afresh; the
    // Unity callbacks that start and end it are stood in for by Begin and End. No Unity runtime needed.
    [TestFixture]
    public class PlaySessionTests
    {
        [SetUp]
        public void SetUp() => PlaySession.Begin();

        [TearDown]
        public void TearDown() => PlaySession.Begin();

        [Test]
        public void Lifetime_IsAlive_AndIsNotEternal()
        {
            Assert.IsFalse(PlaySession.Lifetime.IsTerminated);
            Assert.AreNotSame(Lifetime.Eternal, PlaySession.Lifetime);
        }

        [Test]
        public void End_TerminatesEverythingNestedInTheSession()
        {
            var context = PlaySession.Lifetime.DefineNested("a context rooted in the session");
            var leftover = context.Lifetime.DefineNested("something nested under it");

            PlaySession.End();

            Assert.IsTrue(context.IsTerminated);
            Assert.IsTrue(leftover.IsTerminated, "nothing nested in the session survives into the next one");
        }

        [Test]
        public void Begin_StartsAFreshSession()
        {
            var first = PlaySession.Lifetime;
            PlaySession.End();

            PlaySession.Begin();

            Assert.AreNotSame(first, PlaySession.Lifetime);
            Assert.IsFalse(PlaySession.Lifetime.IsTerminated, "the next session starts clean");
        }

        [Test]
        public void Begin_EndsASessionThatWasNeverEnded()
        {
            // Quitting is not raised on a crash or a kill, and the edit-mode session is never ended by quitting.
            var scope = PlaySession.Lifetime.DefineNested();

            PlaySession.Begin();

            Assert.IsTrue(scope.IsTerminated);
        }

        [Test]
        public void BetweenSessions_AScopeIsBornTerminated_RatherThanSurvivingIntoTheNextSession()
        {
            PlaySession.End();

            var lateScope = PlaySession.Lifetime.DefineNested("created during shutdown");
            PlaySession.Begin();

            Assert.IsTrue(lateScope.IsTerminated);
        }

        [Test]
        public void EndAndForget_EndsTheSession_AndTheNextReadOpensAFreshOne()
        {
            var scope = PlaySession.Lifetime.DefineNested();

            PlaySession.EndAndForget();

            Assert.IsTrue(scope.IsTerminated);
            Assert.IsFalse(PlaySession.Lifetime.IsTerminated, "edit mode gets a session of its own");
        }

        [Test]
        public void TheSessionStartsAtSubsystemRegistration()
        {
            // The first point Unity runs code in a play session, before any scene object wakes, and the one that runs
            // when entering play mode without a domain reload.
            var hooks = typeof(PlaySession)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .SelectMany(m => m.GetCustomAttributesData())
                .Where(a => a.AttributeType == typeof(RuntimeInitializeOnLoadMethodAttribute))
                .ToList();

            Assert.AreEqual(1, hooks.Count);
            Assert.AreEqual((int)RuntimeInitializeLoadType.SubsystemRegistration,
                (int)hooks[0].ConstructorArguments.Single().Value);
        }

        [Test]
        public void NothingInTheUnityBoundary_RootsAScopeInEternal_ExceptThePlaySessionItself()
        {
            var eternal = typeof(Lifetime).GetField(nameof(Lifetime.Eternal));
            var offenders = typeof(PlaySession).Assembly.GetTypes()
                .Where(t => t.DeclaringType == null && t != typeof(PlaySession))
                .SelectMany(ILCalls.Bodies)
                .Where(body => ILCalls.StaticFieldsLoadedBy(body).Contains(eternal))
                .Select(body => body.DeclaringType + "." + body.Name)
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "a component scope on Eternal outlives the play session when domain reload is off; use PlaySession");
        }
    }
}
