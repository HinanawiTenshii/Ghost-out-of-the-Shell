using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(SummoningCircle))]
public sealed class SummoningCircleEditor : Editor
{
    private readonly BoxBoundsHandle area = new BoxBoundsHandle
        { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "boundCharacters", "summonPrefab");
        EditorGUILayout.Space();
        var slots = serializedObject.FindProperty("boundCharacters");
        slots.arraySize = SummoningCircle.CrystalCount;
        var seen = new HashSet<string>(); bool duplicate = false;
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(slot, new GUIContent("绑定角色 " + (i + 1)));
            var identity = slot.objectReferenceValue as CharacterPersistentIdentity;
            if (identity != null && !seen.Add(identity.StableId)) duplicate = true;
        }
        var summon = serializedObject.FindProperty("summonPrefab");
        summon.objectReferenceValue = EditorGUILayout.ObjectField("召唤角色预制体", summon.objectReferenceValue, typeof(ZeldaCharacterData), false);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("为每名指定角色创建独立的 Character Persistent Identity 资产，填入该角色数据的 Persistent Identity，再将同一资产放入此处。与钥匙身份一样可跨场景引用；不同角色不要共用同一身份。矩形内每名角色仅贡献一次，六颗水晶全亮后仅召唤一次。", MessageType.Info);
        if (duplicate) EditorGUILayout.HelpBox("存在重复的角色身份：同一身份只能点亮一颗水晶，请绑定六个不同的身份。", MessageType.Warning);
        if (summon.objectReferenceValue == null) EditorGUILayout.HelpBox("尚未指定召唤角色预制体：可以蓄能，但不会生成角色。", MessageType.Warning);
        if (Application.isPlaying)
            EditorGUILayout.LabelField("已点亮 / 召唤完成", ((SummoningCircle)target).LitCrystalCount + " / 6    " + ((SummoningCircle)target).HasSummoned);
    }
    private void OnSceneGUI()
    {
        // Do not access Editor.serializedObject from scene callbacks.
        var circle = (SummoningCircle)target;
        using (new Handles.DrawingScope(new Color(.8f, .4f, 1, .85f), circle.transform.localToWorldMatrix))
        {
            area.center = circle.AreaOffset; area.size = circle.AreaSize;
            area.handleColor = area.wireframeColor = Handles.color;
            EditorGUI.BeginChangeCheck(); area.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(circle, "Resize Summoning Rectangle");
                circle.AreaSize = area.size; circle.AreaOffset = area.center;
                EditorUtility.SetDirty(circle);
                PrefabUtility.RecordPrefabInstancePropertyModifications(circle);
            }
        }
    }
}
