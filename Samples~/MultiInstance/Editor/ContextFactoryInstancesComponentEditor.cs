using UnityEditor;
using UnityEngine;

namespace OpenUGD.Core.Editor
{
    /// <summary>
    /// Inspector for <see cref="ContextFactoryInstancesComponent"/>: adds a Rebuild button, enabled in play
    /// mode only.
    /// </summary>
    /// <remarks>
    /// The 0.6.x button was live in edit mode too, where <c>Rebuild</c> reached Unity's <c>Destroy</c> (which
    /// edit mode does not allow) and created the clones into the open scene.
    /// </remarks>
    [CustomEditor(typeof(ContextFactoryInstancesComponent), true)]
    public class ContextFactoryInstancesComponentEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild"))
                {
                    ((ContextFactoryInstancesComponent)target).Rebuild();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Rebuild requires play mode.", MessageType.Info);
            }
        }
    }
}
