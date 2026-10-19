using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PossessionWallSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ZeldaFourWayMover player, target;
    static ZeldaCharacterAiBase ai;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static PossessionWallSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "possession-wall-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(PossessionWallSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(PossessionWallSmoke), false))
            { SessionState.SetBool(nameof(PossessionWallSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (suite.MoveNext()) return;
            suite = null; Clear(); Debug.Log("POSSESSION_WALL_PASS checks=" + checks + "; attack interruption regression passed"); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static object Call(string name, params object[] args) => typeof(ZeldaFourWayMover).GetMethod(name, F).Invoke(player, args);
    static bool Advance(float time, bool held = true) => (bool)Call("AdvanceControlSwitchProgress", time, held);
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Clear()
    {
        foreach (var go in owned) if (go) Object.DestroyImmediate(go); owned.Clear();
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
    }
    static ZeldaFourWayMover Actor(string name, Vector2 position, bool ghost = false)
    {
        var go = Own(new GameObject(name)); go.transform.position = position;
        var data = (ZeldaCharacterData)go.AddComponent(ghost ? typeof(GhostZeldaCharacterData) : typeof(CivilianZeldaCharacterData));
        Set(data, "possessionCost", 0);
        var mover = go.AddComponent<ZeldaFourWayMover>();
        var body = go.GetComponent<BoxCollider2D>(); body.size = new Vector2(.3f, .5f); body.offset = new Vector2(0, .25f);
        return mover;
    }
    static void Setup(bool ghost = false, Vector2 direction = default)
    {
        Clear(); Time.timeScale = 1;
        if (direction == Vector2.zero) direction = Vector2.right;
        player = Actor("Possessor", Vector2.zero, ghost); target = Actor("Target", direction * 1.1f);
        target.enabled = false; ai = target.gameObject.AddComponent<ZeldaCharacterAiBase>(); ai.enabled = false;
        player.ReceiveControl(direction); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        Set(player, "possessionInteractionSelected", true);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static BoxCollider2D Wall(string layer, Vector2 position, Vector2 size, bool trigger = false)
    {
        var go = Own(new GameObject(layer + " wall")); go.layer = LayerMask.NameToLayer(layer); go.transform.position = position;
        var c = go.AddComponent<BoxCollider2D>(); c.size = size; c.isTrigger = trigger;
        Physics2D.SyncTransforms(); return c;
    }
    static void Blocked(string label)
    {
        Check(Call("FindControlSwitchCandidate") == null, label + " cannot select behind wall");
        Check(!(bool)Call("IsValidControlSwitchTarget", target), label + " invalid at final guard");
        Check(player.GetSoulMarkCandidate() == null, label + " soul-mark selection cannot bypass wall");
        Check(!Advance(10) && !player.IsPossessionInProgress && !ai.IsPossessionLocked, label + " cannot channel");
        Check(!target.enabled && player.enabled, label + " control unchanged");
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsPreparingGameplay || DocumentReader.IsInputBlocked) yield return null;
        foreach (bool ghost in new[] { false, true })
        foreach (Vector2 direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
        {
            Setup(ghost, direction);
            Check(Call("FindControlSwitchCandidate") == target, "clear target in direction " + direction);
            Wall("Blocks", direction * .55f + Vector2.up * .25f,
                Mathf.Abs(direction.x) > 0 ? new Vector2(.08f, 2) : new Vector2(2, .08f));
            Blocked("ghost=" + ghost + " direction=" + direction);
            Set(player, "controlSwitchLayers", (LayerMask)0);
            Blocked("registry fallback with overlap query disabled");
        }
        foreach (string layer in new[] { "Blocks", "Hidden Blocks" })
        {
            Setup(); bool triggers = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = false;
                var wall = Wall(layer, new Vector2(.55f, .25f), new Vector2(.08f, 2), true);
                Blocked("trigger wall " + layer);
                Check(!Physics2D.queriesHitTriggers, "query restores global physics setting");
                wall.enabled = false;
                Check(Call("FindControlSwitchCandidate") == target, "disabled/destroyed wall permits possession");
            }
            finally { Physics2D.queriesHitTriggers = triggers; }
        }
        foreach (string layer in new[] { "Default", "Visible Non Blocking" })
        {
            Setup(); Wall(layer, new Vector2(.55f, .25f), new Vector2(.1f, 2));
            Check(Call("FindControlSwitchCandidate") == target, layer + " furniture does not become a wall");
        }
        foreach (bool atSource in new[] { true, false })
        {
            Setup(); bool starts = Physics2D.queriesStartInColliders;
            try
            {
                Physics2D.queriesStartInColliders = false;
                Wall("Blocks", new Vector2(atSource ? 0 : 1.1f, .25f), new Vector2(.08f, 1));
                Blocked("wall contains " + (atSource ? "source" : "target") + " center");
            }
            finally { Physics2D.queriesStartInColliders = starts; }
        }
        Setup(); player.gameObject.layer = target.gameObject.layer = LayerMask.NameToLayer("Blocks");
        Check(Call("FindControlSwitchCandidate") == target, "character colliders never self-occlude");
        target.gameObject.SetActive(false);
        Check(Call("FindControlSwitchCandidate") == null, "unloaded target cannot enter registry fallback");

        Setup(); Check(!Advance(.3f) && player.IsPossessionInProgress && ai.IsPossessionLocked, "normal channel starts");
        var particles = Get<PossessionTransferParticles>(player, "possessionTransferParticles");
        int energy = player.CharacterData.FinalPossessionEnergy;
        var door = Wall("Blocks", new Vector2(.55f, .25f), new Vector2(.1f, 2));
        Check(!Advance(10) && !player.IsPossessionInProgress && !ai.IsPossessionLocked, "door closing interrupts and unlocks");
        Check(Get<ZeldaFourWayMover>(player, "pendingControlTarget") == null && player.PossessionProgress == 0, "interrupted progress resets");
        Check(!Get<bool>(particles, "emitting") && Get<bool>(particles, "cancelled"), "wall interruption cancels transfer particles");
        Check(player.CharacterData.FinalPossessionEnergy == energy && !target.enabled, "no energy or control transfer through wall");
        Check(!(bool)Call("TrySwitchControl", target), "direct final-transfer call also checks wall");
        door.transform.position = new Vector3(.55f, 3); Physics2D.SyncTransforms();
        Advance(0, false); Set(player, "possessionInteractionSelected", true);
        Check(!Advance(.1f) && player.PossessionProgress < .1f, "opening passage restarts from zero");
        Check(Advance(4) && target.enabled && !player.enabled && !ai.IsPossessionLocked, "clear passage completes normally");
        Clear();
        var regression = (IEnumerator)typeof(AttackInterruptsPossessionSmoke).GetMethod("Verify", F).Invoke(null, null);
        while (regression.MoveNext()) yield return regression.Current;
    }
}
