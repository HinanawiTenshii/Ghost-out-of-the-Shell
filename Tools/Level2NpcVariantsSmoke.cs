using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Level2NpcVariantsSmoke
{
    [Serializable] public class Entry { public string name, idle, patrol, originalPath, originalGuid, alternatePath, alternateGuid; public bool civilian; }
    [Serializable] public class Manifest { public Entry[] entries; }
    static IEnumerator test;
    static int checks, frame = -1;
    static readonly List<GameObject> owned = new List<GameObject>();
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static Level2NpcVariantsSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("Level2NpcVariantsSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("Level2NpcVariantsSmoke", false))
            { SessionState.SetBool("Level2NpcVariantsSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("LEVEL2_NPC_VARIANTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Clear() { foreach (var go in owned) if (go) Object.DestroyImmediate(go); owned.Clear(); }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        var entries = JsonUtility.FromJson<Manifest>("{\"entries\":" + File.ReadAllText("Assets/Editor/Level2NpcVariants.json") + "}").entries;
        Check(entries.Length == 16, "16 ordinary NPC pairs");
        var roots = new List<GameObject>(); var starts = new List<Vector2>(); var isPatrol = new List<bool>();
        var catalog = ScriptableObject.CreateInstance<SaveGameCatalog>();
        foreach (var entry in entries)
        {
            var idle = AssetDatabase.LoadAssetAtPath<GameObject>(entry.idle);
            var patrol = AssetDatabase.LoadAssetAtPath<GameObject>(entry.patrol);
            Check(idle && patrol, entry.name + " both import");
            Check(AssetDatabase.AssetPathToGUID(entry.originalPath) == entry.originalGuid, entry.name + " original GUID retained");
            Check(AssetDatabase.AssetPathToGUID(entry.alternatePath) == entry.alternateGuid, entry.name + " new GUID");
            var idleData = idle.GetComponent<ZeldaCharacterData>();
            var patrolData = patrol.GetComponent<ZeldaCharacterData>();
            Check(idleData && patrolData && idleData.GetType() == patrolData.GetType(), entry.name + " same character type");
            Check(EditorJsonUtility.ToJson(idleData) == EditorJsonUtility.ToJson(patrolData), entry.name + " identical appearance/combat properties");
            catalog.characterPrefabs = new[] { idle, patrol };
            var type = idleData.GetType().AssemblyQualifiedName;
            Check(idle.name != patrol.name && catalog.FindCharacter(type, idle.name) == idle &&
                catalog.FindCharacter(type, patrol.name) == patrol, entry.name + " save catalog distinguishes modes");
            for (int mode = 0; mode < 2; mode++)
            {
                var source = mode == 0 ? idle : patrol;
                var position = new Vector3(roots.Count / 2 * 14, mode * 18, 0);
                var go = Object.Instantiate(source, position, Quaternion.identity);
                owned.Add(go); roots.Add(go); starts.Add(position); isPatrol.Add(mode == 1);
                foreach (var component in go.GetComponentsInChildren<Component>(true))
                    Check(component != null, entry.name + " no missing components");
                var ais = go.GetComponents<ZeldaCharacterAiBase>();
                Check(ais.Length == 1 && ais[0].enabled, entry.name + " exactly one active AI");
                var expected = mode == 0 ? (entry.civilian ? typeof(CivilianIdleCharacterAi) : typeof(ZeldaCharacterAiBase)) :
                    (entry.civilian ? typeof(CivilianPatrolCharacterAi) : typeof(ZeldaPatrolCharacterAi));
                Check(ais[0].GetType() == expected, entry.name + " correct AI mode");
                if (mode == 1)
                {
                    // Existing civilian assets intentionally have zero offsets.
                    // Preserve those authored defaults, but exercise their route
                    // on this disposable instance to verify the selected AI.
                    var so = new SerializedObject(ais[0]);
                    if (so.FindProperty("patrolPoint2Offset").vector2Value == Vector2.zero &&
                        so.FindProperty("patrolPoint3Offset").vector2Value == Vector2.zero &&
                        so.FindProperty("patrolPoint4Offset").vector2Value == Vector2.zero)
                    {
                        so.FindProperty("patrolPoint2Offset").vector2Value = new Vector2(2, 0);
                        so.FindProperty("patrolPoint3Offset").vector2Value = new Vector2(2, 2);
                        so.FindProperty("patrolPoint4Offset").vector2Value = new Vector2(0, 2);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        typeof(ZeldaPatrolCharacterAi).GetMethod("RebuildPatrolPoints", Flags).Invoke(ais[0], null);
                    }
                }
                if (entry.civilian)
                    Check((int)expected.GetProperty("MinimumPermissionViolationDifference", Flags).GetValue(ais[0]) == 2,
                        entry.name + " civilian permission policy retained");
                Check(!go.GetComponent<ZeldaFourWayMover>().enabled, entry.name + " no player input");
            }
        }
        Object.DestroyImmediate(catalog);
        float end = Time.time + 1.25f;
        while (Time.time < end) yield return null;
        for (int i = 0; i < roots.Count; i++)
        {
            float distance = Vector2.Distance(starts[i], roots[i].GetComponent<Rigidbody2D>().position);
            Check(isPatrol[i] ? distance > .1f : distance < .02f, roots[i].name + " actual idle/patrol motion: " + distance);
            Check(roots[i].GetComponent<ZeldaCharacterAiBase>().CurrentState == ZeldaAiState.Idle, "ordinary idle activity does not force hostile state");
        }
    }
}
