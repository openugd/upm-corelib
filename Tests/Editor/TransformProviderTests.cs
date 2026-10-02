using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenUGD.Services.UI;
using UnityEngine;

namespace OpenUGD.Tests
{
    /// <summary>
    /// The layer seam. What matters: a layer is reached by key with no privileged set, each built-in
    /// accessor asks for its own key, and absence is reported rather than silently answered with the
    /// scene root.
    /// </summary>
    /// <remarks>
    /// The fake records the keys it is asked for and answers <c>false</c> to every lookup, so every
    /// assertion here runs without a Unity runtime. That is deliberate: constructing a real
    /// <c>RectTransform</c> needs the editor, and the part worth guarding is which key an accessor
    /// asks for, not that a one-line method returns what it was handed.
    /// </remarks>
    [TestFixture]
    public class TransformProviderTests
    {
        [Test]
        public void EveryBuiltInAccessorAsksForItsOwnKey()
        {
            // TransformProviderExtensions is eight near-identical one-liners; a copy-paste slip there
            // would silently parent a view to the wrong layer. This is what catches it.
            var expected = new (string Key, Func<ITransformProvider, RectTransform> Accessor)[] {
                (UILayers.Background, p => p.Background()),
                (UILayers.Hud, p => p.Hud()),
                (UILayers.Window, p => p.Window()),
                (UILayers.Popup, p => p.Popup()),
                (UILayers.Tooltip, p => p.Tooltip()),
                (UILayers.Overlay, p => p.Overlay()),
                (UILayers.Splash, p => p.Splash()),
                (UILayers.System, p => p.System()),
            };

            foreach (var pair in expected)
            {
                var provider = new RecordingProvider();
                var error = Assert.Throws<UILayerNotBoundException>(() => pair.Accessor(provider));

                Assert.AreEqual(pair.Key, error.Key, $"the accessor for '{pair.Key}' asked for the wrong key");
                CollectionAssert.AreEqual(new[] { pair.Key }, provider.Requested);
            }
        }

        [Test]
        public void AProjectDeclaresItsOwnLayerTheSameWayTheBuiltInsAre()
        {
            // The whole point of the redesign: no interface change and no release - a key plus an
            // extension method a third party could equally have written.
            var provider = new RecordingProvider();

            Assert.Throws<UILayerNotBoundException>(() => provider.Minimap());
            CollectionAssert.AreEqual(new[] { "minimap" }, provider.Requested);
        }

        [Test]
        public void AnUnboundLayerThrowsNamingTheKeyAndWhatIsBound()
        {
            var provider = new RecordingProvider(
                new UILayer(UILayers.Hud, null), new UILayer(UILayers.Popup, null));

            var error = Assert.Throws<UILayerNotBoundException>(() => provider.Window());

            Assert.AreEqual(UILayers.Window, error.Key);
            StringAssert.Contains("'window'", error.Message);
            StringAssert.Contains("'hud'", error.Message, "the message must say what IS bound");
            StringAssert.Contains("'popup'", error.Message);
        }

        [Test]
        public void AProviderWithNoLayersSaysSoRatherThanListingNothing()
        {
            var error = Assert.Throws<UILayerNotBoundException>(() => new RecordingProvider().Hud());

            StringAssert.Contains("binds no layers at all", error.Message);
        }

        [Test]
        public void ARowWhoseTransformIsNotSetIsReportedAsSuchInTheMessage()
        {
            // The 1.x failure this replaces: a forgotten layer produced a null parent, which the
            // providers read as "scene root" and then kept alive across every scene load.
            var provider = new RecordingProvider(new UILayer(UILayers.Hud, null));

            var error = Assert.Throws<UILayerNotBoundException>(() => provider.Window());

            StringAssert.Contains("transform not set", error.Message);
        }

        [Test]
        public void LayerOnANullProviderThrowsArgumentNullRatherThanNullReference()
        {
            ITransformProvider provider = null;

            Assert.Throws<ArgumentNullException>(() => provider.Layer(UILayers.Hud));
        }

        [Test]
        public void AnEmptyKeyIsReportedWithoutPretendingItWasAName()
        {
            var error = Assert.Throws<UILayerNotBoundException>(() => new RecordingProvider().Layer(""));

            StringAssert.Contains("an empty key", error.Message);
        }

        [Test]
        public void EveryBuiltInKeyIsDistinctAndLowerCase()
        {
            var keys = new[] {
                UILayers.Background, UILayers.Hud, UILayers.Window, UILayers.Popup,
                UILayers.Tooltip, UILayers.Overlay, UILayers.Splash, UILayers.System
            };

            CollectionAssert.AllItemsAreUnique(keys);
            foreach (var key in keys) Assert.AreEqual(key.ToLowerInvariant(), key);
        }

        [Test]
        public void LayersPreservesTheOrderItWasGiven()
        {
            var provider = new RecordingProvider(
                new UILayer(UILayers.Background, null),
                new UILayer(UILayers.Hud, null),
                new UILayer(UILayers.Window, null));

            var keys = new List<string>();
            foreach (var layer in provider.Layers) keys.Add(layer.Key);

            CollectionAssert.AreEqual(
                new[] { UILayers.Background, UILayers.Hud, UILayers.Window }, keys);
        }

        [Test]
        public void AUILayerKeepsWhatItWasConstructedWith()
        {
            var layer = new UILayer("minimap", null);

            Assert.AreEqual("minimap", layer.Key);
            Assert.IsNull(layer.Transform);
        }

        /// A test double is four members. Under the eight-property interface it was eleven, and that
        /// cost is a large part of why the old shape was hard to test around.
        private sealed class RecordingProvider : ITransformProvider
        {
            private readonly List<UILayer> _layers;

            public RecordingProvider(params UILayer[] layers) => _layers = new List<UILayer>(layers);

            public List<string> Requested { get; } = new List<string>();

            public Canvas Canvas => null;
            public Camera Camera => null;
            public Transform Pool => null;
            public IReadOnlyList<UILayer> Layers => _layers;

            public bool TryGetLayer(string key, out RectTransform layer)
            {
                Requested.Add(key);
                layer = null;
                return false;
            }
        }
    }

    internal static class MinimapLayer
    {
        public const string Key = "minimap";

        public static RectTransform Minimap(this ITransformProvider provider) => provider.Layer(Key);
    }
}
