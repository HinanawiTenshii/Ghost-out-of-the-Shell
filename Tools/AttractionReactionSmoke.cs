using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Only executes in the disposable Unity validation project.
[InitializeOnLoad]
public static class AttractionReactionSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = nameof(AttractionReactionSmoke);
    static readonly List<GameObject> owned = new List<GameObject>();
    static int checks;
    static ZeldaFourWayMover player;
    static AttractionReactionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project");
        PlayerSettings.productName = "attraction-reaction-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        try { Verify(); Debug.Log("ATTRACTION_REACTION_PASS checks=" + checks); Clear(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("Field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(ZeldaCharacterAiBase ai, string name, params object[] args) => typeof(ZeldaCharacterAiBase).GetMethod(name, F).Invoke(ai, args);
    static void Move(Component obj, Vector2 position)
    { obj.transform.position = position; obj.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); }
    static GameObject Actor(string name)
    {
        var obj = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover)); owned.Add(obj); return obj;
    }
    static ZeldaCharacterAiBase Setup(Type aiType = null)
    {
        Clear(); Time.timeScale = 1;
        player = Actor("Player").GetComponent<ZeldaFourWayMover>(); Move(player, new Vector2(30, 0));
        var obj = Actor("Investigator"); obj.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaCharacterAiBase)obj.AddComponent(aiType ?? typeof(ZeldaCharacterAiBase));
        Set(ai, "enableAwarenessSound", false); Set(ai, "solidCollisionLayers", (LayerMask)0); Set(ai, "visionObstacleLayers", (LayerMask)0);
        Set(ai, "facingDirection", Vector2.right); Set(ai, "visionRadius", 7f); Set(ai, "peripheralVisionRadius", 1.7f);
        ZeldaRuntimeRegistry.ClaimControlledMover(player); return ai;
    }
    static SpriteRenderer Indicator(ZeldaCharacterAiBase ai)
    { Call(ai, "UpdateStateIndicator"); return ai.transform.Find("AI State Indicator").GetComponent<SpriteRenderer>(); }
    static void Clear()
    {
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
        foreach (var obj in owned) if (obj != null) Object.DestroyImmediate(obj); owned.Clear();
    }
    static void Verify()
    {
        var ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0));
        Check(ai.IsReactingToAttraction && ai.CaptureSaveState().investigationReactionRemaining == 1f, "unseen idle actor starts one-second reaction");
        Check(ai.RequiresVisionStreamingRetention, "reaction retained by distance streaming");
        var icon = Indicator(ai); Check(icon.enabled && icon.sprite.name == "AI Attraction Reaction" && icon.color == Color.white, "white exclamation immediately visible");
        CheckWhiteAndPreview(icon.sprite);
        var sharedSprite = icon.sprite;
        Call(ai, "TickSearch", .4f); float remaining = ai.CaptureSaveState().investigationReactionRemaining;
        for (int i = 0; i < 12; i++) ai.InvestigatePosition(new Vector2(4, i * .1f));
        Check(Mathf.Abs(ai.CaptureSaveState().investigationReactionRemaining - .6f) < .0001f, "repeated attraction neither stacks nor refreshes reaction");
        Check(ai.CaptureSaveState().handgunIncident == new Vector2(4, 1.1f), "latest incident destination retained");
        Check(Indicator(ai).sprite == sharedSprite, "reaction reuses cached icon");
        var rb = ai.GetComponent<Rigidbody2D>(); Vector2 origin = rb.position;
        rb.velocity = Vector2.right * 10; Set(ai, "moveDirection", Vector2.right); Set(ai, "cornerEscapeTimer", 1f);
        Call(ai, "FixedUpdate");
        Check(rb.position == origin && rb.velocity == Vector2.zero && rb.angularVelocity == 0, "physics and stale escape motion frozen");
        Call(ai, "TickSearch", 0f); Check(ai.CaptureSaveState().investigationReactionRemaining == remaining, "zero delta does not consume reaction");
        Call(ai, "UpdateTargetAndState", .5f);
        Check(ai.CaptureSaveState().searchTime == 0 && ai.CaptureSaveState().handgunTraveling, "reaction does not consume investigation time");
        Call(ai, "TickSearch", .59f);
        Check(ai.IsReactingToAttraction && Get<Vector2>(ai, "moveDirection") == Vector2.zero, "stationary until one second");
        Call(ai, "TickSearch", .02f);
        Check(!ai.IsReactingToAttraction && !Indicator(ai).enabled, "reaction ends and white exclamation disappears");
        Call(ai, "TickSearch", .02f); Check(Get<Vector2>(ai, "moveDirection").sqrMagnitude > .01f, "approach resumes after reaction");
        ai.InvestigatePosition(new Vector2(5, 1)); Check(!ai.IsReactingToAttraction, "already-searching actor gets no second reaction");
        Move(ai, ai.CaptureSaveState().handgunApproach); Call(ai, "TickSearch", .01f);
        Check(ai.CaptureSaveState().handgunPausing && !ai.IsReactingToAttraction, "arrival pause remains separate");
        Call(ai, "TickSearch", 3.49f); Check(ai.CaptureSaveState().handgunPausing, "existing arrival pause still 3.5 seconds");
        Call(ai, "TickSearch", .02f); Check(!ai.CaptureSaveState().handgunPausing, "arrival pause ends normally");
        Set(ai, "investigationElapsed", 15.99f); Call(ai, "UpdateTargetAndState", .02f);
        Check(ai.CurrentState == ZeldaAiState.Recovery, "existing 16-second investigation unchanged");

        ai = Setup(); Move(player, new Vector2(3, 0));
        Check((bool)Call(ai, "CanSeePlayer", player), "visible-player fixture"); ai.InvestigatePosition(new Vector2(-5, 0));
        Check(!ai.IsReactingToAttraction && !Indicator(ai).enabled, "visible player at reception skips reaction even if incident turns actor away");
        ai = Setup(); Move(player, new Vector2(-3, 0));
        Check(!(bool)Call(ai, "CanSeePlayer", player), "behind-player fixture"); ai.InvestigatePosition(new Vector2(5, 0));
        Check(ai.IsReactingToAttraction, "player outside vision angle permits reaction");
        ai = Setup(); Move(player, new Vector2(3, 0));
        var wall = new GameObject("Blocks wall"); owned.Add(wall); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector3(1.5f, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.3f, 4);
        Set(ai, "visionObstacleLayers", (LayerMask)LayerMask.GetMask("Blocks")); Physics2D.SyncTransforms();
        Check(!(bool)Call(ai, "CanSeePlayer", player), "occluded-player fixture"); ai.InvestigatePosition(new Vector2(5, 0));
        Check(ai.IsReactingToAttraction, "occluded player permits reaction");

        foreach (var state in new[] { ZeldaAiState.Suspicious, ZeldaAiState.Alert, ZeldaAiState.Hostile, ZeldaAiState.Recovery })
        {
            ai = Setup(); Call(ai, "ChangeState", state, false); ai.InvestigatePosition(new Vector2(5, 0));
            Check(!ai.IsReactingToAttraction, state + " does not acquire idle reaction");
        }
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0)); Call(ai, "TickSearch", .2f); Move(player, new Vector2(3, 0));
        Call(ai, "UpdateTargetAndState", .01f);
        Check(ai.CurrentState == ZeldaAiState.Hostile && !ai.IsReactingToAttraction && ai.IsActivelyHostileTo(player), "seeing player interrupts reaction immediately");
        Check(Indicator(ai).enabled && Indicator(ai).sprite != sharedSprite, "hostile indicator replaces white reaction");
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0)); Move(player, new Vector2(3, 0)); ai.OnCharacterDamagedBy(player.CharacterData);
        Check(ai.CurrentState == ZeldaAiState.Hostile && !ai.IsReactingToAttraction, "retaliation cancels reaction");
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0)); ai.StunForDuration(2f);
        Check(ai.IsStunned && !ai.IsReactingToAttraction && Indicator(ai).sprite != sharedSprite, "stun supersedes reaction and icon");
        Call(ai, "FinishStun"); Check(!ai.IsReactingToAttraction, "stun recovery does not add another delay");
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0)); Call(ai, "BeginSearch");
        Check(!ai.IsReactingToAttraction, "ordinary hostility search clears reaction");
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0)); ai.enabled = false;
        Check(!ai.IsReactingToAttraction && !Indicator(ai).enabled, "possession/AI disable clears white indicator"); ai.enabled = true;
        Check(ai.CurrentState == ZeldaAiState.Idle && !ai.IsReactingToAttraction, "enable starts without stale reaction");
        ai = Setup(); ai.InvestigatePosition(new Vector2(5, 0));
        var body = ai.GetComponent<ZeldaCharacterData>(); var dead = body.CaptureRuntimeState(); dead.currentHealth = 0; body.ApplyRuntimeState(dead); Call(ai, "Update");
        Check(!ai.IsReactingToAttraction && !Indicator(ai).enabled, "death clears pending reaction");

        ai = Setup(); ai.InvestigateHandgunShot(new Vector2(5, 0)); Call(ai, "TickSearch", .35f);
        Check(ai.IsReactingToAttraction && ai.CaptureSaveState().handgunInvestigation, "handgun attraction uses same subphase");
        var saved = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(ai.CaptureSaveState()));
        ai.gameObject.SetActive(false); ai.gameObject.SetActive(true); ai.ApplySaveState(saved);
        Check(ai.IsReactingToAttraction && Mathf.Abs(ai.CaptureSaveState().investigationReactionRemaining - .65f) < .0001f && Indicator(ai).enabled, "save/stream reload restores remaining delay and icon");
        Call(ai, "TickSearch", .64f); Check(ai.IsReactingToAttraction, "restored reaction waits only remainder");
        Call(ai, "TickSearch", .02f); Check(!ai.IsReactingToAttraction, "restored reaction does not restart full second");
        saved.investigationReactionRemaining = 0; ai.ApplySaveState(saved);
        Check(!ai.IsReactingToAttraction, "legacy saves default to no retroactive reaction");
        saved.investigationReactionRemaining = 10; ai.ApplySaveState(saved);
        Check(ai.CaptureSaveState().investigationReactionRemaining == 1f, "saved delay clamped to one second");
        ai = Setup(typeof(ZeldaPatrolCharacterAi)); ai.InvestigatePosition(new Vector2(5, 0));
        Call(ai, "Update"); Check(ai.IsReactingToAttraction && Get<Vector2>(ai, "moveDirection") == Vector2.zero, "patrol AI also stops including separation steering");
        ai = Setup(); ai.RespondToAlarm(new Vector2(5, 0));
        Check(ai.IsRespondingToAlarm && ai.IsReactingToAttraction, "idle alarm uses shared reaction without losing assignment");
        Call(ai, "TickSearch", 1f); Check(!ai.IsReactingToAttraction && ai.IsRespondingToAlarm, "alarm resumes after one second");
    }
    static void CheckWhiteAndPreview(Sprite sprite)
    {
        var rt = RenderTexture.GetTemporary(70, 100, 0, RenderTextureFormat.ARGB32); var old = RenderTexture.active;
        Graphics.Blit(sprite.texture, rt); RenderTexture.active = rt;
        var png = new Texture2D(70, 100, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, 70, 100), 0, 0); png.Apply();
        int opaque = 0; bool allWhite = true;
        foreach (var c in png.GetPixels()) if (c.a > .5f) { opaque++; allWhite &= c.r > .99f && c.g > .99f && c.b > .99f; }
        Check(allWhite, "exclamation pixels white");
        Check(opaque > 0, "exclamation visible");
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../AttractionReaction-preview.png"), png.EncodeToPNG());
        Object.Destroy(png); RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
    }
}
