using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(EnergyCrystal))]
public sealed class EnergyCrystalEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle
        { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "activitySize", "activityOffset", "shatterSound", "shatterVolume");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Activity Rectangle / 矩形活动范围", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Vector2 size = EditorGUILayout.Vector2Field(new GUIContent("Activity Size", "本地宽、高，随水晶旋转与缩放。"), ((EnergyCrystal)target).ActivitySize);
        if (EditorGUI.EndChangeCheck())
            serializedObject.FindProperty("activitySize").vector2Value = new Vector2(Mathf.Max(.2f,size.x),Mathf.Max(.2f,size.y));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("activityOffset"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatterSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatterVolume"));
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("将场景中的骷髅拖入 Skeletons（数量不限）。绿色矩形为 AI 活动范围，可在 Scene 中拖动四边调整宽、高与偏移；玩家操控不受限制。旧半径自动按直径作为初始宽高，原绑定不变。", MessageType.Info);
    }
    private void OnSceneGUI()
    {
        // Scene callbacks must use target, not Editor.serializedObject.
        var crystal = (EnergyCrystal)target;
        using (new Handles.DrawingScope(new Color(.2f,1,.4f,.8f), crystal.transform.localToWorldMatrix))
        {
            boundsHandle.center = crystal.ActivityOffset;
            boundsHandle.size = crystal.ActivitySize;
            boundsHandle.handleColor = boundsHandle.wireframeColor = Handles.color;
            EditorGUI.BeginChangeCheck();
            boundsHandle.DrawHandle();
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(crystal,"Resize Energy Crystal Rectangle");
                crystal.ActivitySize = boundsHandle.size;
                crystal.ActivityOffset = boundsHandle.center;
                EditorUtility.SetDirty(crystal);
                PrefabUtility.RecordPrefabInstancePropertyModifications(crystal);
            }
        }
        Handles.color = new Color(.2f,1,.4f,.8f);
        foreach(var skeleton in crystal.Skeletons)
            if(skeleton != null)Handles.DrawDottedLine(crystal.CrystalCenter,skeleton.LinkCenter,5);
    }
}
