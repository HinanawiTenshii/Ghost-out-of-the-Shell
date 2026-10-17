using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrapdoorInteraction))]
public sealed class TrapdoorInteractionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        var mode = serializedObject.FindProperty("travelMode");
        mode.enumValueIndex = EditorGUILayout.Popup("传送方式", mode.enumValueIndex, new[] { "同场景绑定", "跨场景绑定" });
        if (mode.enumValueIndex == 0)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("localTarget"), new GUIContent("目标活板门"));
        else
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crossSceneTarget"), new GUIContent("目标定位资源"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ownEndpoint"), new GUIContent("本门定位资源"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("arrivalOffset"), new GUIContent("本门到达偏移"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isLocked"), new GUIContent("锁住"));
        if (serializedObject.FindProperty("isLocked").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("password"), new GUIContent("四位密码"));
            EditorGUILayout.HelpBox("沿用门的密码窗口，输入密码需要 2 点技巧；成功解锁后直接通行，解锁状态随存档保留。", MessageType.Info);
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("interactionDistance"), new GUIContent("交互距离"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("promptFont"), new GUIContent("提示与密码字体"));
        serializedObject.ApplyModifiedProperties();
        var door = (TrapdoorInteraction)target;
        using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(door) || string.IsNullOrEmpty(door.gameObject.scene.path)))
        {
            if (door.OwnEndpoint == null)
            {
                if (GUILayout.Button("为本门创建跨场景定位资源")) CreateEndpoint(door);
            }
            else if (GUILayout.Button("将本门场景与当前位置同步到定位资源")) Synchronize(door);
        }
        EditorGUILayout.HelpBox("同场景：直接拖入另一扇活板门。跨场景：先在目标场景的活板门上创建定位资源，再将该资源填入出发门的“目标定位资源”。目标场景须加入 Build Settings。往返时两端分别绑定。运行时读取目标实际位置，备用位置仅用于目标缺失时脱困。", MessageType.Info);
        if (door.OwnEndpoint != null && !EditorUtility.IsPersistent(door))
        {
            if (door.OwnEndpoint.SceneName != door.gameObject.scene.name ||
                (door.OwnEndpoint.FallbackPosition - door.ArrivalPosition).sqrMagnitude > .0001f)
                EditorGUILayout.HelpBox("定位资源中的场景或备用位置与本门不同，请点击同步。每扇目标活板门应使用独立定位资源，不要让复制的门共用同一资源。", MessageType.Warning);
        }
    }
    private void CreateEndpoint(TrapdoorInteraction door)
    {
        string path = EditorUtility.SaveFilePanelInProject("创建活板门定位", "Trapdoor_" + door.gameObject.scene.name, "asset", "为这扇门保存独立的跨场景定位资源。");
        if (string.IsNullOrEmpty(path)) return;
        var endpoint = CreateInstance<TrapdoorDestination>();
        AssetDatabase.CreateAsset(endpoint, path);
        Undo.RecordObject(door, "Bind Trapdoor Endpoint");
        serializedObject.Update();
        serializedObject.FindProperty("ownEndpoint").objectReferenceValue = endpoint;
        serializedObject.ApplyModifiedProperties();
        Synchronize(door);
    }
    private static void Synchronize(TrapdoorInteraction door)
    {
        var endpoint = door.OwnEndpoint;
        if (endpoint == null) return;
        Undo.RecordObject(endpoint, "Synchronize Trapdoor Endpoint");
        var data = new SerializedObject(endpoint);
        data.FindProperty("sceneName").stringValue = door.gameObject.scene.name;
        data.FindProperty("fallbackPosition").vector2Value = door.ArrivalPosition;
        data.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(endpoint);
    }
}
