using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ruccho.WaterRW.Utilities
{
    internal static class SortingLayerEditorUtility
    {
        
        private static GUIStyle boldPopupStyle;
        private static GUIStyle BoldPopupStyle
        {
            get
            {
                if (boldPopupStyle == null)
                {
                    boldPopupStyle = new GUIStyle(EditorStyles.popup);
                    boldPopupStyle.fontStyle = FontStyle.Bold;
                }
                return boldPopupStyle;
            }
        }

        private delegate void SortingLayerFieldDelegate(Rect position, GUIContent label, SerializedProperty layerID,
            GUIStyle style, GUIStyle labelStyle);

        private static SortingLayerFieldDelegate sortingLayerFieldDelegate = default;
        
        private static bool HasPrefabOverride(SerializedProperty property)
        {
            return property != null && property.serializedObject.targetObjects.Length == 1 && property.isInstantiatedPrefab && property.prefabOverride;
        }
        
        public static void SortingLayerFieldLayout(GUIContent label, SerializedProperty layerID)
        {
            var hasPrefabOverride = HasPrefabOverride(layerID);
            var style = hasPrefabOverride ? BoldPopupStyle : EditorStyles.popup;
            var labelStyle = hasPrefabOverride ? EditorStyles.boldLabel : EditorStyles.label;
            SortingLayerFieldLayout(label, layerID, style, labelStyle);
        }
        
        public static void SortingLayerFieldLayout(GUIContent label, SerializedProperty layerID, GUIStyle style, GUIStyle labelStyle)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, style);
            SortingLayerField(rect, label, layerID, style, labelStyle);
        }

        public static void SortingLayerField(Rect position, GUIContent label, SerializedProperty layerID)
        {
            var hasPrefabOverride = HasPrefabOverride(layerID);
            var style = hasPrefabOverride ? BoldPopupStyle : EditorStyles.popup;
            var labelStyle = hasPrefabOverride ? EditorStyles.boldLabel : EditorStyles.label;
            SortingLayerField(position, label, layerID, style, labelStyle);
        }
        
        public static void SortingLayerField(Rect position, GUIContent label, SerializedProperty layerID,
            GUIStyle style, GUIStyle labelStyle)
        {
            if (sortingLayerFieldDelegate == default)
            {
                var editorGuiType = typeof(EditorGUI);
                var sortingLayerFieldMethod =
                    editorGuiType.GetMethod("SortingLayerField", BindingFlags.Static | BindingFlags.NonPublic);

                if (sortingLayerFieldMethod == null) return;

                sortingLayerFieldDelegate = (SortingLayerFieldDelegate)
                    Delegate.CreateDelegate(typeof(SortingLayerFieldDelegate), sortingLayerFieldMethod);
            }
            
            sortingLayerFieldDelegate?.Invoke(position, label, layerID, style, labelStyle);
        }
    }
}