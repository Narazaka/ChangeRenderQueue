using UnityEditor;
using UnityEngine;

namespace Narazaka.VRChat.ChangeRenderQueue.Editor
{
    [CustomEditor(typeof(ChangeRenderQueue))]
    class ChangeRenderQueueEditor : UnityEditor.Editor
    {
        SerializedProperty RenderQueue;
        SerializedProperty MaterialIndex;
        float LineHeight;

        void OnEnable()
        {
            LineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            RenderQueue = serializedObject.FindProperty(nameof(ChangeRenderQueue.RenderQueue));
            MaterialIndex = serializedObject.FindProperty(nameof(ChangeRenderQueue.MaterialIndex));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUILayout.PropertyField(RenderQueue);
            EditorGUILayout.PropertyField(MaterialIndex);

            var renderer = (target as ChangeRenderQueue).GetComponent<Renderer>();
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                var lineCount = materials.Length + 1;
                EditorGUI.indentLevel++;
                var rect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight * lineCount + EditorGUIUtility.standardVerticalSpacing * (lineCount - 1)));
                rect = EditorGUI.IndentedRect(rect);
                EditorGUI.indentLevel--;
                EditorGUI.BeginProperty(rect, GUIContent.none, MaterialIndex);
                rect.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.BeginChangeCheck();
                var isAll = EditorGUI.ToggleLeft(rect, "All", MaterialIndex.intValue == -1);
                if (EditorGUI.EndChangeCheck())
                {
                    MaterialIndex.intValue = isAll ? -1 : 0;
                }
                rect.y += LineHeight;

                for (int i = 0; i < materials.Length; i++)
                {
                    EditorGUI.BeginChangeCheck();
                    var isTarget = EditorGUI.ToggleLeft(rect, $"[{i}]", MaterialIndex.intValue == i);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MaterialIndex.intValue = isTarget ? i : -1;
                    }
                    var objectRect = rect;
                    objectRect.xMin += 42;
                    objectRect.xMax -= 42;
                    EditorGUI.ObjectField(objectRect, materials[i], typeof(Material), false);
                    var labelRect = rect;
                    labelRect.xMin = objectRect.xMax + 2;
                    EditorGUI.LabelField(labelRect, $"({materials[i].renderQueue})");
                    rect.y += LineHeight;
                }

                EditorGUI.EndProperty();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
