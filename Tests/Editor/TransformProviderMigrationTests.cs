using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenUGD.Services.UI;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace OpenUGD.Tests
{
    /// <summary>
    /// The 0.6.x to 2.0 upgrade of <see cref="TransformProviderComponent"/>. What matters: every key
    /// 0.6.1 wrote still lands somewhere, the eight layers come back as rows in their old order under
    /// the <see cref="UILayers"/> keys, and data written by 2.0 never goes through the upgrade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These run without a Unity runtime by playing the serializer's part: build the component without
    /// its constructor, write each serialized key into the field Unity would pick (same name, otherwise
    /// the one field that names the key in <see cref="FormerlySerializedAsAttribute"/>), then call
    /// <see cref="ISerializationCallbackReceiver.OnAfterDeserialize"/>. The canvas, camera and
    /// transforms are managed shells with no engine object behind them. That is enough because the
    /// upgrade only moves references and must never ask the engine anything — Unity may run it off the
    /// main thread. Assertions compare with <see cref="object.ReferenceEquals"/> for the same reason.
    /// </para>
    /// <para>
    /// What only the real editor can show — that Unity honours the names, that its placeholders for
    /// unassigned fields do not trigger the upgrade, that a saved file reloads — is in
    /// <see cref="TransformProviderMigrationUnityTests"/>.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class TransformProviderMigrationTests
    {
        /// The script GUID in every scene and prefab saved by 0.6.x. Change it and Unity no longer knows
        /// which script those components are, which loses everything on them at once.
        internal const string ScriptGuid = "decf5df09cbb4e63ab9602d74b467234";

        /// The keys 0.6.1 wrote for the eight layers, in its declaration order (back to front).
        internal static readonly string[] LegacyLayerKeys = {
            "<Background>k__BackingField",
            "<Hud>k__BackingField",
            "<Window>k__BackingField",
            "<Popup>k__BackingField",
            "<Tooltip>k__BackingField",
            "<Overlay>k__BackingField",
            "<Splash>k__BackingField",
            "<System>k__BackingField",
        };

        /// The keys the eight rows must carry, in the same order.
        internal static readonly string[] ExpectedLayerKeys = {
            UILayers.Background, UILayers.Hud, UILayers.Window, UILayers.Popup,
            UILayers.Tooltip, UILayers.Overlay, UILayers.Splash, UILayers.System,
        };

        private const string LegacyCanvasKey = "<Canvas>k__BackingField";
        private const string LegacyCameraKey = "<Camera>k__BackingField";
        private const string LegacyPoolKey = "<Pool>k__BackingField";

        [Test]
        public void Every061KeyLandsInExactlyOneSerializedFieldOfACompatibleType()
        {
            // A typo in one FormerlySerializedAs string loses that reference in every old scene, and
            // nothing else would notice: Unity skips keys it cannot place without a word.
            var legacy = new List<(string Key, Type Type)> {
                (LegacyCanvasKey, typeof(Canvas)),
                (LegacyCameraKey, typeof(Camera)),
                (LegacyPoolKey, typeof(Transform)),
            };
            legacy.AddRange(LegacyLayerKeys.Select(key => (key, typeof(RectTransform))));

            foreach (var (key, type) in legacy)
            {
                var field = FieldFor(key);
                Assert.IsTrue(field.FieldType.IsAssignableFrom(type),
                    $"'{key}' held a {type.Name} in 0.6.1 but lands in {field.Name}, a {field.FieldType.Name}");
            }
        }

        [Test]
        public void A061ComponentComesBackWithEveryReferenceInPlace()
        {
            var canvas = Shell<Canvas>();
            var camera = Shell<Camera>();
            var pool = Shell<RectTransform>();
            var layers = LegacyLayerKeys.Select(_ => Shell<RectTransform>()).ToArray();

            var component = Load(Legacy061(canvas, camera, pool, layers));

            AssertSame(canvas, component.Canvas, "Canvas");
            AssertSame(camera, component.Camera, "Camera");
            AssertSame(pool, component.Pool, "Pool");
            AssertRows(component, layers);
        }

        [Test]
        public void AnUnassigned061FieldBecomesARowWithNoTransform()
        {
            // 0.6.1 always had eight slots; a slot nobody filled is the same empty row Reset seeds.
            var hud = Shell<RectTransform>();
            var window = Shell<RectTransform>();
            var layers = new RectTransform[8];
            layers[1] = hud;
            layers[2] = window;

            var component = Load(Legacy061(null, null, null, layers));

            AssertRows(component, layers);
        }

        [Test]
        public void The061FieldsAreEmptiedSoASaveWritesOnlyTheList()
        {
            var layers = LegacyLayerKeys.Select(_ => Shell<RectTransform>()).ToArray();
            var component = Load(Legacy061(null, null, null, layers));

            foreach (var key in LegacyLayerKeys)
            {
                Assert.IsTrue(ReferenceEquals(FieldFor(key).GetValue(component), null),
                    $"the field read from '{key}' still holds its reference after the upgrade");
            }
        }

        [Test]
        public void AnUpgradedComponentRoundTripsUnchanged()
        {
            var layers = LegacyLayerKeys.Select(_ => Shell<RectTransform>()).ToArray();
            var upgraded = Load(Legacy061(null, null, null, layers));

            var reloaded = Load(Save(upgraded));

            AssertRows(reloaded, layers);
        }

        [Test]
        public void DataWrittenBy20IsNeverUpgradedEvenWhenItsListIsEmpty()
        {
            // In the editor an unassigned object field can come back as a placeholder object rather
            // than a null reference. Were the old fields what told old data from new, a list emptied on
            // purpose would refill with eight rows on every load. The placeholders here stand in for
            // exactly that.
            var component = New();
            SetField(component, "_layers", new List<UILayer>());

            var saved = Save(component);
            foreach (var key in LegacyLayerKeys) saved[FieldFor(key).Name] = Shell<RectTransform>();

            var reloaded = Load(saved);

            Assert.AreEqual(0, reloaded.Layers.Count, "a 2.0 list emptied on purpose was refilled");
        }

        [Test]
        public void DataWrittenBy20KeepsItsRowsAsTheyWere()
        {
            var minimap = Shell<RectTransform>();
            var hud = Shell<RectTransform>();
            var component = New();
            SetField(component, "_layers", new List<UILayer> {
                new UILayer("minimap", minimap), new UILayer(UILayers.Hud, hud)
            });

            var saved = Save(component);
            foreach (var key in LegacyLayerKeys) saved[FieldFor(key).Name] = Shell<RectTransform>();

            var reloaded = Load(saved);

            Assert.AreEqual(2, reloaded.Layers.Count);
            Assert.AreEqual("minimap", reloaded.Layers[0].Key);
            AssertSame(minimap, reloaded.Layers[0].Transform, "row 0");
            Assert.AreEqual(UILayers.Hud, reloaded.Layers[1].Key);
            AssertSame(hud, reloaded.Layers[1].Transform, "row 1");
        }

        [Test]
        public void UnstampedDataWhoseListAlreadyHasRowsKeepsThem()
        {
            // "Only when the list is empty": the upgrade adds rows, it never replaces them.
            var mine = Shell<RectTransform>();
            var data = Legacy061(null, null, null, LegacyLayerKeys.Select(_ => Shell<RectTransform>()).ToArray());
            data["_layers"] = new List<UILayer> { new UILayer("mine", mine) };

            var component = Load(data);

            Assert.AreEqual(1, component.Layers.Count);
            Assert.AreEqual("mine", component.Layers[0].Key);
            AssertSame(mine, component.Layers[0].Transform, "row 0");
        }

        [Test]
        public void AMissingListIsCreatedRatherThanDereferenced()
        {
            var layers = LegacyLayerKeys.Select(_ => Shell<RectTransform>()).ToArray();

            var component = Load(Legacy061(null, null, null, layers), runFieldInitialisers: false);

            AssertRows(component, layers);
        }

        [Test]
        public void TheScriptKeepsThe061Guid()
        {
            var meta = Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..",
                "Runtime", "Services", "UI", "TransformProviderComponent.cs.meta");

            Assert.IsTrue(File.Exists(meta), $"expected the script's .meta at {meta}");
            StringAssert.Contains($"guid: {ScriptGuid}", File.ReadAllText(meta),
                "the script GUID changed; every scene and prefab using the component would lose it");
        }

        // ---- the serializer's part -------------------------------------------------------------------

        /// The component's data as 0.6.1 wrote it: eleven keys, any of which may be unassigned.
        private static Dictionary<string, object> Legacy061(
            Canvas canvas, Camera camera, Transform pool, IReadOnlyList<RectTransform> layers)
        {
            var data = new Dictionary<string, object> {
                [LegacyCanvasKey] = canvas,
                [LegacyCameraKey] = camera,
                [LegacyPoolKey] = pool,
            };
            for (var i = 0; i < LegacyLayerKeys.Length; i++) data[LegacyLayerKeys[i]] = layers[i];
            return data;
        }

        /// A component as its constructor leaves it — what AddComponent hands a 2.0 scene.
        private static TransformProviderComponent New(bool runFieldInitialisers = true)
        {
            // The MonoBehaviour constructor calls into the engine, so the one field initialiser the
            // constructor would run is applied by hand.
            var component = (TransformProviderComponent)RuntimeHelpers.GetUninitializedObject(
                typeof(TransformProviderComponent));
            if (runFieldInitialisers) SetField(component, "_layers", new List<UILayer>());
            return component;
        }

        /// Loads data into a fresh component as Unity does: construct, write each key, call back. Keys
        /// the data lacks keep the value the constructor gave them.
        private static TransformProviderComponent Load(
            IDictionary<string, object> data, bool runFieldInitialisers = true)
        {
            var component = New(runFieldInitialisers);

            foreach (var pair in data) FieldFor(pair.Key).SetValue(component, Copy(pair.Value));

            ((ISerializationCallbackReceiver)component).OnAfterDeserialize();
            return component;
        }

        /// Writes a component out as Unity does: call back, then read every serialized field.
        private static Dictionary<string, object> Save(TransformProviderComponent component)
        {
            ((ISerializationCallbackReceiver)component).OnBeforeSerialize();

            return SerializedFields().ToDictionary(field => field.Name, field => Copy(field.GetValue(component)));
        }

        /// Resolves a serialized key the way Unity does: the field of that name, otherwise the one field
        /// that names it in [FormerlySerializedAs].
        private static FieldInfo FieldFor(string key)
        {
            var fields = SerializedFields().ToArray();

            var exact = fields.Where(field => field.Name == key).ToArray();
            if (exact.Length == 1) return exact[0];

            var former = fields.Where(field => field.GetCustomAttributes<FormerlySerializedAsAttribute>()
                .Any(attribute => attribute.oldName == key)).ToArray();
            Assert.AreEqual(1, former.Length,
                $"'{key}' must be claimed by exactly one serialized field; found " +
                $"[{string.Join(", ", former.Select(field => field.Name))}]");
            return former[0];
        }

        private static IEnumerable<FieldInfo> SerializedFields() =>
            typeof(TransformProviderComponent)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                           BindingFlags.DeclaredOnly)
                .Where(field => !field.IsNotSerialized &&
                                (field.IsPublic || field.IsDefined(typeof(SerializeField), false)));

        private static void SetField(TransformProviderComponent component, string name, object value) =>
            FieldFor(name).SetValue(component, value);

        // A list is copied so that a saved or loaded component never shares one with the data it came
        // from; UILayer is a struct, so copying the list copies the rows.
        private static object Copy(object value) =>
            value is List<UILayer> list ? new List<UILayer>(list) : value;

        private static T Shell<T>() where T : Object => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        private static string ThisFile([CallerFilePath] string path = null) => path;

        // ---- assertions that never ask the engine anything --------------------------------------------

        private static void AssertRows(TransformProviderComponent component, IReadOnlyList<RectTransform> expected)
        {
            Assert.AreEqual(ExpectedLayerKeys.Length, component.Layers.Count, "row count");
            for (var i = 0; i < ExpectedLayerKeys.Length; i++)
            {
                Assert.AreEqual(ExpectedLayerKeys[i], component.Layers[i].Key, $"key of row {i}");
                AssertSame(expected[i], component.Layers[i].Transform, $"transform of row {i} ('{ExpectedLayerKeys[i]}')");
            }
        }

        // NUnit's AreSame would format a mismatch with ToString, which on a shell calls into the engine.
        private static void AssertSame(object expected, object actual, string what) =>
            Assert.IsTrue(ReferenceEquals(expected, actual), $"{what}: not the reference that was saved");
    }
}
