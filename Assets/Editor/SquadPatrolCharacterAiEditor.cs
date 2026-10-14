using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SquadPatrolCharacterAi)), CanEditMultipleObjects]
public sealed class SquadPatrolCharacterAiEditor : ZeldaPatrolCharacterAiEditor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("默认队长绑定最多 4 名队员；队员的 Members 留空。队长递补按数组顺序，继承原队长路线。不能与另一个 AI 同时使用。", MessageType.Info);
        base.OnInspectorGUI();
        var ai = (SquadPatrolCharacterAi)target;
        if (Application.isPlaying)
            EditorGUILayout.ObjectField("当前队长", ai.CurrentLeader, typeof(SquadPatrolCharacterAi), true);
        else if (ai.GetComponents<ZeldaCharacterAiBase>().Length > 1)
            EditorGUILayout.HelpBox("请移除重复 AI，或撤销添加后使用 GameObject/AI/Convert To Squad Patrol 保留原配置进行转换。", MessageType.Error);
    }

    [MenuItem("GameObject/AI/Convert To Squad Patrol", false, 20)]
    public static void ConvertSelected()
    {
        if (Application.isPlaying) return;
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Convert To Squad Patrol");
        foreach (var go in Selection.gameObjects)
        {
            if (EditorUtility.IsPersistent(go) || go.GetComponent<ZeldaCharacterData>() == null) continue;
            var old = go.GetComponent<ZeldaCharacterAiBase>();
            if (old is SquadPatrolCharacterAi) continue;
            if (old is AutomatonCharacterAi || old is SkeletonCharacterAi ||
                go.GetComponent<SandwormZeldaCharacterData>() != null)
            { Debug.LogWarning("专用 AI 角色不能转换为普通巡逻队。", go); continue; }
            bool civilian = old is CivilianPatrolCharacterAi || old is CivilianIdleCharacterAi;
            bool wasEnabled = old == null || old.enabled;
            // A temporary editor-only clone keeps serialized values, including
            // object references, alive while Undo replaces the component.
            GameObject snapshot = null;
            if (old != null)
            { snapshot = Instantiate(go); snapshot.hideFlags = HideFlags.HideAndDontSave; snapshot.SetActive(false); }
            var source = snapshot != null ? new SerializedObject(snapshot.GetComponent<ZeldaCharacterAiBase>()) : null;
            if (old != null) Undo.DestroyObjectImmediate(old);
            var replacement = Undo.AddComponent<SquadPatrolCharacterAi>(go);
            var destination = new SerializedObject(replacement);
            if (source != null)
            {
                var field = source.GetIterator();
                if (field.NextVisible(true))
                    do
                    {
                        if (field.propertyPath.StartsWith("m_") || destination.FindProperty(field.propertyPath) == null) continue;
                        destination.CopyFromSerializedProperty(field);
                    } while (field.NextVisible(false));
            }
            destination.FindProperty("civilianPermissionRules").boolValue = civilian;
            destination.ApplyModifiedProperties();
            replacement.enabled = wasEnabled;
            if (snapshot != null) DestroyImmediate(snapshot);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        }
        Undo.CollapseUndoOperations(group);
    }
}
