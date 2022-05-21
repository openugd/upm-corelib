using OpenUGD.Core.ContextBuilder;
using UnityEditor;

namespace OpenUGD.Core.Editor
{
    [CustomEditor(typeof(UnityDebugServiceObserver))]
    public class UnityDebugServiceObserverEditor : UnityEditor.Editor
    {
        private void OnEnable() => EditorApplication.update += Repaint;

        private void OnDisable() => EditorApplication.update -= Repaint;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var observer = (UnityDebugServiceObserver)target;
            foreach (var service in observer.Services)
            {
                EditorGUILayout.LabelField(service.GetType().Name, $"{service.State}");
            }
        }
    }
}
