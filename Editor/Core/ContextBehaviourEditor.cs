using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OpenUGD.Core.Editor
{
    /// <summary>
    /// Inspector for <see cref="ContextBehaviour"/>: shows the state of the boot and offers Rebuild.
    /// </summary>
    /// <remarks>
    /// The boot status is the point of this drawer. Before 2.0.0 the boot was a discarded task, so a context
    /// that failed to start looked identical in the inspector to one that started fine.
    /// </remarks>
    [CustomEditor(typeof(ContextBehaviour), true)]
    public class ContextBehaviourEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var component = (ContextBehaviour)target;

            EditorGUILayout.Separator();

            var startup = component.Startup;
            if (startup == null)
            {
                EditorGUILayout.LabelField("Startup", "not started");
            }
            else
            {
                EditorGUILayout.LabelField("Startup", startup.Status.ToString());
                if (startup.Status == TaskStatus.Faulted && startup.Exception != null)
                {
                    EditorGUILayout.HelpBox(startup.Exception.GetBaseException().Message, MessageType.Error);
                }
            }

            EditorGUILayout.LabelField("Context", component.Context == null ? "null" : "built");

            EditorGUILayout.Separator();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Rebuild"))
                {
                    component.Rebuild();
                }
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Rebuild requires play mode.", MessageType.Info);
            }
        }
    }
}
