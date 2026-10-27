using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class InvestigationSearchSmoke
{
    const string Key = nameof(InvestigationSearchSmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
    static int checks;
    static readonly List<GameObject> owned = new List<GameObject>();
    static readonly string[] Prefabs = {
        "Assets/Prefabs/Level0NPCs/Warden-level0.prefab",
        "Assets/Prefabs/Level0NPCs/SwordGuy-Walking.prefab",
        "Assets/Prefabs/Level0NPCs/SwordGuy-level0.prefab",
        "Assets/Prefabs/Level0NPCs/Prisoner.prefab",
        "Assets/Prefabs/Level0NPCs/AdvancedGuard-Patrol.prefab",
        "Assets/Prefabs/Level0NPCs/AdvancedGuard-level0.prefab",
        "Assets/Prefabs/SwordGuy.prefab",
        "Assets/Prefabs/Level2NPCS/Automaton-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Skeleton-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Turret-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/AdvancedArabSoldier-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/AdvancedEngineer-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/AdvancedPaladin-Patrol.prefab",
        "Assets/Prefabs/Level1NPCS/Behemoth.prefab",
        "Assets/Prefabs/Level1NPCS/Blacksmith.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Alchemist-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/ArabSoldier-level2-Patrol.prefab",
        "Assets/Prefabs/Level1NPCS/Civilian-Base 1.prefab",
        "Assets/Prefabs/Level1NPCS/AdvancedGuard-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Alchemist-level2-Idle.prefab",
        "Assets/Prefabs/Level1NPCS/AdvancedGuard-level1.prefab",
        "Assets/Prefabs/Level1NPCS/Civilian-1author.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/ArabStrongman-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Blacksmith-level2.prefab",
        "Assets/Prefabs/Level1NPCS/LegendaryGuardian-Patrol.prefab",
        "Assets/Prefabs/Level1NPCS/LegendaryGuardian.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/AdvancedPaladin.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Chef-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Bodyguard-level2-Patrol.prefab",
        "Assets/Prefabs/Level1NPCS/Noble.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/AdvancedEngineer-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/Paladin-Squad.prefab",
        "Assets/Prefabs/Level1NPCS/Strongman-Level1.prefab",
        "Assets/Prefabs/Level1NPCS/SwordGuy-level1.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/AdvancedArabSoldier-level2.prefab",
        "Assets/Prefabs/Level1NPCS/SwordGuy-WalkingLevel1.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/HeavyPaladin-level2-Suqad.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/ArabSoldier-Squad.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/EliteArabSoldier-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/AdvancedPaladin-Squad.prefab",
        "Assets/Prefabs/Level2NPCS/Troops/Bodyguard-Squad.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Engineer-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Dancer-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/EliteArabSoldier-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/EasternSwordsman-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Civilian-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Dancer-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/DesertScout-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/EasternSwordsman.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Engineer-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Chef-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/HeavyPaladin-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/DesertScout-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Merchant-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Paladin-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Bodyguard-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/EliteArabSoldier-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Priest-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Paladin.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Scribe-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Blacksmith-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Wizard-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/ArabStrongman-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Wizard-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Merchant-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Strongman-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/ArabSoldier-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Strongman-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Patrol/Skeleton-level2-Patrol.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/HeavyPaladin-level2.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/StrangeMerchant-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Scribe-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/StrangeBlacksmith-level2-Idle.prefab",
        "Assets/Prefabs/Level2NPCS/Idle/Priest-level2-Idle.prefab",
    };
    static InvestigationSearchSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); checks++; }
    static T Get<T>(ZeldaCharacterAiBase ai, string field) => (T)typeof(ZeldaCharacterAiBase).GetField(field, F).GetValue(ai);
    static void Set(ZeldaCharacterAiBase ai, string field, object value) => typeof(ZeldaCharacterAiBase).GetField(field, F).SetValue(ai, value);
    static object Call(ZeldaCharacterAiBase ai, string name, params object[] args) => typeof(ZeldaCharacterAiBase).GetMethod(name, F).Invoke(ai, args);
    static void CheckConfiguration(ZeldaCharacterAiBase ai, string label)
    {
        var data = new SerializedObject(ai);
        Check(data.FindProperty("investigationDuration").floatValue == 16f, label + " 16 seconds");
        Check(data.FindProperty("investigationWaypointPauseDuration").floatValue == 3.5f, label + " 3.5 second pauses");
        Check(data.FindProperty("sharedSearchWaypointPauseDuration").floatValue == 2f, label + " shared search two-second pauses");
        Check(data.FindProperty("sharedSearchMinimumMoveDistance").floatValue == 1.2f, label + " shared search minimum movement defaults");
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        foreach (string path in Prefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab != null, path + " imports");
            foreach (var ai in prefab.GetComponentsInChildren<ZeldaCharacterAiBase>(true)) CheckConfiguration(ai, path);
        }
        EditorSceneManager.OpenScene("Assets/Scenes/Level1/Level1-Garden.unity");
        foreach (var ai in Object.FindObjectsOfType<ZeldaCharacterAiBase>(true)) CheckConfiguration(ai, ai.name + " scene override");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key + ".checks", checks); SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        if (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) return;
        SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key + ".checks", 0);
        try { Verify(); Debug.Log("INVESTIGATION_SEARCH_PASS checks=" + checks); Clear(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static GameObject Actor(string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        owned.Add(go); go.transform.position = position; go.GetComponent<Rigidbody2D>().position = position;
        return go;
    }
    static ZeldaCharacterAiBase Npc()
    {
        var go = Actor("Investigator", Vector2.zero);
        go.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = go.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, "enableAwarenessSound", false);
        Set(ai, "solidCollisionLayers", (LayerMask)0);
        Set(ai, "visionObstacleLayers", (LayerMask)0);
        return ai;
    }
    static void Move(ZeldaCharacterAiBase ai, Vector2 point)
    { ai.transform.position = point; ai.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }
    static void Clear()
    {
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
    }
    static GameObject Wall(float x)
    {
        var go = new GameObject("Search wall"); owned.Add(go);
        go.layer = LayerMask.NameToLayer("Blocks"); go.transform.position = new Vector3(x, 0, 0);
        go.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 8f); Physics2D.SyncTransforms(); return go;
    }
    static void Verify()
    {
        Time.timeScale = 1;
        var ai = Npc();
        CheckConfiguration(ai, "new component defaults");
        ai.InvestigatePosition(new Vector2(3, 0));
        Check(ai.CurrentState == ZeldaAiState.Search && ai.CaptureSaveState().handgunTraveling, "attraction begins with approach");
        Check(ai.IsReactingToAttraction, "unseen idle attraction first pauses");
        Call(ai, "TickSearch", 1f);
        Check(!ai.IsReactingToAttraction, "approach may begin after one-second reaction");
        Call(ai, "UpdateTargetAndState", 40f);
        Check(ai.CurrentState == ZeldaAiState.Search && ai.CaptureSaveState().searchTime == 0f, "travel does not consume search duration");
        Move(ai, ai.CaptureSaveState().handgunApproach);
        Call(ai, "TickSearch", .02f);
        Check(ai.CaptureSaveState().handgunPausing && ai.CaptureSaveState().searchTime == 0f, "arrival begins pause and local timer");
        Vector2 heading = ai.FacingDirection;
        Call(ai, "TickSearch", 3.49f);
        Check(ai.CaptureSaveState().handgunPausing && Get<Vector2>(ai, "moveDirection") == Vector2.zero &&
            Vector2.Angle(heading, ai.FacingDirection) < .01f, "wait 3.5 seconds without turning back at incident");
        Call(ai, "TickSearch", .02f); Call(ai, "UpdateFacingFromMovement");
        float turn = Vector2.Angle(heading, ai.FacingDirection);
        Check(!ai.CaptureSaveState().handgunPausing && turn >= 59.99f && turn <= 120.01f, "first movement turns 60 to 120 degrees");
        Move(ai, ai.CaptureSaveState().searchWaypoint); heading = ai.FacingDirection;
        Call(ai, "TickSearch", .02f);
        Check(ai.CaptureSaveState().handgunPausing && Vector2.Angle(ai.FacingDirection, heading) < .01f, "waypoint arrival preserves movement heading");

        // Continuous travel around the same incident; both left and right turns and no drift.
        int moved = 0, left = 0, right = 0;
        UnityEngine.Random.InitState(37019);
        for (int i = 0; i < 160; i++)
        {
            Vector2 before = ai.FacingDirection, origin = ai.GetComponent<Rigidbody2D>().position;
            Call(ai, "TickSearch", 3.51f); Call(ai, "UpdateFacingFromMovement");
            var state = ai.CaptureSaveState();
            if ((state.searchWaypoint - origin).sqrMagnitude < .0001f)
            {
                Check(Vector2.Angle(before, ai.FacingDirection) < .01f, "no candidate leaves orientation unchanged");
                continue;
            }
            float angle = Vector2.Angle(before, state.searchWaypoint - origin);
            Check(angle >= 59.99f && angle <= 120.01f, "repeated waypoint target satisfies turn limits");
            Check(Vector2.Angle(ai.FacingDirection, state.searchWaypoint - origin) < .05f, "movement follows chosen view direction");
            Check(Vector2.Distance(state.searchWaypoint, state.searchCenter) <= Get<float>(ai, "searchRadius") + .001f,
                "search stays inside original radius");
            Check(Vector2.Distance(state.searchWaypoint, state.handgunIncident) >= Get<float>(ai, "investigationApproachMinimumRadius") - .001f,
                "safe incident stand-off retained");
            if (Vector2.SignedAngle(before, ai.FacingDirection) > 0) left++; else right++;
            moved++; Move(ai, state.searchWaypoint);
            heading = ai.FacingDirection; Call(ai, "TickSearch", .02f);
            Check(ai.CaptureSaveState().handgunPausing && Vector2.Angle(heading, ai.FacingDirection) < .01f,
                "every reached point pauses without extra turn");
        }
        Check(moved > 80 && left > 10 && right > 10, "random search keeps moving and turns both ways");

        Set(ai, "investigationElapsed", 10.75f); Set(ai, "investigationPauseTimer", 2.5f);
        var saved = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(ai.CaptureSaveState()));
        ai.ApplySaveState(saved);
        Check(ai.CaptureSaveState().searchTime == 10.75f && ai.CaptureSaveState().handgunPause == 2.5f &&
            ai.FacingDirection == saved.facing, "save/load retains duration, pause, waypoint and facing");
        Call(ai, "TickSearch", .99f);
        Check(ai.CaptureSaveState().handgunPausing, "restored pause waits remaining time");
        Set(ai, "investigationElapsed", 0f);
        Call(ai, "UpdateTargetAndState", 15.99f);
        Check(ai.CurrentState == ZeldaAiState.Search, "search lasts through 15.99 seconds");
        Call(ai, "UpdateTargetAndState", .02f);
        Check(ai.CurrentState == ZeldaAiState.Recovery, "search expires after 16 seconds");

        // Ordinary post-hostility search still uses unrestricted sampling and no observation pauses.
        Move(ai, Vector2.zero); Call(ai, "BeginSearch"); Set(ai, "facingDirection", Vector2.up);
        UnityEngine.Random.InitState(203);
        Vector2 expected = ai.CaptureSaveState().searchCenter + UnityEngine.Random.insideUnitCircle * Get<float>(ai, "searchRadius");
        UnityEngine.Random.InitState(203); Call(ai, "ChooseNextSearchWaypoint");
        Check(ai.CaptureSaveState().searchWaypoint == expected && ai.FacingDirection == Vector2.up,
            "normal search sampling and orientation unchanged");
        Move(ai, expected); Call(ai, "TickSearch", .02f);
        Check(!ai.CaptureSaveState().handgunPausing, "normal search does not gain investigation pauses");

        // Obstructed sectors must wait, not spin or choose a point through a wall.
        Move(ai, Vector2.zero);
        Set(ai, "solidCollisionLayers", (LayerMask)LayerMask.GetMask("Blocks"));
        Set(ai, "isDoorIncidentInvestigation", true);
        Set(ai, "isTravelingToInvestigation", false);
        Set(ai, "searchCenter", Vector2.zero);
        Set(ai, "investigationIncidentPosition", new Vector2(0, -10));
        Set(ai, "facingDirection", Vector2.up);
        var leftWall = Wall(-.55f); var rightWall = Wall(.55f);
        Call(ai, "ChooseNextSearchWaypoint");
        Check(ai.CaptureSaveState().searchWaypoint == Vector2.zero && ai.FacingDirection == Vector2.up,
            "blocked turn sectors wait without invalid turn");
        Call(ai, "BeginInvestigationPause"); Call(ai, "TickSearch", 3.49f);
        Check(ai.CaptureSaveState().handgunPausing && Get<Vector2>(ai, "moveDirection") == Vector2.zero,
            "blocked retry is bounded by pause");
        Object.DestroyImmediate(leftWall); Physics2D.SyncTransforms();
        for (int i = 0; i < 100; i++)
        {
            Set(ai, "facingDirection", Vector2.up); Call(ai, "ChooseNextSearchWaypoint");
            Vector2 target = ai.CaptureSaveState().searchWaypoint;
            if (target == Vector2.zero) continue;
            Check(target.x < 0 && (bool)Call(ai, "IsBodyPathSegmentClear", Vector2.zero, target, 0f),
                "new target uses unobstructed sector");
        }
        Object.DestroyImmediate(rightWall); Physics2D.SyncTransforms();
        Set(ai, "solidCollisionLayers", (LayerMask)0);
        ai.InvestigateHandgunShot(new Vector2(3, 0));
        Move(ai, ai.CaptureSaveState().handgunApproach); Call(ai, "TickSearch", .02f);
        Set(ai, "facingDirection", Vector2.up);
        var player = Actor("Visible player", ai.transform.position + Vector3.up).GetComponent<ZeldaFourWayMover>();
        Physics2D.SyncTransforms(); Call(ai, "UpdateTargetAndState", .1f);
        Check(ai.CurrentState == ZeldaAiState.Hostile && ai.IsActivelyHostileTo(player), "player detection interrupts attracted pause immediately");
        Clear();
        // Existing multiplayer-NPC pursuit/search timer regression must remain untouched.
        try { typeof(SharedHostilitySmoke).GetMethod("Verify", F).Invoke(null, null); }
        finally { typeof(SharedHostilitySmoke).GetMethod("Clear", F).Invoke(null, null); }
        Check(true, "existing shared hostility and search regressions pass");
    }
}

