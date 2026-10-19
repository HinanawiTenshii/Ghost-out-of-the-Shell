using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Uses the existing attraction fixture; run in the disposable validation project.
[InitializeOnLoad]
public static class InvestigationPermissionSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = nameof(InvestigationPermissionSmoke);
    static readonly List<GameObject> areas = new List<GameObject>();
    static ZeldaFourWayMover player;
    static int checks;
    static InvestigationPermissionSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Fixture(string method, params object[] args) => typeof(AttractionReactionSmoke).GetMethod(method, F).Invoke(null, args);
    static void Set(object obj, string name, object value) => Fixture("Set", obj, name, value);
    static object Call(ZeldaCharacterAiBase ai, string method, params object[] args) => typeof(ZeldaCharacterAiBase).GetMethod(method, F).Invoke(ai, args);
    static void Clear()
    {
        foreach (var area in areas) if (area != null) Object.DestroyImmediate(area);
        areas.Clear(); Fixture("Clear");
    }
    static ZeldaCharacterAiBase Setup(int permission, Type type = null)
    {
        Clear();
        var ai = (ZeldaCharacterAiBase)Fixture("Setup", new object[] { type });
        player = (ZeldaFourWayMover)typeof(AttractionReactionSmoke).GetField("player", F).GetValue(null);
        Set(player.CharacterData, "permissionLevel", permission);
        Fixture("Move", player, new Vector2(3, 0));
        return ai;
    }
    static PermissionArea Area(int permission, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Permission " + permission); areas.Add(go);
        go.transform.position = center;
        var area = go.AddComponent<PermissionArea>(); area.SetPermissionLevel(permission);
        Set(area, "areaSize", size);
        typeof(PermissionArea).GetMethod("SynchronizeCollider", F).Invoke(area, null);
        Physics2D.SyncTransforms(); return area;
    }
    static void Perceive(ZeldaCharacterAiBase ai) => Call(ai, "UpdateTargetAndState", .02f);
    static void Attract(ZeldaCharacterAiBase ai) { ai.InvestigatePosition(new Vector2(5, 0)); Perceive(ai); }
    static void Exempt(ZeldaCharacterAiBase ai, string label)
    {
        Check(ai.CurrentState == ZeldaAiState.Search && !ai.IsActivelyHostileTo(player), label);
        Check(Object.FindObjectsOfType<SharedPlayerHostilityTimer>().Length == 0, label + " does not create shared incident");
    }
    static void Hostile(ZeldaCharacterAiBase ai, string label) => Check(ai.CurrentState == ZeldaAiState.Hostile && ai.IsActivelyHostileTo(player), label);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "investigation-permission-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        // The loading curtain intentionally freezes AI and shared timers. Older
        // fixtures predate that guard, so run them after startup has completed.
        if (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) return;
        SessionState.SetBool(Key, false);
        try
        {
            Verify(); Clear();
            Fixture("Verify"); Clear();
            try { typeof(InvestigationSearchSmoke).GetMethod("Verify", F).Invoke(null, null); }
            finally { typeof(InvestigationSearchSmoke).GetMethod("Clear", F).Invoke(null, null); }
            Debug.Log("INVESTIGATION_PERMISSION_PASS checks=" + checks + "; attraction, search and shared-hostility regressions passed");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Verify()
    {
        for (int permission = -2; permission <= 9; permission++)
        {
            var a = Setup(permission); Attract(a);
            if (permission >= 2) Exempt(a, "public area permission=" + permission);
            else Hostile(a, "insufficient surplus permission=" + permission);
        }
        foreach (int permission in new[] { 4, 5, 6 })
        {
            var a = Setup(permission); Area(3, new Vector2(3, 0), Vector2.one * 2); Attract(a);
            if (permission >= 5) Exempt(a, "level 3 surplus=" + (permission - 3));
            else Hostile(a, "one-level surplus remains suspicious");
        }
        var ai = Setup(5);
        Area(1, new Vector2(3, 0), Vector2.one * 3);
        Area(4, new Vector2(3, 0), Vector2.one * 2); Attract(ai);
        Hostile(ai, "overlap uses highest requirement");
        ai = Setup(5); Area(3, new Vector2(3, 0), Vector2.one * 2);
        Area(8, Vector2.zero, Vector2.one * 2); Attract(ai);
        Exempt(ai, "uses observed character location, not investigator location");

        ai = Setup(5); var changing = Area(3, new Vector2(3, 0), Vector2.one * 2); Attract(ai);
        Exempt(ai, "before area restriction increases"); changing.SetPermissionLevel(4); Perceive(ai);
        Hostile(ai, "permission change evaluated while investigating");
        ai = Setup(5); Area(4, new Vector2(5, 0), Vector2.one * 2); Attract(ai);
        Exempt(ai, "incident location does not replace character location");
        Fixture("Move", player, new Vector2(4.5f, 0)); Perceive(ai);
        Hostile(ai, "moving into higher-security area revokes exemption");
        ai = Setup(5); Attract(ai); Set(player.CharacterData, "permissionLevel", 1); Perceive(ai);
        Hostile(ai, "character permission reduction evaluated");

        foreach (Type type in new[] { typeof(ZeldaCharacterAiBase), typeof(ZeldaPatrolCharacterAi), typeof(SquadPatrolCharacterAi) })
        {
            ai = Setup(2, type); Attract(ai); Exempt(ai, "shared behavior for " + type.Name);
        }
        ai = Setup(9); ai.InvestigateHandgunShot(new Vector2(5, 0)); Perceive(ai);
        Hostile(ai, "handgun attack search never grants permission exemption");
        ai = Setup(9); Fixture("Move", player, new Vector2(30, 0));
        ai.GetComponent<ZeldaCharacterData>().TakeHandgunDamage(1, new Vector2(5, 0), player.CharacterData);
        Check(ai.CurrentState == ZeldaAiState.Search && ai.CaptureSaveState().handgunInvestigation, "actual remote hit marks attack investigation");
        ai.InvestigatePosition(new Vector2(5, 0));
        Fixture("Move", player, new Vector2(3, 0)); Perceive(ai);
        Hostile(ai, "later environmental pulse cannot erase handgun-hit evidence");
        ai = Setup(9); Fixture("Move", player, new Vector2(30, 0));
        ai.InvestigateHandgunShot(new Vector2(5, 0));
        var gunSave = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(ai.CaptureSaveState()));
        ai.gameObject.SetActive(false); ai.gameObject.SetActive(true); ai.ApplySaveState(gunSave);
        Fixture("Move", player, new Vector2(3, 0)); Perceive(ai);
        Hostile(ai, "restored handgun search still attacks high-permission player");
        ai = Setup(2); Attract(ai);
        var save = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(ai.CaptureSaveState()));
        ai.gameObject.SetActive(false); ai.gameObject.SetActive(true); ai.ApplySaveState(save); Perceive(ai);
        Exempt(ai, "saved investigation still respects permission");
        Set(ai, "isTravelingToInvestigation", false); Set(ai, "investigationElapsed", 15.99f); Perceive(ai);
        Check(ai.CurrentState == ZeldaAiState.Recovery, "exempt observer does not stall 16-second investigation expiry");

        ai = Setup(9); Call(ai, "BeginSearch"); Perceive(ai); Hostile(ai, "ordinary pursuit search is not exempt");
        ai = Setup(9); ai.RespondToAlarm(new Vector2(5, 0)); Perceive(ai); Hostile(ai, "explicit alarm dispatch is not exempt");
        ai = Setup(9); Attract(ai); ai.OnCharacterDamagedBy(player.CharacterData); Hostile(ai, "direct damage still retaliates");
        Perceive(ai); Hostile(ai, "high permission does not cancel established hostility");
        ai = Setup(9); Attract(ai); Check(ai.OnPlayerDestroyedDoor(player.CharacterData), "witnessed destruction still reports evidence");
        Hostile(ai, "personally witnessing door/glass destruction remains hostile");
        ai = Setup(9); Attract(ai); ai.OnPlayerPlacedBomb(player.CharacterData); Hostile(ai, "witnessed bomb placement remains hostile");
        ai = Setup(9); Attract(ai);
        var victim = (GameObject)Fixture("Actor", "Victim"); victim.GetComponent<ZeldaFourWayMover>().enabled = false;
        ai.OnPlayerAttackedCharacter(player.CharacterData, victim.GetComponent<ZeldaCharacterData>());
        Hostile(ai, "witnessed attack is not exempt");
    }
}
