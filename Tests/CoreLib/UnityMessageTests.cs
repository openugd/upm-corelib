using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenUGD.Core;
using OpenUGD.Presenters;
using OpenUGD.Utils.Components;

namespace OpenUGD.Tests
{
    // CC-6, UH-9: Unity finds a message method by name, so a private Update in a base class is silently replaced by
    // a subclass's own Update, and the base's work — firing a signal, ending a scope — stops with no warning. As
    // protected virtual methods, a subclass declaring one without `override` gets CS0114 instead. This pins the
    // exact set of messages each boundary class handles, and their shape. Reflection only: no Unity runtime.
    [TestFixture]
    public class UnityMessageTests
    {
        private const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly string[] UnityMessages =
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy",
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit"
        };

        [Test]
        public void ContextBehaviour_HandlesItsEightMessages_AsProtectedVirtual() =>
            AssertMessages(typeof(ContextBehaviour), "Awake", "Update", "FixedUpdate", "LateUpdate",
                "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit", "OnDestroy");

        [Test]
        public void ViewBehaviour_HandlesAwakeAndOnDestroy_AsProtectedVirtual() =>
            AssertMessages(typeof(ViewBehaviour), "Awake", "OnDestroy");

        [Test]
        public void SignalMonoBehaviour_HandlesItsFiveMessages_AsProtectedVirtual() =>
            AssertMessages(typeof(SignalMonoBehaviour), "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy");

        [Test]
        public void ViewBehaviour_OnAwake_IsGone_AndLifetimeIsPublic()
        {
            Assert.IsNull(typeof(ViewBehaviour).GetMethod("OnAwake", Declared),
                "override Awake and call base.Awake() instead: one way to hook Awake");

            var lifetime = typeof(ViewBehaviour).GetProperty("Lifetime", Declared);
            Assert.IsNotNull(lifetime);
            Assert.IsTrue(lifetime.GetMethod.IsPublic, "a presenter must be able to bind to the view's end (CC-5)");
        }

        [Test]
        public void ContextBehaviour_PersistAcrossScenes_IsAnOverridableProperty()
        {
            var property = typeof(ContextBehaviour).GetProperty("PersistAcrossScenes", Declared);

            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(bool), property.PropertyType);
            Assert.IsTrue(property.GetMethod.IsFamily && property.GetMethod.IsVirtual);
        }

        private static void AssertMessages(Type type, params string[] expected)
        {
            var declared = type.GetMethods(Declared).Where(m => UnityMessages.Contains(m.Name)).ToList();

            CollectionAssert.AreEquivalent(expected, declared.Select(m => m.Name));
            foreach (var method in declared)
            {
                Assert.IsTrue(method.IsFamily, $"{type.Name}.{method.Name} must be protected");
                Assert.IsTrue(method.IsVirtual && !method.IsFinal, $"{type.Name}.{method.Name} must be virtual");
                Assert.AreEqual(typeof(void), method.ReturnType);
            }
        }
    }
}
