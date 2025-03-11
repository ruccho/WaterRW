using UnityEngine;
using UnityEditor;

namespace Ruccho.Utilities
{
    [CustomEditor(typeof(MeshRendererSorting))]
    internal class MeshRendererSortingEditor : Editor
    {
        private MeshRendererSorting Target => target as MeshRendererSorting;

        private SerializedObject CurrentMeshRenderer { get; set; }

        public override void OnInspectorGUI()
        {
            if (!Target) base.OnInspectorGUI();

            var meshRenderer = Target.GetComponent<MeshRenderer>();

            if (!meshRenderer)
            {
                EditorGUILayout.HelpBox($"There is no MeshRenderer.", MessageType.Warning);
                return;
            }

            if (CurrentMeshRenderer == null || meshRenderer != CurrentMeshRenderer.targetObject)
            {
                CurrentMeshRenderer = new SerializedObject(meshRenderer);
            }
            else CurrentMeshRenderer.Update();

            var sortingOrderProp = CurrentMeshRenderer.FindProperty("m_SortingOrder");
            var sortingLayerIDProp = CurrentMeshRenderer.FindProperty("m_SortingLayerID");
            
            EditorGUI.BeginChangeCheck();
            
            SortingLayerEditorUtility.SortingLayerFieldLayout(new GUIContent("Sorting Layer"), sortingLayerIDProp);
            EditorGUILayout.PropertyField(sortingOrderProp);
            
            if (EditorGUI.EndChangeCheck())
            {
                CurrentMeshRenderer.ApplyModifiedProperties();
            }
        }
    }
}