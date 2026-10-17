using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SilenceOathSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<GameObject> actors = new List<GameObject>();
    static ZeldaCharacterData player;
    static PlayerGrowthAttributes growth;
    static int checks, frame = -1;
    static IEnumerator suite;
    static double deadline;
    static SilenceOathSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "silence-oath-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SilenceOathSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SilenceOathSmoke), false))
            { SessionState.SetBool(nameof(SilenceOathSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SILENCE_OATH_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool v, string m) { checks++; if (!v) throw new Exception(m); }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; }
        throw new Exception("Missing field " + n);
    }
    static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static object Call(object o, string n, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(n, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + n);
    }
    static ZeldaFourWayMover Mover => player.GetComponent<ZeldaFourWayMover>();
    static GameObject Actor(string name, Vector2 p)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        actors.Add(go); go.transform.position = p; go.GetComponent<Rigidbody2D>().position = p;
        var data = go.GetComponent<ZeldaCharacterData>(); var s = data.CaptureRuntimeState();
        s.health = s.currentHealth = 30; s.possessionEnergy = 100; data.ApplyRuntimeState(s); return go;
    }
    static ZeldaCharacterAiBase Npc(float x, float y = 0)
    {
        var go = Actor("Oath Guard " + actors.Count, new Vector2(x, y));
        go.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = go.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, "enableAwarenessSound", false); Set(ai, "visionObstacleLayers", (LayerMask)0);
        Set(ai, "solidCollisionLayers", (LayerMask)0); Set(ai, "visionRadius", 3f);
        Set(ai, "facingDirection", Vector2.left); return ai;
    }
    static void Setup(int rank = 1)
    {
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
        for (int i = actors.Count - 1; i >= 0; i--) if (actors[i] != null) Object.DestroyImmediate(actors[i]); actors.Clear();
        Time.timeScale = 1;
        player = Actor("Oath Player", Vector2.zero).GetComponent<ZeldaCharacterData>();
        Mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(Mover);
        growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default); growth.AddCollectibles(30);
        if (rank >= 1)
        {
            var state = growth.CaptureRuntimeState(); state.discoveredCharacterSkills = new[] { SilenceOathRuntime.SkillId };
            growth.ApplyRuntimeState(state);
        }
        if (rank >= 1) growth.TryUpgradeSkill("silence_oath");
        if (rank >= 2) growth.TryUpgradeSkill("silence_oath_2");
        if (rank >= 3) growth.TryUpgradeSkill("silence_oath_3");
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static void MovePlayer(Vector2 p)
    { Mover.transform.position = p; Mover.GetComponent<Rigidbody2D>().position = p; Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); }
    static void Cast(ZeldaCharacterAiBase ai)
    {
        Mover.EndItemAttack(); Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        int energy = player.FinalPossessionEnergy;
        Check(SilenceOathRuntime.TryUse(player, ai), "cast succeeds");
        Check(player.FinalPossessionEnergy == energy - 2, "cast costs exactly two energy");
    }
    static void Hostile(ZeldaCharacterAiBase ai) => Call(ai, "BeginTemporaryHostility", Mover, false);
    static void Advance(ZeldaCharacterAiBase ai, float seconds) => Call(ai, "AdvanceSilenceOath", seconds);
    static void State(ZeldaCharacterAiBase ai, ZeldaAiState state) => Call(ai, "ChangeState", state, true);
    static SharedPlayerHostilityTimer Timer => SharedPlayerHostilityTimer.ForScene(SceneManager.GetActiveScene());

    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Silence Oath Tests"); SceneManager.SetActiveScene(scene);
        Setup(0);
        var basic = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "silence_oath");
        Check(!growth.IsSkillDiscovered(basic) && !growth.TryUpgradeSkill("silence_oath"), "relic discovery required for new saves");
        var discovery = growth.CaptureRuntimeState(); discovery.discoveredCharacterSkills = new[] { SilenceOathRuntime.SkillId };
        growth.ApplyRuntimeState(discovery);
        Check(!growth.TryUpgradeSkill("silence_oath_2"), "upgrade requires base");
        Check(growth.TryUpgradeSkill("silence_oath") && growth.CollectibleCount == 26, "unlock costs four");
        Check(growth.ToggleEquippedSkill(0, "silence_oath"), "equip base");
        Check(growth.TryUpgradeSkill("silence_oath_2") && growth.CollectibleCount == 24 && growth.GetEquippedSkill(0) == "silence_oath_2", "first upgrade costs two and replaces equipped skill");
        Check(growth.TryUpgradeSkill("silence_oath_3") && growth.CollectibleCount == 22 && growth.GetEquippedSkill(0) == "silence_oath_3", "second upgrade costs two and replaces equipped skill");
        var savedGrowth = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(savedGrowth);
        Check(SilenceOathRuntime.Rank == 3 && growth.GetEquippedSkill(0) == "silence_oath_3", "growth JSON persists ranks and equipment");

        Setup(); var a = Npc(2); var b = Npc(2, 1); var behind = Npc(-1); var distant = Npc(6);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        Check(SilenceOathRuntime.FindTarget(Mover) == a, "preview prefers forward target");
        Check(!SilenceOathRuntime.IsValidTarget(Mover, behind) && !SilenceOathRuntime.IsValidTarget(Mover, distant), "range and facing validated");
        var wall = new GameObject("Blocks test wall"); actors.Add(wall); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = Vector3.right; wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3f); Physics2D.SyncTransforms();
        Check(!SilenceOathRuntime.IsValidTarget(Mover, a), "cannot mark through Blocks");
        Object.DestroyImmediate(wall); Physics2D.SyncTransforms();
        State(a, ZeldaAiState.Alert); Mover.EndItemAttack(); int energy = player.FinalPossessionEnergy;
        Check(!SilenceOathRuntime.TryUse(player, a) && player.FinalPossessionEnergy == energy, "new warning target rejected without energy");
        State(a, ZeldaAiState.Hostile); Check(!a.CanReceiveSilenceOath, "new hostile target rejected");
        State(a, ZeldaAiState.Search); Check(!a.CanReceiveSilenceOath, "new search target rejected");
        Call(a, "BeginRecovery"); State(a, ZeldaAiState.Idle); Cast(a);
        Check(a.CurrentState == ZeldaAiState.Idle && a.HasSilenceOath && a.RequiresVisionStreamingRetention, "cast does not alert and retains offscreen timer");
        Check(a.transform.Find("Silence Oath Mark") != null, "world marker created");
        Advance(a, 12f); Cast(a); Check(Mathf.Approximately(a.SilenceOathRemaining, 30f), "recast refreshes existing target");
        Mover.EndItemAttack(); energy = player.FinalPossessionEnergy;
        Check(!SilenceOathRuntime.TryUse(player, b) && player.FinalPossessionEnergy == energy, "base cannot add second target");
        Check(CharacterSkillBuffs.TryGet(player, BuffDurationIcon.SilenceOath, out var buff) && buff.IsTimed && buff.Remaining == 30f, "common HUD adapter exposes live timer");
        Hostile(a);
        Check(a.CurrentState == ZeldaAiState.Hostile && a.CurrentTarget == Mover && b.CurrentState == ZeldaAiState.Idle, "oath target fights but does not chain nearby hostility");
        Check(Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle, "oath does not start global timer");
        Check(!(bool)Call(b, "IsNonGhostPlayerHostileToAnotherAi", Mover), "watching an oath target's hostility does not indirectly spread it");
        Advance(a, 31f); State(a, ZeldaAiState.Hostile); Call(a, "SyncSharedPlayerIncident");
        Check(!a.HasSilenceOath && a.IsSilenceOathIncidentPrivate && Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle && b.CurrentState == ZeldaAiState.Idle,
            "expiration does not replay old alert");
        MovePlayer(new Vector2(40, 0)); Set(a, "npcThreatLastSeenAt", Time.time - 10f); Call(a, "UpdateTargetAndState", .1f);
        Check(a.CurrentState == ZeldaAiState.Search && b.CurrentState == ZeldaAiState.Idle, "independent pursuit times out into local search");
        Call(a, "UpdateTargetAndState", 12.1f);
        Check(a.CurrentState == ZeldaAiState.Recovery && !a.IsSilenceOathIncidentPrivate, "independent search ends normally");

        Setup(); a = Npc(2); b = Npc(2, 1); Cast(a); Hostile(a); Advance(a, 31f);
        a.OnCharacterDamagedBy(player);
        Check(b.IsActivelyHostileTo(Mover) && Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "new attack after expiration can alert normally");
        Setup(); a = Npc(2); b = Npc(2, 1); Cast(a); Hostile(a); Advance(a, 31f);
        a.OnPlayerPlacedBomb(player);
        Check(Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "fresh bomb placement can alert after expiration");
        Setup(); a = Npc(2); b = Npc(2, 1); Cast(a); Hostile(a); Advance(a, 31f);
        a.OnPlayerAttackedCharacter(player, b.GetComponent<ZeldaCharacterData>());
        Check(Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "fresh witnessed violence can alert after expiration");
        Setup(); a = Npc(2); b = Npc(2, 1); Cast(a);
        b.OnPlayerAttackedCharacter(player, a.GetComponent<ZeldaCharacterData>());
        Check(b.IsActivelyHostileTo(Mover) && Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile, "unmarked witnesses still respond to actual violence against marked target");
        Setup(); a = Npc(2); b = Npc(20); Cast(a); Hostile(b);
        Check(!(bool)Call(a, "CanObserveSharedPlayer", Mover), "marked observer cannot refresh someone else's global timer");
        Timer.Advance(8f); Check(Mathf.Approximately(Timer.Remaining, 1f), "global countdown continues despite marked guard seeing player");

        Setup(2); a = Npc(2); Cast(a); Advance(a, 29f); Hostile(a); Advance(a, 2f);
        Check(a.HasSilenceOath && Mathf.Approximately(a.SilenceOathRemaining, 59f), "rank two extends through alert cycle with ninety-second cap");
        Call(a, "BeginSearch"); Advance(a, 10f); Check(a.HasSilenceOath, "extension covers search");
        Call(a, "BeginRecovery"); Check(!a.HasSilenceOath && !a.IsSilenceOathIncidentPrivate, "return ends extension");
        Setup(2); a = Npc(2); Cast(a); Advance(a, 30.1f); Check(!a.HasSilenceOath, "calm rank two still lasts thirty seconds");
        Setup(2); a = Npc(2); Cast(a); Hostile(a); Advance(a, 90.1f);
        Check(!a.HasSilenceOath && a.CurrentState == ZeldaAiState.Hostile && Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle,
            "hard cap expires without amnesia or retroactive global alarm");
        Setup(2); a = Npc(2); Cast(a); Hostile(a); a.StunForDuration(5f); Advance(a, 31f);
        Check(a.HasSilenceOath && a.IsStunned, "stun preserves ongoing alert cycle"); Call(a, "FinishStun");
        Check(a.IsSilenceOathIncidentPrivate, "stun restoration does not broadcast");

        Setup(3); a = Npc(2); b = Npc(2, 1); var c = Npc(2, -1); Cast(a); Cast(b);
        Check(a.HasSilenceOath && b.HasSilenceOath, "rank three allows two targets");
        Mover.EndItemAttack(); energy = player.FinalPossessionEnergy;
        Check(!SilenceOathRuntime.TryUse(player, c) && player.FinalPossessionEnergy == energy, "third target refused without spending");
        Advance(a, 5f); Advance(b, 10f); Cast(a);
        Check(a.SilenceOathRemaining == 30f && b.SilenceOathRemaining == 20f, "two timers independent, recast uses no extra slot");
        Check(CharacterSkillBuffs.TryGet(player, BuffDurationIcon.SilenceOath, out buff) && buff.Remaining == 20f, "buff HUD follows next expiry");
        Hostile(a); Advance(a, 40f);
        var state = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(a.CaptureSaveState()));
        Call(a, "ClearSilenceOath"); Call(a, "BeginRecovery"); a.ApplySaveState(state);
        Check(a.HasSilenceOath && a.SilenceOathRank == 3 && a.SilenceOathRemaining == 50f && a.IsActivelyHostileTo(Mover), "save roundtrip restores remaining duration and hostility");
        Check(Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle, "restoring marked hostility does not create global incident");
        Advance(a, 51f); state = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(a.CaptureSaveState()));
        Call(a, "ClearSilenceOath"); a.ApplySaveState(state); State(a, ZeldaAiState.Hostile);
        Check(!a.HasSilenceOath && a.IsSilenceOathIncidentPrivate && Timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle,
            "expired private incident also persists without deferred alert");
        Call(a, "BeginRecovery"); a.ApplySaveState(new ZeldaCharacterAiBase.SaveState());
        Check(!a.HasSilenceOath && !a.IsSilenceOathIncidentPrivate, "old saves default unmarked");
        Call(b, "ClearSilenceOath"); Cast(a); Time.timeScale = 0; Mover.EndItemAttack();
        Check(!SilenceOathRuntime.TryUse(player, b), "paused cast rejected"); Time.timeScale = 1;
        var low = player.CaptureRuntimeState(); low.possessionEnergy = 1;
        low.spentBasePossessionEnergy = low.spentGrowthPossessionEnergy = low.crystalEnergyThirds = 0; player.ApplyRuntimeState(low);
        energy = player.FinalPossessionEnergy; Check(!SilenceOathRuntime.TryUse(player, b) && player.FinalPossessionEnergy == energy, "insufficient energy cannot apply");
        Check(!SilenceOathRuntime.TryUse(null), "null caster safe");
        GhostFormRuntime.Restore(player, 5f, 5f);
        Check(!SilenceOathRuntime.TryUse(player, b), "ghost-form caster rejected"); GhostFormRuntime.Restore(player, 0f);
        b.StunForDuration(5f); Check(!b.CanReceiveSilenceOath, "stunned new target rejected");
        Call(b, "FinishStun"); b.GetComponent<ZeldaCharacterData>().KillFromPotion();
        Check(!b.CanReceiveSilenceOath, "dead target rejected");
        Mover.enabled = false; a.GetComponent<ZeldaFourWayMover>().ReceiveControl(Vector2.down);
        Check(!a.HasSilenceOath, "possessing a marked body releases its oath");

        Setup(3); a = Npc(2); Cast(a); Advance(a, 8f);
        var travel = SceneTravelStateManager.GetOrCreate(); string world = travel.CaptureDiskWorld();
        Check(world.Contains("silenceOath") && world.Contains("\"elapsed\":8"), "actual scene disk snapshot includes oath timer");
        var sameSceneState = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(a.CaptureSaveState()));
        var extraScene = SceneManager.CreateScene("Oath Unloaded Scene");
        SceneManager.MoveGameObjectToScene(a.gameObject, extraScene);
        var unload = SceneManager.UnloadSceneAsync(extraScene); while (!unload.isDone) yield return null;
        a = Npc(2); a.ApplySaveState(sameSceneState);
        Check(a.HasSilenceOath && Mathf.Approximately(a.SilenceOathRemaining, 22f), "unloaded NPC recreated with remaining mark");
        Call(a, "ClearSilenceOath"); a.RestoreSilenceOathAfterSceneReturn(sameSceneState);
        Check(a.HasSilenceOath && Mathf.Approximately(a.SilenceOathRemaining, 22f) && !a.CaptureSaveState().silenceOath.privateIncident,
            "normal scene return restores base duration without an old pursuit");
        var extended = sameSceneState; extended.silenceOath.elapsed = 40; extended.silenceOath.alertCycle = true; extended.silenceOath.privateIncident = true;
        a.RestoreSilenceOathAfterSceneReturn(extended);
        Check(!a.HasSilenceOath && !a.IsSilenceOathIncidentPrivate, "normal return ends the extended alert cycle with existing scene policy");
        extended.silenceOath.elapsed = 8; extended.silenceOath.alertCycle = false; extended.silenceOath.privateIncident = false;
        a.RestoreSilenceOathAfterSceneReturn(extended);
        Time.timeScale = 0;
        Visuals();
        for (int i = actors.Count - 1; i >= 0; i--) if (actors[i] != null) Object.DestroyImmediate(actors[i]); actors.Clear();
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
        Regression(typeof(NearbyHostilitySmoke));
        Regression(typeof(SharedHostilitySmoke));
        yield return null;
    }

    static void Regression(Type type)
    {
        try { type.GetMethod("Verify", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); Check(true, type.Name + " regression"); }
        finally { type.GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
        Debug.Log("SILENCE_OATH_REGRESSION_PASS " + type.Name + " checks=" + type.GetField("checks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
    }

    static void Visuals()
    {
        var root = new GameObject("Oath Skill Preview", typeof(RectTransform), typeof(Canvas));
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; root.transform.position = new Vector3(1000, 1000);
        var page = root.AddComponent<CharacterSkillPage>();
        var font = AssetDatabase.FindAssets("ZLabsPixel_12px_M_CN t:Font").Select(g => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        page.Build(font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Set(page, "selectedId", "silence_oath"); Call(page, "Refresh"); var tree = Get<SkillTreeScroll>(page, "tree"); tree.Zoom(-.5f);
        Check(tree.content.rect.height >= 520, "expanded content includes bottom row even at minimum zoom");
        var art = root.GetComponentsInChildren<SkillPageArt>().Where(i => i.silenceOath).ToArray();
        Check(art.Length == 3 && art.All(i => i.mainTexture != Texture2D.whiteTexture), "all three tree nodes use oath glyph");
        foreach (string id in new[] { "silence_oath", "silence_oath_2", "silence_oath_3" })
        {
            var skill = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == id);
            Check(skill.position.y == -660 && SkillPageArt.GetAbilitySprite(id) != null, "oath node below holy shield with usable equipped icon " + id);
            Set(page, "selectedId", id); Call(page, "Refresh"); Canvas.ForceUpdateCanvases();
            var description = Get<Text>(page, "description");
            Check(description.preferredHeight <= description.rectTransform.rect.height + 1f, "description fits without clipping " + id);
        }
        Set(page, "selectedId", "silence_oath"); Call(page, "Refresh");
        Canvas.ForceUpdateCanvases(); tree.verticalNormalizedPosition = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var camera = new GameObject("Oath Preview Camera").AddComponent<Camera>();
        camera.transform.position = root.transform.position + Vector3.back * 10;
        camera.orthographic = true; camera.orthographicSize = 285;
        camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        Canvas.ForceUpdateCanvases(); Capture(camera, "Silence-oath-tree.png", 1600, 850);
        // A dedicated buff strip verifies timer graphics and sprite integration.
        var anchor = new GameObject("Property Anchor", typeof(RectTransform)).GetComponent<RectTransform>(); anchor.SetParent(root.transform, false);
        anchor.sizeDelta = new Vector2(200, 40);
        var hud = new GameObject("Buff Strip", typeof(RectTransform)).AddComponent<RuntimeBuffDurationBar>(); hud.transform.SetParent(root.transform, false);
        hud.Build(anchor); hud.RefreshNow();
        Check(hud.GetIcon(BuffDurationIcon.SilenceOath).gameObject.activeSelf && hud.GetIcon(BuffDurationIcon.SilenceOath).IsTimed,
            "pooled duration bar displays oath timer");
        Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
    }
    static void Capture(Camera camera, string name, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(Application.dataPath + "/../" + name, texture.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }
}
