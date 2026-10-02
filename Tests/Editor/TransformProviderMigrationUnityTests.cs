using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenUGD.Services.UI;
using UnityEditor;
using UnityEngine;

namespace OpenUGD.Tests
{
    /// <summary>
    /// The 0.6.x to 2.0 upgrade of <see cref="TransformProviderComponent"/> through the real editor:
    /// a prefab file written in the 0.6.1 layout is imported, loaded, saved and loaded again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each test writes its prefab as YAML text into a temporary folder under <c>Assets</c> and
    /// deletes the folder afterwards. The file is written by hand rather than by this version of the
    /// component, because the point is to load what 0.6.1 wrote: the script's 0.6.1 GUID, and the
    /// compiler-generated backing-field names of its auto-properties, each referencing a child object
    /// of the same prefab.
    /// </para>
    /// <para>
    /// The logic is also covered without Unity in <see cref="TransformProviderMigrationTests"/>. These
    /// add what only the editor can show: that Unity honours the old names, that its placeholders for
    /// unassigned fields do not set the upgrade off again, and that a saved file reloads unchanged.
    /// </para>
    /// </remarks>
    [TestFixture]
    [Category("RequiresUnity")]
    public class TransformProviderMigrationUnityTests
    {
        /// The child objects the eight 0.6.1 layer fields point at, back to front.
        private static readonly string[] LayerObjectNames = {
            "Background", "Hud", "Window", "Popup", "Tooltip", "Overlay", "Splash", "System",
        };

        /// The layers bound in the partially-assigned prefab: the three this package reads.
        private static readonly int[] PartiallyAssigned = { 1, 2, 4 };

        private string _folder;
        private string _path;

        [SetUp]
        public void CreateTemporaryFolder()
        {
            var guid = AssetDatabase.CreateFolder("Assets", "TransformProviderMigration-" + Guid.NewGuid().ToString("N"));
            _folder = AssetDatabase.GUIDToAssetPath(guid);
            Assert.IsFalse(string.IsNullOrEmpty(_folder), "could not create a temporary folder under Assets");
            _path = _folder + "/Legacy061.prefab";
        }

        [TearDown]
        public void DeleteTemporaryFolder()
        {
            if (!string.IsNullOrEmpty(_folder)) AssetDatabase.DeleteAsset(_folder);
            _folder = null;
        }

        [Test]
        public void APrefabSavedBy061LoadsWithEveryReferenceBound()
        {
            var root = Import(Legacy061Prefab(assigned: AllLayers()));

            AssertBound(root, AllLayers());
        }

        [Test]
        public void AnUnassigned061LayerLoadsAsARowWithNoTransform()
        {
            var root = Import(Legacy061Prefab(assigned: PartiallyAssigned));

            AssertBound(root, PartiallyAssigned);
        }

        [Test]
        public void SavingTheUpgradedPrefabWritesThe20LayoutAndReloadsUnchanged()
        {
            Import(Legacy061Prefab(assigned: AllLayers()));

            var contents = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(contents, _path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var text = File.ReadAllText(_path);
            StringAssert.DoesNotContain("k__BackingField", text, "the saved file still uses the 0.6.1 names");
            StringAssert.Contains("_layers:", text);

            AssertBound(Reimport(), AllLayers());
        }

        [Test]
        public void AListEmptiedAndSavedIn20StaysEmptyOnReload()
        {
            // Saved data carries the hidden 0.6.1 fields as unassigned. In the editor those can come
            // back as placeholder objects rather than null references; this is the test that they do
            // not refill a list emptied on purpose.
            Import(Legacy061Prefab(assigned: AllLayers()));

            var contents = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                var component = contents.GetComponent<TransformProviderComponent>();
                var serialized = new SerializedObject(component);
                serialized.FindProperty("_layers").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(0, component.Layers.Count, "applying the edit refilled the list");
                PrefabUtility.SaveAsPrefabAsset(contents, _path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var reloaded = Reimport().GetComponent<TransformProviderComponent>();

            Assert.AreEqual(0, reloaded.Layers.Count, "a 2.0 list emptied on purpose was refilled on load");
            Assert.IsTrue(reloaded.Canvas != null, "the canvas was lost on the way");
        }

        // ---- import and assertions -------------------------------------------------------------------

        private GameObject Import(string yaml)
        {
            File.WriteAllText(_path, yaml);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            Assert.IsTrue(root != null, $"{_path} did not import as a prefab");
            return root;
        }

        private GameObject Reimport()
        {
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            Assert.IsTrue(root != null, $"{_path} did not reimport");
            return root;
        }

        private static void AssertBound(GameObject root, IReadOnlyCollection<int> assigned)
        {
            var component = root.GetComponent<TransformProviderComponent>();
            Assert.IsTrue(component != null,
                $"script GUID {TransformProviderMigrationTests.ScriptGuid} no longer resolves to {nameof(TransformProviderComponent)}");

            var canvas = root.GetComponent<Canvas>();
            var camera = root.transform.Find("Camera").GetComponent<Camera>();
            var pool = root.transform.Find("Pool");
            Assert.IsTrue(canvas != null && camera != null && pool != null, "the prefab's own objects are missing");

            Assert.AreSame(canvas, component.Canvas, "Canvas");
            Assert.AreSame(camera, component.Camera, "Camera");
            Assert.AreSame(pool, component.Pool, "Pool");

            var keys = TransformProviderMigrationTests.ExpectedLayerKeys;
            Assert.AreEqual(keys.Length, component.Layers.Count, "row count");

            for (var i = 0; i < keys.Length; i++)
            {
                var row = component.Layers[i];
                Assert.AreEqual(keys[i], row.Key, $"key of row {i}");

                if (assigned.Contains(i))
                {
                    var expected = (RectTransform)root.transform.Find(LayerObjectNames[i]);
                    Assert.IsTrue(expected != null, $"the prefab has no '{LayerObjectNames[i]}' child");
                    Assert.AreSame(expected, row.Transform, $"transform of row {i} ('{keys[i]}')");

                    Assert.IsTrue(component.TryGetLayer(keys[i], out var layer), $"'{keys[i]}' is not bound");
                    Assert.AreSame(expected, layer, $"TryGetLayer('{keys[i]}')");
                }
                else
                {
                    Assert.IsTrue(row.Transform == null, $"row {i} ('{keys[i]}') should have no transform");
                    Assert.IsFalse(component.TryGetLayer(keys[i], out _), $"'{keys[i]}' should be unbound");
                }
            }
        }

        private static int[] AllLayers() => Enumerable.Range(0, LayerObjectNames.Length).ToArray();

        // ---- the 0.6.1 prefab ------------------------------------------------------------------------

        private const long RootObject = 1000, RootRect = 1001, CanvasId = 1002, ProviderId = 1003;
        private const long CameraObject = 2000, CameraTransform = 2001, CameraId = 2002;
        private const long PoolObject = 3000, PoolRect = 3001;

        private static long LayerObject(int i) => 4000 + 10 * i;
        private static long LayerRect(int i) => 4001 + 10 * i;

        /// A Screen Space - Camera canvas with a camera, a pool and the eight layers as children, and a
        /// TransformProviderComponent serialized exactly as 0.6.1 wrote it. Layers not in
        /// <paramref name="assigned"/> exist as objects but are left unassigned on the component.
        private static string Legacy061Prefab(IReadOnlyCollection<int> assigned)
        {
            var yaml = new StringBuilder();
            yaml.Append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");

            var children = new List<long> { CameraTransform, PoolRect };
            children.AddRange(Enumerable.Range(0, LayerObjectNames.Length).Select(LayerRect));

            GameObjectBlock(yaml, RootObject, "Root", 5, RootRect, CanvasId, ProviderId);
            RectTransformBlock(yaml, RootRect, RootObject, father: 0, children);
            CanvasBlock(yaml, CanvasId, RootObject, CameraId);

            Block(yaml, 114, ProviderId, "MonoBehaviour");
            yaml.Append($"  m_GameObject: {{fileID: {RootObject}}}\n");
            yaml.Append("  m_Enabled: 1\n");
            yaml.Append("  m_EditorHideFlags: 0\n");
            yaml.Append($"  m_Script: {{fileID: 11500000, guid: {TransformProviderMigrationTests.ScriptGuid}, type: 3}}\n");
            yaml.Append("  m_Name: \n");
            yaml.Append("  m_EditorClassIdentifier: \n");
            yaml.Append($"  <Canvas>k__BackingField: {{fileID: {CanvasId}}}\n");
            yaml.Append($"  <Camera>k__BackingField: {{fileID: {CameraId}}}\n");
            yaml.Append($"  <Pool>k__BackingField: {{fileID: {PoolRect}}}\n");
            for (var i = 0; i < LayerObjectNames.Length; i++)
            {
                var target = assigned.Contains(i) ? LayerRect(i) : 0;
                yaml.Append($"  {TransformProviderMigrationTests.LegacyLayerKeys[i]}: {{fileID: {target}}}\n");
            }

            GameObjectBlock(yaml, CameraObject, "Camera", 0, CameraTransform, CameraId);
            TransformBlock(yaml, CameraTransform, CameraObject, RootRect);
            CameraBlock(yaml, CameraId, CameraObject);

            GameObjectBlock(yaml, PoolObject, "Pool", 5, PoolRect);
            RectTransformBlock(yaml, PoolRect, PoolObject, RootRect, new long[0]);

            for (var i = 0; i < LayerObjectNames.Length; i++)
            {
                GameObjectBlock(yaml, LayerObject(i), LayerObjectNames[i], 5, LayerRect(i));
                RectTransformBlock(yaml, LayerRect(i), LayerObject(i), RootRect, new long[0]);
            }

            return yaml.ToString();
        }

        private static void Block(StringBuilder yaml, int classId, long id, string type)
        {
            yaml.Append($"--- !u!{classId} &{id}\n{type}:\n");
            yaml.Append("  m_ObjectHideFlags: 0\n");
            yaml.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
            yaml.Append("  m_PrefabInstance: {fileID: 0}\n");
            yaml.Append("  m_PrefabAsset: {fileID: 0}\n");
        }

        private static void GameObjectBlock(StringBuilder yaml, long id, string name, int layer, params long[] components)
        {
            Block(yaml, 1, id, "GameObject");
            yaml.Append("  serializedVersion: 6\n");
            yaml.Append("  m_Component:\n");
            foreach (var component in components) yaml.Append($"  - component: {{fileID: {component}}}\n");
            yaml.Append($"  m_Layer: {layer}\n");
            yaml.Append($"  m_Name: {name}\n");
            yaml.Append("  m_TagString: Untagged\n");
            yaml.Append("  m_Icon: {fileID: 0}\n");
            yaml.Append("  m_NavMeshLayer: 0\n");
            yaml.Append("  m_StaticEditorFlags: 0\n");
            yaml.Append("  m_IsActive: 1\n");
        }

        private static void Children(StringBuilder yaml, IReadOnlyCollection<long> children)
        {
            if (children.Count == 0)
            {
                yaml.Append("  m_Children: []\n");
                return;
            }

            yaml.Append("  m_Children:\n");
            foreach (var child in children) yaml.Append($"  - {{fileID: {child}}}\n");
        }

        private static void RectTransformBlock(StringBuilder yaml, long id, long gameObject, long father, IReadOnlyCollection<long> children)
        {
            Block(yaml, 224, id, "RectTransform");
            yaml.Append($"  m_GameObject: {{fileID: {gameObject}}}\n");
            yaml.Append("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n");
            yaml.Append("  m_LocalPosition: {x: 0, y: 0, z: 0}\n");
            yaml.Append("  m_LocalScale: {x: 1, y: 1, z: 1}\n");
            yaml.Append("  m_ConstrainProportionsScale: 0\n");
            Children(yaml, children);
            yaml.Append($"  m_Father: {{fileID: {father}}}\n");
            yaml.Append("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n");
            yaml.Append("  m_AnchorMin: {x: 0, y: 0}\n");
            yaml.Append("  m_AnchorMax: {x: 1, y: 1}\n");
            yaml.Append("  m_AnchoredPosition: {x: 0, y: 0}\n");
            yaml.Append("  m_SizeDelta: {x: 0, y: 0}\n");
            yaml.Append("  m_Pivot: {x: 0.5, y: 0.5}\n");
        }

        private static void TransformBlock(StringBuilder yaml, long id, long gameObject, long father)
        {
            Block(yaml, 4, id, "Transform");
            yaml.Append($"  m_GameObject: {{fileID: {gameObject}}}\n");
            yaml.Append("  serializedVersion: 2\n");
            yaml.Append("  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n");
            yaml.Append("  m_LocalPosition: {x: 0, y: 0, z: -10}\n");
            yaml.Append("  m_LocalScale: {x: 1, y: 1, z: 1}\n");
            yaml.Append("  m_ConstrainProportionsScale: 0\n");
            yaml.Append("  m_Children: []\n");
            yaml.Append($"  m_Father: {{fileID: {father}}}\n");
            yaml.Append("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n");
        }

        private static void CanvasBlock(StringBuilder yaml, long id, long gameObject, long camera)
        {
            Block(yaml, 223, id, "Canvas");
            yaml.Append($"  m_GameObject: {{fileID: {gameObject}}}\n");
            yaml.Append("  m_Enabled: 1\n");
            yaml.Append("  serializedVersion: 3\n");
            yaml.Append("  m_RenderMode: 1\n");
            yaml.Append($"  m_Camera: {{fileID: {camera}}}\n");
            yaml.Append("  m_PlaneDistance: 100\n");
            yaml.Append("  m_PixelPerfect: 0\n");
            yaml.Append("  m_ReceivesEvents: 1\n");
            yaml.Append("  m_OverrideSorting: 0\n");
            yaml.Append("  m_OverridePixelPerfect: 0\n");
            yaml.Append("  m_SortingBucketNormalizedSize: 0\n");
            yaml.Append("  m_VertexColorAlwaysGammaSpace: 0\n");
            yaml.Append("  m_AdditionalShaderChannelsFlag: 0\n");
            yaml.Append("  m_UpdateRectTransformForStandalone: 0\n");
            yaml.Append("  m_SortingLayerID: 0\n");
            yaml.Append("  m_SortingOrder: 0\n");
            yaml.Append("  m_TargetDisplay: 0\n");
        }

        private static void CameraBlock(StringBuilder yaml, long id, long gameObject)
        {
            Block(yaml, 20, id, "Camera");
            yaml.Append($"  m_GameObject: {{fileID: {gameObject}}}\n");
            yaml.Append("  m_Enabled: 1\n");
            yaml.Append("  serializedVersion: 2\n");
            yaml.Append("  m_ClearFlags: 3\n");
            yaml.Append("  m_BackGroundColor: {r: 0, g: 0, b: 0, a: 0}\n");
            yaml.Append("  m_projectionMatrixMode: 1\n");
            yaml.Append("  m_GateFitMode: 2\n");
            yaml.Append("  m_FOVAxisMode: 0\n");
            yaml.Append("  m_Iso: 200\n");
            yaml.Append("  m_ShutterSpeed: 0.005\n");
            yaml.Append("  m_Aperture: 16\n");
            yaml.Append("  m_FocusDistance: 10\n");
            yaml.Append("  m_FocalLength: 50\n");
            yaml.Append("  m_BladeCount: 5\n");
            yaml.Append("  m_Curvature: {x: 2, y: 11}\n");
            yaml.Append("  m_BarrelClipping: 0.25\n");
            yaml.Append("  m_Anamorphism: 0\n");
            yaml.Append("  m_SensorSize: {x: 36, y: 24}\n");
            yaml.Append("  m_LensShift: {x: 0, y: 0}\n");
            yaml.Append("  m_NormalizedViewPortRect:\n");
            yaml.Append("    serializedVersion: 2\n");
            yaml.Append("    x: 0\n");
            yaml.Append("    y: 0\n");
            yaml.Append("    width: 1\n");
            yaml.Append("    height: 1\n");
            yaml.Append("  near clip plane: 0.3\n");
            yaml.Append("  far clip plane: 1000\n");
            yaml.Append("  field of view: 60\n");
            yaml.Append("  orthographic: 1\n");
            yaml.Append("  orthographic size: 5\n");
            yaml.Append("  m_Depth: 0\n");
            yaml.Append("  m_CullingMask:\n");
            yaml.Append("    serializedVersion: 2\n");
            yaml.Append("    m_Bits: 32\n");
            yaml.Append("  m_RenderingPath: -1\n");
            yaml.Append("  m_TargetTexture: {fileID: 0}\n");
            yaml.Append("  m_TargetDisplay: 0\n");
            yaml.Append("  m_TargetEye: 3\n");
            yaml.Append("  m_HDR: 1\n");
            yaml.Append("  m_AllowMSAA: 1\n");
            yaml.Append("  m_AllowDynamicResolution: 0\n");
            yaml.Append("  m_ForceIntoRT: 0\n");
            yaml.Append("  m_OcclusionCulling: 1\n");
            yaml.Append("  m_StereoConvergence: 10\n");
            yaml.Append("  m_StereoSeparation: 0.022\n");
        }
    }
}
