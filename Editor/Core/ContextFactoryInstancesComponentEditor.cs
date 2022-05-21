using UnityEditor;
using UnityEngine;

namespace OpenUGD.Core.Editor
{
    [CustomEditor(typeof(ContextFactoryInstancesComponent), true)]
    public class ContextFactoryInstancesComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            if (GUILayout.Button("Rebuild"))
            {
                var component = (ContextFactoryInstancesComponent)target;
                component.Rebuild();
            }
        }
    }
}
