using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenUGD.Core;
using UnityEngine;

namespace OpenUGD.Tests
{
    // LS-14, UH-10, CC-27: a component's scope is created in Awake and nowhere earlier, because Unity never sends
    // OnDestroy to an object that was never activated. A property getter that creates the scope on first read - the
    // way SignalMonoBehaviour's signals did - leaves it behind on such an object. Read from the IL, so level 1 checks
    // it; UnityBoundaryPlayModeTests checks the behaviour in the engine.
    [TestFixture]
    public class ComponentScopeTests
    {
        [Test]
        public void NoPropertyOfAComponent_CreatesAScope()
        {
            var assembly = typeof(LifetimeBehaviour).Assembly;
            var defineNested = typeof(Lifetime).GetMethod(nameof(Lifetime.DefineNested));
            var components = assembly.GetTypes().Where(t => typeof(MonoBehaviour).IsAssignableFrom(t)).ToList();
            CollectionAssert.IsNotEmpty(components);

            var offenders = (
                from type in components
                from property in type.GetProperties(BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.DeclaredOnly)
                let getter = property.GetMethod
                where getter != null
                from call in ILCalls.CallsMadeBy(getter)
                // Defining a scope, directly or through ComponentScope.Define, or through a helper of the component
                // itself (the Initialize-on-first-read pattern). Exceptions built for "read before Awake" are fine.
                where call == defineNested
                      || (call.DeclaringType?.Name == "ComponentScope" && call.Name == "Define")
                      || (call.DeclaringType != null && call.DeclaringType.Assembly == assembly &&
                          call.DeclaringType.IsAssignableFrom(type) && !call.IsSpecialName && !call.IsConstructor)
                select $"{type.Name}.{property.Name} calls {call.DeclaringType?.Name}.{call.Name}").ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void LifetimeBehaviour_IsSealed_OneToAGameObject_WithPrivateMessages()
        {
            var type = typeof(LifetimeBehaviour);

            Assert.IsTrue(type.IsSealed, "sealed, so no subclass can hide Awake or OnDestroy");
            Assert.IsTrue(type.GetCustomAttributesData().Any(a => a.AttributeType == typeof(DisallowMultipleComponent)),
                "one scope per GameObject");
            foreach (var name in new[] { "Awake", "OnDestroy" })
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, name);
                Assert.IsTrue(method.IsPrivate, name);
            }
        }

        [Test]
        public void GetLifetime_IsAnExtensionOnGameObjectAndOnComponent()
        {
            var methods = typeof(GameObjectLifetimeExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == nameof(GameObjectLifetimeExtensions.GetLifetime))
                .Select(m => m.GetParameters().Single().ParameterType)
                .ToList();

            CollectionAssert.AreEquivalent(new[] { typeof(GameObject), typeof(Component) }, methods);
            Assert.AreEqual(typeof(Lifetime),
                typeof(GameObjectLifetimeExtensions).GetMethod(nameof(GameObjectLifetimeExtensions.GetLifetime),
                    new[] { typeof(GameObject) })?.ReturnType);
        }
    }
}
