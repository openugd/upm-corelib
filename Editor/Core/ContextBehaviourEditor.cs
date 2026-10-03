using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace OpenUGD.Core.Editor
{
    /// <summary>
    /// Inspector for <see cref="ContextBehaviour"/> and its subclasses: below the default inspector, the status of
    /// <see cref="ContextBehaviour.Startup"/>, the failure's message when the boot faulted, whether
    /// <see cref="ContextBehaviour.Context"/> is set, and a Rebuild button enabled in play mode.
    /// </summary>
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
