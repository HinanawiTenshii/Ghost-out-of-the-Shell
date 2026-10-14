using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PatrolStreamingGroup))]
[CanEditMultipleObjects]
public sealed class PatrolStreamingGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("将本组件挂在独立的空对象上，绑定同一小区域的普通巡逻角色。\n" +
            "只同步距离加载导致的暂停/恢复，不改变路线，不形成巡逻队。任意成员或其巡逻路线（含连接段及回程）进入预加载范围后，整组分帧加载并同步开始巡逻。\n" +
            "战斗、调查、附身不受暂停约束；手动停用和已死亡的成员不参与等待。建议按房间或道路小段分组，避免整层常驻。", MessageType.Info);
        var members = serializedObject.FindProperty("members");
        EditorGUILayout.PropertyField(members, new GUIContent("巡逻成员"), true);
        serializedObject.ApplyModifiedProperties();
        if (targets.Length != 1) return;
        var group = (PatrolStreamingGroup)target;
        for (var parent = group.transform; parent != null; parent = parent.parent)
            if (parent.GetComponent<Renderer>() != null || parent.GetComponent<Collider2D>() != null ||
                parent.GetComponent<ZeldaCharacterAiBase>() != null)
            {
                EditorGUILayout.HelpBox("请改为独立空对象，避免分组组件随角色或场景物体被距离加载停用。", MessageType.Warning);
                break;
            }
        for (int i = 0; i < members.arraySize; i++)
        {
            var ai = members.GetArrayElementAtIndex(i).objectReferenceValue as ZeldaPatrolCharacterAi;
            if (ai == null) continue;
            if (ai is SquadPatrolCharacterAi)
                EditorGUILayout.HelpBox(ai.name + " 使用巡逻队 AI，不会加入本组。", MessageType.Warning);
            if (ai.gameObject.scene != group.gameObject.scene)
                EditorGUILayout.HelpBox(ai.name + " 不在本组所在场景，请绑定场景中的角色实例，而不是项目中的预制体资源。", MessageType.Warning);
            for (int j = 0; j < i; j++)
                if (members.GetArrayElementAtIndex(j).objectReferenceValue == ai)
                    EditorGUILayout.HelpBox(ai.name + " 重复绑定，仅计入一次。", MessageType.Warning);
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("巡逻同步状态", group.PatrolPaused ? "等待整组加载 / 离屏暂停" : "正常巡逻");
            if (GUILayout.Button("应用运行时成员修改")) group.RefreshMembers();
        }
    }
}
