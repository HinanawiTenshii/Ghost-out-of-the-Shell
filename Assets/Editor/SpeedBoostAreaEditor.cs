using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(SpeedBoostArea))]
public sealed class SpeedBoostAreaEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("角色中心进入此区域后，当前移动速度 ×2，对玩家与所有 AI 状态均生效。多个区域重叠不叠加，离开即恢复；沙虫按头部位置判断。与权限区域一样，可在 Scene 视图拖动边框编辑范围。", MessageType.Info);
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();
        var area = (SpeedBoostArea)target;
        var size = serializedObject.FindProperty("areaSize");
        var offset = serializedObject.FindProperty("areaOffset");
        var color = serializedObject.FindProperty("editorColor").colorValue;
        boundsHandle.center = offset.vector2Value;
        boundsHandle.size = size.vector2Value;
        boundsHandle.handleColor = color;
        boundsHandle.wireframeColor = new Color(color.r, color.g, color.b, 1);
        using (new Handles.DrawingScope(area.transform.localToWorldMatrix))
        {
            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area, "Resize Speed Boost Area");
                offset.vector2Value = boundsHandle.center;
                size.vector2Value = new Vector2(Mathf.Max(.1f, boundsHandle.size.x), Mathf.Max(.1f, boundsHandle.size.y));
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
