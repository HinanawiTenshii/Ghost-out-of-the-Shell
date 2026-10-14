using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Copy into Assets/Editor of the isolated smoke project, then execute Run.
[InitializeOnLoad]
public static class Level2VisionPossessionSmoke
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static Level2VisionPossessionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(Level2VisionPossessionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(Level2VisionPossessionSmoke), false)) return;
        SessionState.SetBool(nameof(Level2VisionPossessionSmoke), false);
        try
        {
            Verify();
            foreach (var go in owned) if (go) Object.DestroyImmediate(go);
            Debug.Log("LEVEL2_VISION_POSSESSION_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Call(ZeldaFourWayMover mover, string method, ZeldaFourWayMover target) =>
        typeof(ZeldaFourWayMover).GetMethod(method, Flags).Invoke(mover, new object[] { target });
    static void State(ZeldaCharacterAiBase ai, ZeldaAiState state) =>
        typeof(ZeldaCharacterAiBase).GetField("currentState", Flags).SetValue(ai, state);
    static void Verify()
    {
        GameObject source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Level2NPCS/Idle/Bodyguard-level2.prefab"));
        owned.Add(source);
        var actor = source.GetComponent<ZeldaFourWayMover>();
        source.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        actor.enabled = true;
        int normal = 0, special = 0, withoutAi = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Level2NPCS" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (!prefab.GetComponent<ZeldaCharacterAiBase>()) { withoutAi++; continue; }
            var go = Object.Instantiate(prefab, new Vector3(50, 50), Quaternion.identity);
            owned.Add(go);
            var ai = go.GetComponent<ZeldaCharacterAiBase>();
            var mover = go.GetComponent<ZeldaFourWayMover>();
            var data = go.GetComponent<ZeldaCharacterData>();
            mover.enabled = false;
            bool omni = ai is AutomatonCharacterAi || ai is SkeletonCharacterAi;
            if (omni) special++; else normal++;
            var config = new SerializedObject(ai);
            Check(config.FindProperty("visionRadius").floatValue == 7f, prefab.name + " radius");
            Check(config.FindProperty("peripheralVisionAngle").floatValue == (omni ? 360f : 240f), prefab.name + " angle after Awake");
            Check(config.FindProperty("immediateResponseDistance").floatValue == 5.5f, prefab.name + " immediate response");
            if (omni) Check(config.FindProperty("visionAngle").floatValue == 360f, "preserve omnidirectional vision");
            var dataConfig = new SerializedObject(data);
            dataConfig.FindProperty("possessionCost").intValue = 0;
            dataConfig.ApplyModifiedPropertiesWithoutUndo();
            foreach (ZeldaAiState state in Enum.GetValues(typeof(ZeldaAiState)))
            {
                State(ai, state);
                bool allowed = state != ZeldaAiState.Suspicious && state != ZeldaAiState.Alert && state != ZeldaAiState.Hostile;
                Check((bool)Call(actor, "IsPotentialControlSwitchTarget", mover), prefab.name + " keep prompt for " + state);
                Check((bool)Call(actor, "IsValidControlSwitchTarget", mover) == allowed, prefab.name + " eligibility " + state);
                if (!allowed)
                {
                    Check(!(bool)Call(actor, "TrySwitchControl", mover), "final transfer rejects " + state);
                    Check(!mover.enabled && actor.enabled && !source.GetComponent<ZeldaCharacterData>().IsDead, "rejected transfer preserves control/life");
                }
            }
            // Even if external code changes state during channeling, completion must fail and unlock the NPC.
            State(ai, ZeldaAiState.Idle);
            ai.SetPossessionLock(true);
            typeof(ZeldaFourWayMover).GetField("pendingControlTarget", Flags).SetValue(actor, mover);
            State(ai, ZeldaAiState.Alert);
            Check(!(bool)Call(actor, "TrySwitchControl", mover), "alert during channel rejects completion");
            Check(!ai.IsPossessionLocked, "failed channel releases target lock");
            ai.Stun();
            Check((bool)Call(actor, "IsValidControlSwitchTarget", mover), "stunning alert NPC permits possession");
        }
        Check(normal == 32 && special == 2 && withoutAi == 1, "32 normal, 2 omnidirectional, 1 sandworm");
    }
}
