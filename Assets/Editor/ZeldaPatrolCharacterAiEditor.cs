using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(ZeldaPatrolCharacterAi), true), CanEditMultipleObjects]
public class ZeldaPatrolCharacterAiEditor : Editor
{
    private static readonly string[] RouteFields =
    {
        "m_Script", "patrolSpeedMultiplier", "patrolPointTolerance",
        "patrolPoint2Offset", "patrolPoint3Offset", "patrolPoint4Offset",
        "waitAtPoint1Duration", "waitAtPoint2Duration", "waitAtPoint3Duration", "waitAtPoint4Duration",
        "useCustomPatrolRoute", "customPatrolPoints", "legacyPatrolPointCount"
    };
    private ReorderableList pointList;
    private int selectedPoint;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (target is ZeldaPatrolCharacterAi specializedAi && !specializedAi.HasIdlePatrol)
        {
            // Original idle skeletons keep their ordinary inspector and no phantom route handles.
            DrawPropertiesExcluding(serializedObject, RouteFields);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField("巡逻路径", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolSpeedMultiplier"), new GUIContent("巡逻速度倍率"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolPointTolerance"), new GUIContent("到达判定距离"));
        if (serializedObject.isEditingMultipleObjects)
            EditorGUILayout.HelpBox("请单独选择角色编辑巡逻点，避免覆盖各角色不同的既有路径。", MessageType.Info);
        else
        {
            EditorGUILayout.HelpBox("按列表顺序循环巡逻。坐标是相对角色初始位置的世界方向偏移，不随角色缩放或旋转改变。选中巡逻点后也可在 Scene 视图拖动。", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (!serializedObject.FindProperty("useCustomPatrolRoute").boolValue)
                {
                    EditorGUILayout.IntSlider(serializedObject.FindProperty("legacyPatrolPointCount"), 2, 4, new GUIContent("巡逻点数量"));
                    EditorGUILayout.HelpBox("依次使用前 2～4 个点。减少数量不会删除隐藏点的坐标和停留时间；需要更多点时，可转换为下方的可增删列表。", MessageType.None);
                    DrawLegacyPoints();
                    if (GUILayout.Button("启用可增删巡逻点（保留当前路径）"))
                    {
                        UpgradeRoute(serializedObject);
                        pointList = null;
                    }
                }
                if (serializedObject.FindProperty("useCustomPatrolRoute").boolValue)
                {
                    EnsurePointList();
                    pointList.DoLayoutList();
                    int count = serializedObject.FindProperty("customPatrolPoints").arraySize;
                    if (count < 2) EditorGUILayout.HelpBox("此旧路径少于两个点，已保留原行为。请使用 + 补充到至少两个巡逻点。", MessageType.Info);
                }
            }
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("请退出运行模式后编辑路径；运行中的巡逻进度不会被重置。", MessageType.None);
        }
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, RouteFields);
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLegacyPoints()
    {
        int count = Mathf.Clamp(serializedObject.FindProperty("legacyPatrolPointCount").intValue, 2, 4);
        for (int i = 0; i < count; i++)
        {
            EditorGUILayout.LabelField("巡逻点 " + (i + 1), EditorStyles.miniBoldLabel);
            if (i == 0) EditorGUILayout.LabelField("位置", "角色初始位置 (0, 0)");
            else EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolPoint" + (i + 1) + "Offset"), new GUIContent("位置偏移"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("waitAtPoint" + (i + 1) + "Duration"), new GUIContent("停留秒数"));
        }
    }

    // Called only following an explicit edit, via SerializedProperty so Undo,
    // scene dirty tracking and prefab-instance overrides are handled by Unity.
    internal static void UpgradeRoute(SerializedObject data)
    {
        if (data.FindProperty("useCustomPatrolRoute").boolValue) return;
        var points = data.FindProperty("customPatrolPoints");
        int count = Mathf.Clamp(data.FindProperty("legacyPatrolPointCount").intValue, 2, 4);
        points.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            var point = points.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("offset").vector2Value = i == 0 ? Vector2.zero :
                data.FindProperty("patrolPoint" + (i + 1) + "Offset").vector2Value;
            point.FindPropertyRelative("waitDuration").floatValue =
                data.FindProperty("waitAtPoint" + (i + 1) + "Duration").floatValue;
        }
        data.FindProperty("useCustomPatrolRoute").boolValue = true;
    }

    private void EnsurePointList()
    {
        if (pointList != null) return;
        var points = serializedObject.FindProperty("customPatrolPoints");
        pointList = new ReorderableList(serializedObject, points, true, true, true, true);
        pointList.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 8f;
        pointList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "巡逻点列表（至少 2 点；拖动排序，+ / − 增删）");
        pointList.drawElementCallback = (rect, index, active, focused) =>
        {
            var point = points.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, point.FindPropertyRelative("offset"), new GUIContent("点 " + (index + 1) + " 偏移"));
            rect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(rect, point.FindPropertyRelative("waitDuration"), new GUIContent("停留秒数"));
        };
        pointList.onSelectCallback = list => { selectedPoint = list.index; SceneView.RepaintAll(); };
        pointList.onReorderCallback = list => { selectedPoint = list.index; SceneView.RepaintAll(); };
        pointList.onAddCallback = list =>
        {
            int count = points.arraySize;
            Vector2 offset = count == 0 ? Vector2.zero : points.GetArrayElementAtIndex(count - 1).FindPropertyRelative("offset").vector2Value;
            if (count > 0) offset += Vector2.right * 2f;
            points.arraySize++;
            var point = points.GetArrayElementAtIndex(count);
            point.FindPropertyRelative("offset").vector2Value = offset;
            point.FindPropertyRelative("waitDuration").floatValue = 1f;
            selectedPoint = list.index = count;
            SceneView.RepaintAll();
        };
        pointList.onCanRemoveCallback = list => points.arraySize > 2;
        pointList.onRemoveCallback = list =>
        {
            if (points.arraySize <= 2) return;
            int index = Mathf.Clamp(list.index, 0, points.arraySize - 1);
            points.DeleteArrayElementAtIndex(index);
            selectedPoint = list.index = Mathf.Max(0, index - 1);
            SceneView.RepaintAll();
        };
    }

    protected virtual void OnSceneGUI()
    {
        if (Application.isPlaying || !(target is ZeldaPatrolCharacterAi ai) || !ai.HasIdlePatrol) return;
        // Unity invokes Scene GUI for one target at a time (also during prefab
        // dragging). The Inspector's cached multi-object serializedObject is
        // forbidden here; use an independent wrapper for the current target.
        using var sceneObject = new SerializedObject(ai);
        sceneObject.Update();
        bool custom = sceneObject.FindProperty("useCustomPatrolRoute").boolValue;
        var points = sceneObject.FindProperty("customPatrolPoints");
        int count = custom ? points.arraySize : Mathf.Clamp(sceneObject.FindProperty("legacyPatrolPointCount").intValue, 2, 4);
        Vector3 origin = ai.transform.position;
        selectedPoint = Mathf.Clamp(selectedPoint, 0, Mathf.Max(0, count - 1));
        using (new Handles.DrawingScope(new Color(.2f, .85f, 1f, .9f)))
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = custom ? points.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector2Value :
                    (i == 0 ? Vector2.zero : sceneObject.FindProperty("patrolPoint" + (i + 1) + "Offset").vector2Value);
                Vector3 position = origin + (Vector3)offset;
                float size = HandleUtility.GetHandleSize(position) * .06f;
                Handles.Label(position + Vector3.up * size * 1.5f, "巡逻点 " + (i + 1));
                if (Handles.Button(position, Quaternion.identity, size, size, Handles.DotHandleCap))
                {
                    selectedPoint = i;
                    if (pointList != null) pointList.index = i;
                    Repaint();
                }
                if (selectedPoint != i) continue;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
                if (!EditorGUI.EndChangeCheck()) continue;
                UpgradeRoute(sceneObject);
                points.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector2Value = (Vector2)(moved - origin);
                sceneObject.ApplyModifiedProperties();
                Repaint();
                break;
            }
        }
    }
}
