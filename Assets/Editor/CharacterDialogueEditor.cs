using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterDialogue))]
public sealed class CharacterDialogueEditor : Editor
{
    [MenuItem("CONTEXT/ZeldaCharacterData/添加角色对话")]
    private static void AddDialogue(MenuCommand command)
    {
        var character = command.context as ZeldaCharacterData;
        if (character != null && character.GetComponent<CharacterDialogue>() == null)
        {
            var dialogue = Undo.AddComponent<CharacterDialogue>(character.gameObject);
            var serialized = new SerializedObject(dialogue);
            serialized.FindProperty("displayFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(
                AssetDatabase.GUIDToAssetPath("292af55397c443f6a5d7774297dac481"));
            serialized.ApplyModifiedProperties();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("挂载并启用此组件即可对话；禁用或移除即关闭。靠近面向角色按 E，使用鼠标 / 1–4 选择，Esc 退出。", MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script", "nodes", "startNodeId");
        var nodes = serializedObject.FindProperty("nodes");
        var ids = new List<string>();
        for (int i = 0; i < nodes.arraySize; i++) ids.Add(nodes.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);
        DrawNodeLink(serializedObject.FindProperty("startNodeId"), "开始段落", ids);
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            node.isExpanded = EditorGUILayout.Foldout(node.isExpanded, "段落 " + (i+1) + " · " + node.FindPropertyRelative("id").stringValue, true);
            if (GUILayout.Button("删除段落", GUILayout.Width(80)))
            { nodes.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
            EditorGUILayout.EndHorizontal();
            if (node.isExpanded)
            {
                var id = node.FindPropertyRelative("id");
                EditorGUILayout.PropertyField(id, new GUIContent("段落 ID（唯一）"));
                if (string.IsNullOrWhiteSpace(id.stringValue) || ids.FindAll(value => value == id.stringValue).Count > 1)
                    EditorGUILayout.HelpBox("段落 ID 必须非空且唯一。修改 ID 后请同步修改跳转。", MessageType.Error);
                EditorGUILayout.PropertyField(node.FindPropertyRelative("text"), new GUIContent("对话内容"));
                EditorGUILayout.PropertyField(node.FindPropertyRelative("differentActorOpeningText"), new GUIContent("更换角色后的开场差分"));
                var choices = node.FindPropertyRelative("choices");
                if (choices.arraySize == 0) EditorGUILayout.HelpBox("没有选项时自动显示“结束对话”。", MessageType.None);
                if (choices.arraySize > 4) EditorGUILayout.HelpBox("最多显示四个选项，请移除多余选项。", MessageType.Error);
                for (int j = 0; j < choices.arraySize; j++)
                {
                    var choice = choices.GetArrayElementAtIndex(j);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("选项 " + (j+1));
                    bool remove = GUILayout.Button("删除", GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                    if (remove) { choices.DeleteArrayElementAtIndex(j); EditorGUILayout.EndVertical(); break; }
                    var text = choice.FindPropertyRelative("text");
                    EditorGUILayout.LabelField("选项文字");
                    text.stringValue = EditorGUILayout.TextArea(text.stringValue, GUILayout.MinHeight(38));
                    var action = choice.FindPropertyRelative("action");
                    action.enumValueIndex = EditorGUILayout.Popup("功能", action.enumValueIndex, new[] { "跳转到下一段", "退出对话" });
                    if (action.enumValueIndex == 0) DrawNodeLink(choice.FindPropertyRelative("nextNodeId"), "下一段", ids);
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("effect"), new GUIContent("扩展逻辑（可选）", "继承 DialogueOptionEffect，可检查条件并访问当前玩家及 NPC。"));
                    EditorGUILayout.PropertyField(choice.FindPropertyRelative("onSelected"), new GUIContent("选择后事件（可选）"));
                    EditorGUILayout.EndVertical();
                }
                using (new EditorGUI.DisabledScope(choices.arraySize >= 4))
                    if (GUILayout.Button("＋ 添加选项（最多四个）")) AddChoice(choices);
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("＋ 添加对话段落"))
        {
            int index = nodes.arraySize; nodes.InsertArrayElementAtIndex(index);
            var node = nodes.GetArrayElementAtIndex(index);
            node.FindPropertyRelative("id").stringValue = "node_" + Guid.NewGuid().ToString("N").Substring(0,8);
            node.FindPropertyRelative("text").stringValue = "";
            node.FindPropertyRelative("differentActorOpeningText").stringValue = "";
            var choices = node.FindPropertyRelative("choices"); choices.ClearArray(); AddChoice(choices);
            node.isExpanded = true;
        }
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawNodeLink(SerializedProperty value, string label, List<string> ids)
    {
        var labels = new List<string> { "（未设置 / 无效：" + value.stringValue + "）" }; labels.AddRange(ids);
        int current = ids.IndexOf(value.stringValue) + 1;
        int next = EditorGUILayout.Popup(label, current, labels.ToArray());
        if (next > 0) value.stringValue = ids[next-1];
        if (current == 0) EditorGUILayout.HelpBox("请选择一个有效的对话段落。", MessageType.Warning);
    }

    private static void AddChoice(SerializedProperty choices)
    {
        int index = choices.arraySize; choices.InsertArrayElementAtIndex(index);
        var choice = choices.GetArrayElementAtIndex(index);
        choice.FindPropertyRelative("text").stringValue = "再见。";
        choice.FindPropertyRelative("action").enumValueIndex = 1;
        choice.FindPropertyRelative("nextNodeId").stringValue = "";
        choice.FindPropertyRelative("effect").objectReferenceValue = null;
        choice.FindPropertyRelative("onSelected").FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
    }
}
