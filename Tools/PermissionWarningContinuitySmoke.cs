using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PermissionWarningContinuitySmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = nameof(PermissionWarningContinuitySmoke);
    static readonly List<GameObject> owned = new List<GameObject>();
    static ZeldaFourWayMover player;
    static int checks;
    static PermissionWarningContinuitySmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception(label);
    }
    static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) return field;
        }
        throw new Exception("Missing field: " + name);
    }
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    static object Call(ZeldaCharacterAiBase ai, string name, params object[] args) =>
        typeof(ZeldaCharacterAiBase).GetMethod(name, Flags).Invoke(ai, args);
    static void Perceive(ZeldaCharacterAiBase ai, float dt) => Call(ai, "UpdateTargetAndState", dt);
    static void Move(Vector2 position)
    {
        player.transform.position = position;
        player.GetComponent<Rigidbody2D>().position = position;
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static GameObject Actor(string name)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        owned.Add(go); return go;
    }
    static void Clear()
    {
        foreach (var clock in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(clock.gameObject);
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
    }
    static ZeldaCharacterAiBase Setup(int permission = 1, Type type = null)
    {
        Clear();
        player = Actor("Player").GetComponent<ZeldaFourWayMover>();
        Set(player.CharacterData, "permissionLevel", permission);
        Move(new Vector2(2, 0));
        var guard = Actor("Guard"); guard.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaCharacterAiBase)guard.AddComponent(type ?? typeof(ZeldaCharacterAiBase));
        Set(ai, "enableAwarenessSound", false);
        Set(ai, "solidCollisionLayers", (LayerMask)0); Set(ai, "visionObstacleLayers", (LayerMask)0);
        Set(ai, "facingDirection", Vector2.right); Set(ai, "visionRadius", 10f);
        Set(ai, "immediateResponseDistance", 5.5f); Set(ai, "warningEscalationTime", 4f);
        ZeldaRuntimeRegistry.ClaimControlledMover(player);
        return ai;
    }
    static PermissionArea Area(int permission, float centerX = 2, float width = 2)
    {
        var go = new GameObject("Permission " + permission); owned.Add(go);
        go.transform.position = new Vector2(centerX, 0);
        var area = go.AddComponent<PermissionArea>(); area.SetPermissionLevel(permission);
        Set(area, "areaSize", new Vector2(width, 6));
        typeof(PermissionArea).GetMethod("SynchronizeCollider", Flags).Invoke(area, null);
        Physics2D.SyncTransforms(); return area;
    }
    static void Alert(ZeldaCharacterAiBase ai, float elapsed = 1f)
    {
        Perceive(ai, .01f);
        Check(ai.CurrentState == ZeldaAiState.Alert && ai.CurrentTarget == player, "Permission violation starts a warning");
        Perceive(ai, elapsed);
        Check(Mathf.Approximately(ai.CaptureSaveState().warning, elapsed), "Initial warning time accumulates");
    }
    static void StillAlert(ZeldaCharacterAiBase ai, float elapsed, string label)
    {
        Check(ai.CurrentState == ZeldaAiState.Alert && ai.CurrentTarget == player, label + ": same warning target");
        Check(Mathf.Abs(ai.CaptureSaveState().warning - elapsed) < .001f, label + ": timer preserved");
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "Isolated project only");
        PlayerSettings.productName = "permission-warning-continuity-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false) || Time.frameCount < 3 ||
            RetroSceneLoadReveal.IsPreparingGameplay) return;
        SessionState.SetBool(Key, false);
        try { Verify(); Clear(); Debug.Log("PERMISSION_WARNING_CONTINUITY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Verify()
    {
        // Reproduce the boundary bug for ordinary, patrol, and squad guards.
        foreach (var type in new[] { typeof(ZeldaCharacterAiBase), typeof(ZeldaPatrolCharacterAi), typeof(SquadPatrolCharacterAi) })
        {
            var ai = Setup(1, type); var left = Area(2); var right = Area(2, 4);
            Alert(ai); Move(new Vector2(4, 0)); Perceive(ai, .5f);
            StillAlert(ai, 1.5f, type.Name + " crosses adjacent restricted region");
            Check(Get<PermissionArea>(ai, "activePermissionArea") == right, "Current region replaces stale original region");
            Move(new Vector2(2, 0)); Perceive(ai, .5f); StillAlert(ai, 2f, "Returning across border cannot reset time");
            Move(new Vector2(4, 0)); Perceive(ai, 2.1f);
            Check(ai.CurrentState == ZeldaAiState.Hostile && ai.IsActivelyHostileTo(player), "Warning escalates on cumulative time across regions");
        }
        var guard = Setup(); Area(2); Area(2, 3, 3); Alert(guard);
        Move(new Vector2(3.5f, 0)); Perceive(guard, .25f);
        StillAlert(guard, 1.25f, "Leaving one overlapping area retains other violation");

        guard = Setup(); Area(2, 2, 8); Area(3, 4, 1); Alert(guard);
        Move(new Vector2(4, 0)); Perceive(guard, .01f);
        Check(guard.CurrentState == ZeldaAiState.Hostile, "Higher overlapping restriction immediately escalates as normal");

        guard = Setup(); Area(2); Area(1, 4); Alert(guard);
        Move(new Vector2(4, 0)); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Recovery && guard.CurrentTarget == null, "Entering permitted area still clears permission warning");
        guard = Setup(); Area(2); Alert(guard); Move(new Vector2(5, 0)); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Recovery, "Leaving restrictions for legal public space clears warning");

        guard = Setup(); var changing = Area(2); Alert(guard); changing.SetPermissionLevel(1); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Recovery, "Current region permission reduction clears violation");
        guard = Setup(); Area(2); Alert(guard); Set(player.CharacterData, "permissionLevel", 2); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Recovery, "Player's new sufficient permission clears warning");
        guard = Setup(); changing = Area(2); Alert(guard); changing.SetPermissionLevel(3); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Hostile, "Restriction increase evaluated without changing area object");

        guard = Setup(-1); Area(0); Alert(guard); Move(new Vector2(5, 0)); Perceive(guard, .25f);
        StillAlert(guard, 1.25f, "Default public permission remains a violation for negative-permission body");
        Check(Get<PermissionArea>(guard, "activePermissionArea") == null, "Public space does not require an area component");
        Call(guard, "TickAlert", .02f);
        Check(Get<Vector2>(guard, "moveDirection").sqrMagnitude > 0, "Warning follow works without an area component");
        Perceive(guard, 2.8f); Check(guard.CurrentState == ZeldaAiState.Hostile, "Default-area warning eventually escalates");

        guard = Setup(); var original = Area(2); var replacement = Area(2); Alert(guard);
        original.enabled = false; Perceive(guard, .25f);
        StillAlert(guard, 1.25f, "Disabled original area cannot erase overlapping violation");
        Object.DestroyImmediate(replacement.gameObject); Perceive(guard, .25f);
        Check(guard.CurrentState == ZeldaAiState.Recovery, "No remaining active restriction clears warning");

        guard = Setup(); Area(2); Area(2, 4); Alert(guard);
        var saved = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(guard.CaptureSaveState()));
        guard.gameObject.SetActive(false); guard.gameObject.SetActive(true); guard.ApplySaveState(saved);
        Move(new Vector2(4, 0)); Perceive(guard, .5f);
        StillAlert(guard, 1.5f, "Existing save/stream reload rebuilds current area without timer reset");

        guard = Setup(); Area(2); Alert(guard);
        var health = player.CharacterData.CaptureRuntimeState(); health.currentHealth = 0; player.CharacterData.ApplyRuntimeState(health);
        Perceive(guard, .2f); Check(guard.CurrentState == ZeldaAiState.Recovery, "Dead target ends warning safely");
        guard = Setup(); Area(2); Alert(guard); Object.DestroyImmediate(player.gameObject); Perceive(guard, .2f);
        Check(guard.CurrentState == ZeldaAiState.Recovery, "Missing target ends warning safely");

        guard = Setup(1, typeof(CivilianIdleCharacterAi)); Area(2); Perceive(guard, 1f);
        Check(guard.CurrentState == ZeldaAiState.Idle, "Civilian still ignores one-level violations");
        guard = Setup(); Area(2); Alert(guard); guard.OnCharacterDamagedBy(player.CharacterData);
        Move(new Vector2(5, 0)); Perceive(guard, .1f);
        Check(guard.CurrentState == ZeldaAiState.Hostile, "Legal area does not clear actual assault hostility");
    }
}
