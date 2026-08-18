using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(PermissionArea))]
public class PermissionAreaEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

    private void OnSceneGUI()
    {
        PermissionArea area = (PermissionArea)target;
        SerializedProperty sizeProperty = serializedObject.FindProperty("areaSize");
        SerializedProperty offsetProperty = serializedObject.FindProperty("areaOffset");
        SerializedProperty colorProperty = serializedObject.FindProperty("editorColor");

        boundsHandle.center = offsetProperty.vector2Value;
        boundsHandle.size = sizeProperty.vector2Value;
        boundsHandle.handleColor = colorProperty.colorValue;
        boundsHandle.wireframeColor = new Color(
            colorProperty.colorValue.r,
            colorProperty.colorValue.g,
            colorProperty.colorValue.b,
            1f);

        using (new Handles.DrawingScope(area.transform.localToWorldMatrix))
        {
            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area, "Resize Permission Area");
                offsetProperty.vector2Value = boundsHandle.center;
                sizeProperty.vector2Value = new Vector2(
                    Mathf.Max(0.1f, boundsHandle.size.x),
                    Mathf.Max(0.1f, boundsHandle.size.y));
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
