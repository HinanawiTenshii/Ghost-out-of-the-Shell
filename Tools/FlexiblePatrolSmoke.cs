using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in the isolated Unity smoke project. Generated prefab/scene fixtures
// are local to that project; no authored production scene or prefab is saved.
[InitializeOnLoad]
public static class FlexiblePatrolSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator suite;
    static int frame = -1, checks;
    static FlexiblePatrolSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object owner, string name)
    {
        for (var t = owner.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception(name);
    }
    static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args) => typeof(ZeldaPatrolCharacterAi).GetMethod(name, F).Invoke(owner, args);
    static ZeldaPatrolCharacterAi Make(Type type, Vector2 position)
    {
        var go = new GameObject(type.Name); owned.Add(go); go.SetActive(false);
        go.transform.position = position;
        go.AddComponent<CivilianZeldaCharacterData>();
        go.AddComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaPatrolCharacterAi)go.AddComponent(type);
        Set(ai, "enableAwarenessSound", false);
        return ai;
    }
    static void Clear()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    static void LegacyConfig(ZeldaPatrolCharacterAi ai)
    {
        Set(ai, "patrolPoint2Offset", new Vector2(6, -2));
        Set(ai, "patrolPoint3Offset", new Vector2(8, 5));
        Set(ai, "patrolPoint4Offset", new Vector2(-3, 9));
        for (int i = 0; i < 4; i++) Set(ai, "waitAtPoint" + (i + 1) + "Duration", .5f + i);
        Set(ai, "patrolSpeedMultiplier", .63f); Set(ai, "patrolPointTolerance", .17f);
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VerifyEditor(); Clear();
            SessionState.SetBool(nameof(FlexiblePatrolSmoke), true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void VerifyEditor()
    {
        VerifyReducedLegacyEditor();
        var ai = Make(typeof(CivilianPatrolCharacterAi), new Vector2(30, -20)); LegacyConfig(ai);
        var saved = PrefabUtility.SaveAsPrefabAsset(ai.gameObject, "Assets/TestPrefabs/FlexiblePatrol-LegacyFixture.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(saved); owned.Add(instance);
        var patrol = instance.GetComponent<ZeldaPatrolCharacterAi>();
        var data = new SerializedObject(patrol);
        data.FindProperty("patrolPoint3Offset").vector2Value = new Vector2(17, -11);
        data.FindProperty("waitAtPoint3Duration").floatValue = 4.75f;
        data.ApplyModifiedProperties();
        string original = EditorJsonUtility.ToJson(patrol);
        var editor = Editor.CreateEditor(patrol);
        Check(editor is ZeldaPatrolCharacterAiEditor, "civilian uses shared waypoint inspector");
        Object.DestroyImmediate(editor);
        Check(original == EditorJsonUtility.ToJson(patrol), "opening inspector does not migrate or dirty the route");
        Undo.IncrementCurrentGroup();
        ZeldaPatrolCharacterAiEditor.UpgradeRoute(data); data.ApplyModifiedProperties();
        Undo.FlushUndoRecordObjects();
        Check(data.FindProperty("customPatrolPoints").arraySize == 4, "migration retains four points");
        Check(data.FindProperty("customPatrolPoints").GetArrayElementAtIndex(2).FindPropertyRelative("offset").vector2Value == new Vector2(17, -11), "resolved scene-instance coordinate override retained");
        Check(data.FindProperty("customPatrolPoints").GetArrayElementAtIndex(2).FindPropertyRelative("waitDuration").floatValue == 4.75f, "resolved scene-instance wait override retained");
        Check(!new SerializedObject(saved.GetComponent<ZeldaPatrolCharacterAi>()).FindProperty("useCustomPatrolRoute").boolValue, "source prefab not changed by instance conversion");
        Undo.PerformUndo(); data.Update();
        Check(!data.FindProperty("useCustomPatrolRoute").boolValue, "Undo returns to legacy serialization");
        Check(data.FindProperty("patrolPoint3Offset").vector2Value == new Vector2(17, -11), "Undo preserves prior instance overrides");
        Undo.PerformRedo(); data.Update();
        Check(data.FindProperty("useCustomPatrolRoute").boolValue, "Redo restores custom route");
        var points = data.FindProperty("customPatrolPoints"); points.arraySize = 7;
        for (int i = 4; i < 7; i++)
        {
            points.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector2Value = new Vector2(i * 3, i * -2);
            points.GetArrayElementAtIndex(i).FindPropertyRelative("waitDuration").floatValue = i + .25f;
        }
        points.MoveArrayElement(6, 1); points.DeleteArrayElementAtIndex(4); data.ApplyModifiedProperties();
        Check(points.arraySize == 6, "add, reorder and remove are serialized");
        Check(points.GetArrayElementAtIndex(1).FindPropertyRelative("offset").vector2Value == new Vector2(18, -12), "reorder retains coordinate/wait pair");
        Check(points.GetArrayElementAtIndex(1).FindPropertyRelative("waitDuration").floatValue == 6.25f, "reorder retains wait");
        EditorSceneManager.SaveScene(instance.scene, "Assets/TestPrefabs/FlexiblePatrol-Fixture.unity");
        EditorSceneManager.OpenScene("Assets/TestPrefabs/FlexiblePatrol-Fixture.unity");
        var reloaded = Array.Find(Object.FindObjectsOfType<CivilianPatrolCharacterAi>(true), a => new SerializedObject(a).FindProperty("useCustomPatrolRoute").boolValue);
        Check(reloaded != null, "custom flag survives scene save/reload");
        var reloadData = new SerializedObject(reloaded);
        Check(reloadData.FindProperty("customPatrolPoints").arraySize == 6, "variable length survives scene save/reload");
        Selection.activeGameObject = reloaded.gameObject;
        SquadPatrolCharacterAiEditor.ConvertSelected();
        var squad = Selection.activeGameObject.GetComponent<SquadPatrolCharacterAi>();
        Check(squad != null, "civilian to squad conversion works");
        var squadData = new SerializedObject(squad);
        Check(squadData.FindProperty("useCustomPatrolRoute").boolValue && squadData.FindProperty("customPatrolPoints").arraySize == 6, "squad editor conversion preserves custom path");
        Check(squadData.FindProperty("civilianPermissionRules").boolValue, "squad conversion retains permission behavior");
        editor = Editor.CreateEditor(squad);
        Check(editor is ZeldaPatrolCharacterAiEditor && editor is SquadPatrolCharacterAiEditor, "squad inspector has list editing");
        Object.DestroyImmediate(editor);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("FLEXIBLE_PATROL_EDITOR_PASS checks=" + checks);
    }
    static void VerifyReducedLegacyEditor()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Patrol/Engineer-level2.prefab");
        Check(existing && Get<int>(existing.GetComponent<ZeldaPatrolCharacterAi>(), "legacyPatrolPointCount") == 4,
            "old prefab without new serialized field still defaults to four points");
        foreach (int count in new[] { 2, 3, 4 })
        {
            var ai = Make(typeof(CivilianPatrolCharacterAi), Vector2.zero); LegacyConfig(ai);
            var data = new SerializedObject(ai);
            Undo.IncrementCurrentGroup();
            data.FindProperty("legacyPatrolPointCount").intValue = count;
            data.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Check(Get<Vector2>(ai,"patrolPoint4Offset") == new Vector2(-3,9) && Get<float>(ai,"waitAtPoint4Duration") == 3.5f,
                "reduced count preserves hidden coordinates and waits");
            if (count < 4)
            {
                Undo.PerformUndo(); data.Update();
                Check(data.FindProperty("legacyPatrolPointCount").intValue == 4, "Undo restores original point count");
                Undo.PerformRedo(); data.Update();
                Check(data.FindProperty("legacyPatrolPointCount").intValue == count, "Redo restores reduced count");
            }
            ZeldaPatrolCharacterAiEditor.UpgradeRoute(data); data.ApplyModifiedProperties();
            var points = data.FindProperty("customPatrolPoints");
            Check(points.arraySize == count, "conversion copies only enabled points " + count);
            Check(points.GetArrayElementAtIndex(1).FindPropertyRelative("offset").vector2Value == new Vector2(6,-2) &&
                points.GetArrayElementAtIndex(1).FindPropertyRelative("waitDuration").floatValue == 1.5f, "conversion preserves coordinates and waits");
            var editor = Editor.CreateEditor(ai);
            typeof(ZeldaPatrolCharacterAiEditor).GetMethod("EnsurePointList",F).Invoke(editor,null);
            var list = (UnityEditorInternal.ReorderableList)typeof(ZeldaPatrolCharacterAiEditor).GetField("pointList",F).GetValue(editor);
            while(list.serializedProperty.arraySize > 2)
            {
                Check(list.onCanRemoveCallback(list), "more than two can remove");
                list.index = list.serializedProperty.arraySize-1; list.onRemoveCallback(list);
            }
            Check(!list.onCanRemoveCallback(list), "minus button disabled at two points");
            list.onRemoveCallback(list);
            Check(list.serializedProperty.arraySize==2, "remove callback also enforces minimum");
            list.onAddCallback(list); Check(list.serializedProperty.arraySize==3, "can add again after reaching minimum");
            Object.DestroyImmediate(editor);
        }
        Clear();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(FlexiblePatrolSmoke), false))
            { SessionState.SetBool(nameof(FlexiblePatrolSmoke), false); suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Clear(); Debug.Log("FLEXIBLE_PATROL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        foreach (var type in new[] { typeof(ZeldaPatrolCharacterAi), typeof(CivilianPatrolCharacterAi) })
        foreach (int count in new[] { 2, 3, 4 })
        {
            var actor = Make(type,new Vector2(30,-20)); LegacyConfig(actor);
            Set(actor,"legacyPatrolPointCount",count); actor.gameObject.SetActive(true);
            var allPoints = new[] { new Vector2(30,-20), new Vector2(36,-22), new Vector2(38,-15), new Vector2(27,-11) };
            var expected = new Vector2[count]; var waits = new float[count];
            Array.Copy(allPoints,expected,count); for (int i=0;i<count;i++) waits[i]=.5f+i;
            VerifyLoop(actor,expected,waits); VerifyInheritance(actor,count);
            var bounds = Get<Bounds>(actor,"streamingRouteBounds");
            for (int i=0;i<count;i++) Check(bounds.Contains(expected[i]),"streaming bounds include active point");
            if(count==2) Check(!bounds.Contains(allPoints[3]),"hidden point cannot expand streaming bounds");
            Call(actor,"OnStateEntered",ZeldaAiState.Recovery,ZeldaAiState.Idle);
            Check(Get<Vector2[]>(actor,"patrolPoints").Length==count && Get<int>(actor,"currentPatrolPointIndex")==1,
                "recovery respects reduced route");
            Set(actor,"legacyPatrolPointCount",4); Call(actor,"RebuildPatrolPoints");
            Check(Get<Vector2[]>(actor,"patrolPoints")[3]==allPoints[3] && (float)Call(actor,"GetWaitDuration",3)==3.5f,
                "restoring four points restores hidden coordinates and waits");
            Clear();
        }
        foreach (var type in new[] { typeof(ZeldaPatrolCharacterAi), typeof(CivilianPatrolCharacterAi) })
        {
            var legacy = Make(type, new Vector2(30, -20)); LegacyConfig(legacy);
            legacy.gameObject.SetActive(true);
            Check(!Get<bool>(legacy, "useCustomPatrolRoute"), "legacy mode is still default after Awake");
            Check(Get<int>(legacy, "currentPatrolPointIndex") == 1 && Get<float>(legacy, "waitTimer") == 0, "legacy initial destination/timing unchanged");
            var expected = new[] { new Vector2(30, -20), new Vector2(36, -22), new Vector2(38, -15), new Vector2(27, -11) };
            VerifyLoop(legacy, expected, new[] { .5f, 1.5f, 2.5f, 3.5f });
            Call(legacy, "OnStateEntered", ZeldaAiState.Recovery, ZeldaAiState.Idle);
            Check(Get<int>(legacy, "currentPatrolPointIndex") == 1 && Get<float>(legacy, "waitTimer") == .5f, "legacy recovery destination/wait unchanged");
            Clear();
        }
        // More than four points, empty paths, single-point paths and non-origin
        // first points all use the same idle loop without changing combat AI.
        foreach (int count in new[] { 0, 1, 2, 4, 7, 32 })
        {
            var actor = Make(typeof(ZeldaPatrolCharacterAi), new Vector2(-40, 15));
            var config = new ZeldaPatrolCharacterAi.PatrolWaypoint[count];
            var expected = new Vector2[count]; var waits = new float[count];
            for (int i = 0; i < count; i++)
            {
                config[i] = new ZeldaPatrolCharacterAi.PatrolWaypoint { offset = new Vector2(i * 2, (i % 3) * 3), waitDuration = i * .1f };
                expected[i] = new Vector2(-40, 15) + config[i].offset; waits[i] = config[i].waitDuration;
            }
            Set(actor, "customPatrolPoints", config); Set(actor, "useCustomPatrolRoute", true);
            actor.gameObject.SetActive(true);
            Check(Get<int>(actor, "currentPatrolPointIndex") == (count > 1 ? 1 : 0), "custom first destination " + count);
            VerifyLoop(actor, expected, waits);
            if (count > 0)
            {
                config[0].offset = new Vector2(1, 1);
                Call(actor, "OnStateEntered", ZeldaAiState.Recovery, ZeldaAiState.Idle);
                Check(Get<int>(actor, "currentPatrolPointIndex") == 0 && Get<float>(actor, "waitTimer") == 0, "non-origin first point must not be skipped");
            }
            VerifyInheritance(actor, count);
            Clear();
        }
        Debug.Log("FLEXIBLE_PATROL_RUNTIME_PASS checks=" + checks);
        // Let the manual transform/destruction fixtures and the first expensive
        // Play Mode frame settle before a separate real-time movement test.
        Physics2D.SyncTransforms();
        float readyAt = Time.time + .5f;
        while (Time.time < readyAt) yield return null;
        // Run the existing physical squad/leadership regression suite too.
        var squadTest = (IEnumerator)typeof(SquadPatrolSmoke).GetMethod("Verify", S).Invoke(null, null);
        while (squadTest.MoveNext()) yield return squadTest.Current;
        typeof(SquadPatrolSmoke).GetMethod("Clear", S).Invoke(null, null);
        Debug.Log("FLEXIBLE_PATROL_SQUAD_REGRESSION_PASS");
        yield return null;
        var squad = (SquadPatrolCharacterAi[])typeof(SquadPatrolSmoke).GetMethod("MakeSquad", S).Invoke(null, new object[] { 2, new Vector2(0, 100) });
        var customRoute = new ZeldaPatrolCharacterAi.PatrolWaypoint[7];
        for (int i = 0; i < customRoute.Length; i++)
            customRoute[i] = new ZeldaPatrolCharacterAi.PatrolWaypoint { offset = new Vector2(i * 4, (i % 2) * 3), waitDuration = i + .3f };
        Set(squad[0], "useCustomPatrolRoute", true); Set(squad[0], "customPatrolPoints", customRoute);
        Call(squad[0], "RebuildPatrolPoints");
        yield return null; yield return null;
        var original = (Vector2[])Get<Vector2[]>(squad[0], "patrolPoints").Clone();
        squad[0].GetComponent<ZeldaCharacterData>().TakeDamage(999);
        yield return null; yield return null;
        Check(squad[1].CurrentLeader == squad[1], "seven-point captain death appoints successor");
        var inherited = Get<Vector2[]>(squad[1], "patrolPoints");
        Check(inherited.Length == 7, "real squad inherits all seven points");
        for (int i = 0; i < inherited.Length; i++)
        {
            Check(inherited[i] == original[i], "real squad preserves absolute custom route");
            Check((float)Call(squad[1], "GetWaitDuration", i) == customRoute[i].waitDuration, "real squad preserves every custom wait");
        }
        Call(squad[1], "OnStateEntered", ZeldaAiState.Recovery, ZeldaAiState.Idle);
        Check(Get<Vector2[]>(squad[1], "patrolPoints").Length == 7, "recovery keeps inherited custom route");
        squad[1].GetComponent<ZeldaFourWayMover>().enabled = true;
        squad[1].enabled = false;
        yield return null; yield return null;
        Check(squad[2].CurrentLeader == squad[2] && Get<Vector2[]>(squad[2], "patrolPoints").Length == 7, "possession appoints second successor without truncating route");
        typeof(SquadPatrolSmoke).GetMethod("Clear", S).Invoke(null, null);
    }
    static void VerifyLoop(ZeldaPatrolCharacterAi actor, Vector2[] expected, float[] waits)
    {
        var actual = Get<Vector2[]>(actor, "patrolPoints");
        Check(actual.Length == expected.Length, "full route length");
        for (int i = 0; i < actual.Length; i++) Check(actual[i] == expected[i], "absolute route point " + i);
        if (actual.Length == 0)
        {
            for (int i = 0; i < 10; i++) Call(actor, "TickIdle", .02f);
            Call(actor, "OnStateEntered", ZeldaAiState.Recovery, ZeldaAiState.Idle);
            Check(Get<Vector2>(actor, "moveDirection") == Vector2.zero, "empty route stays idle"); return;
        }
        for (int i = 0; i < actual.Length * 2; i++)
        {
            int index = Get<int>(actor, "currentPatrolPointIndex");
            actor.GetComponent<Rigidbody2D>().position = actual[index]; actor.transform.position = actual[index];
            Set(actor, "waitTimer", 0f); Call(actor, "TickIdle", .02f);
            Check(Get<int>(actor, "currentPatrolPointIndex") == (index + 1) % actual.Length, "point traversal wraps in order");
            Check(Mathf.Approximately(Get<float>(actor, "waitTimer"), waits[index]), "point wait retained");
            if (waits[index] > 0)
            {
                Call(actor, "TickIdle", waits[index] * .4f);
                Check(Mathf.Approximately(Get<float>(actor, "waitTimer"), waits[index] * .6f), "remaining wait ticks without advancing");
            }
        }
        int savedIndex = Get<int>(actor, "currentPatrolPointIndex");
        float savedWait = Get<float>(actor, "waitTimer");
        actor.gameObject.SetActive(false); actor.gameObject.SetActive(true);
        Check(savedIndex == Get<int>(actor, "currentPatrolPointIndex") && savedWait == Get<float>(actor, "waitTimer"), "streaming re-enable preserves patrol progress");
    }
    static void VerifyInheritance(ZeldaPatrolCharacterAi captain, int count)
    {
        var successor = Make(typeof(ZeldaPatrolCharacterAi), new Vector2(500, 500)); successor.gameObject.SetActive(true);
        var snapshotType = typeof(ZeldaPatrolCharacterAi).GetNestedType("PatrolRouteSnapshot", BindingFlags.NonPublic);
        var snapshot = Activator.CreateInstance(snapshotType, true);
        Call(captain, "CapturePatrolRoute", snapshot);
        Call(successor, "ApplyPatrolRoute", snapshot);
        var points = Get<Vector2[]>(captain, "patrolPoints"); var copied = Get<Vector2[]>(successor, "patrolPoints");
        Check(copied.Length == count, "successor retains arbitrary route length");
        Check(!ReferenceEquals(points, copied), "successor owns runtime points");
        for (int i = 0; i < count; i++)
        {
            Check(copied[i] == points[i], "successor inherits original absolute positions");
            Check((float)Call(successor, "GetWaitDuration", i) == (float)Call(captain, "GetWaitDuration", i), "successor inherits all waits");
        }
        Check(Get<int>(successor, "currentPatrolPointIndex") == Get<int>(captain, "currentPatrolPointIndex"), "successor inherits progress");
        Check(Get<float>(successor, "waitTimer") == Get<float>(captain, "waitTimer"), "successor inherits remaining wait");
        var storage = Field(snapshot, "points").GetValue(snapshot);
        Call(successor, "CapturePatrolRoute", snapshot);
        Check(ReferenceEquals(storage, Field(snapshot, "points").GetValue(snapshot)), "unchanged route capture reuses buffers");
    }
}
