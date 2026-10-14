using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in the isolated Unity test project; never edits authored scenes/saves.
[InitializeOnLoad]
public static class DialogueObstacleSmoke
{
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static readonly List<GameObject> objects = new List<GameObject>();
    static DialogueObstacleSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project only");
        PlayerSettings.productName = "dialogue-obstacle-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(DialogueObstacleSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DialogueObstacleSmoke), false))
            { SessionState.SetBool(nameof(DialogueObstacleSmoke), false); deadline = EditorApplication.timeSinceStartup + 60; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Dialogue physics test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DIALOGUE_OBSTACLE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static GameObject Go(string name) { var value = new GameObject(name); objects.Add(value); return value; }
    static BoxCollider2D Obstacle(string name, int layer, bool trigger = false)
    {
        var go = Go(name); go.layer = layer;
        var box = go.AddComponent<BoxCollider2D>(); box.size = new Vector2(.15f, 1); box.isTrigger = trigger;
        return box;
    }
    static bool CanTalk(CharacterDialogue npc, ZeldaFourWayMover player)
    { Physics2D.SyncTransforms(); return npc.CanConverse(player); }
    static PermissionArea Area(string name, Vector2 position, int level)
    {
        var go = Go(name); go.transform.position = position;
        var area = go.AddComponent<PermissionArea>();
        typeof(PermissionArea).GetField("permissionLevel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(area, level);
        typeof(PermissionArea).GetField("areaSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(area, Vector2.one * .6f);
        go.GetComponent<BoxCollider2D>().size = Vector2.one * .6f;
        Physics2D.SyncTransforms(); return area;
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        int blocks = LayerMask.NameToLayer("Blocks"); Check(blocks >= 0, "Blocks layer exists");
        var speaker = Go("Shopkeeper"); speaker.transform.position = new Vector3(-.65f, 0);
        speaker.AddComponent<SwordZeldaCharacterData>(); speaker.AddComponent<ZeldaFourWayMover>();
        var dialogue = speaker.AddComponent<CharacterDialogue>();
        var actor = Go("Customer"); actor.transform.position = new Vector3(.65f, 0);
        actor.AddComponent<SwordZeldaCharacterData>(); var player = actor.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        Check(CanTalk(dialogue, player), "Nearby living actors can converse");
        var counter = Obstacle("Counter", 0);
        Check(CanTalk(dialogue, player), "Solid Default counter is ignored");
        Check(counter.enabled && !counter.isTrigger, "Counter keeps its solid collision");
        foreach (string name in new[] { "Hidden Blocks", "Visible Non Blocking", "Default" })
        {
            int layer = LayerMask.NameToLayer(name); Check(layer >= 0, "test layer " + name);
            counter.gameObject.layer = layer;
            Check(CanTalk(dialogue, player), "Solid non-Blocks obstacle: " + name);
            counter.isTrigger = true; Check(CanTalk(dialogue, player), "Non-Blocks trigger: " + name); counter.isTrigger = false;
        }
        counter.gameObject.layer = blocks; Check(!CanTalk(dialogue, player), "Blocks wall prevents conversation");
        counter.isTrigger = true; Check(!CanTalk(dialogue, player), "Blocks trigger prevents conversation");
        bool previousQueries = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = false;
            Check(CanTalk(dialogue, player), "Existing global trigger-query policy is preserved");
            counter.isTrigger = false;
            Check(!CanTalk(dialogue, player), "Solid Blocks still obstruct when global trigger queries are disabled");
        }
        finally { Physics2D.queriesHitTriggers = previousQueries; }
        counter.isTrigger = false; counter.gameObject.layer = 0;
        for (int i = 0; i < 80; i++) Obstacle("Counter clutter " + i, 0);
        Check(CanTalk(dialogue, player), "80 non-Blocks colliders cannot overflow dialogue hit buffer");
        counter.gameObject.layer = blocks;
        Check(!CanTalk(dialogue, player), "Clutter does not conceal a Blocks wall");
        counter.enabled = false; Check(CanTalk(dialogue, player), "Disabled wall permits dialogue");
        counter.enabled = true; counter.gameObject.layer = 0;
        actor.transform.position = new Vector3(1.3f, 0); Check(!CanTalk(dialogue, player), "Original interaction distance preserved");
        actor.transform.position = new Vector3(.65f, 0);
        dialogue.enabled = false; Check(!CanTalk(dialogue, player), "Disabled dialogue remains unavailable"); dialogue.enabled = true;
        // Actual modal, not a mock: the opening and continuing checks share the policy.
        float previousTime = Time.timeScale;
        dialogue.BeginDialogue(); Check(CharacterDialogueWindow.IsOpen, "Dialogue window opens across a solid counter");
        var window = Object.FindObjectOfType<CharacterDialogueWindow>();
        Check(window != null && Time.timeScale == 0, "Modal pauses gameplay as before");
        var update = typeof(CharacterDialogueWindow).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        update.Invoke(window, null); Check(CharacterDialogueWindow.IsOpen, "Counter does not immediately close the window");
        counter.gameObject.layer = blocks; Physics2D.SyncTransforms();
        update.Invoke(window, null); Check(!CharacterDialogueWindow.IsOpen, "A newly blocking wall closes conversation");
        Check(Time.timeScale == previousTime, "Modal restores simulation clock");
        yield return null;
        counter.gameObject.layer = 0; Physics2D.SyncTransforms();
        var playerData = player.CharacterData;
        var state = playerData.CaptureRuntimeState(); state.permissionLevel = 0; playerData.ApplyRuntimeState(state);
        var shopArea = Area("Restricted behind counter", speaker.transform.position, 5);
        Check(dialogue.HasDialoguePermission(player), "Restricted NPC location does not block customer standing in public area");
        dialogue.BeginDialogue(); Check(CharacterDialogueWindow.IsOpen, "Public-side customer can open actual merchant window");
        var playerArea = Area("Customer region", actor.transform.position, 3);
        window = Object.FindObjectOfType<CharacterDialogueWindow>();
        update.Invoke(window, null);
        Check(CharacterDialogueWindow.IsOpen, "Permission is only checked on opening, not continuously during conversation");
        CharacterDialogueWindow.CloseFor(dialogue); yield return null;
        Check(CanTalk(dialogue, player) && !dialogue.HasDialoguePermission(player), "Illegal actor keeps interaction offer but fails opening permission");
        dialogue.BeginDialogue(); Check(!CharacterDialogueWindow.IsOpen, "Illegal actor cannot open through E interaction");
        CharacterDialogueWindow.Open(dialogue, player); Check(!CharacterDialogueWindow.IsOpen, "Direct window opening uses same actor-position permission rule");
        state.permissionLevel = 3; playerData.ApplyRuntimeState(state);
        dialogue.BeginDialogue(); Check(CharacterDialogueWindow.IsOpen, "Equal actor-region permission permits conversation despite NPC requiring five");
        CharacterDialogueWindow.CloseFor(dialogue); yield return null;
        var overlap = Area("Stricter overlapping region", actor.transform.position, 4);
        Check(!dialogue.HasDialoguePermission(player), "Highest overlapping actor region wins");
        overlap.enabled = false; Check(dialogue.HasDialoguePermission(player), "Disabled restricted region ignored");
        overlap.enabled = true;
        var foreignScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Foreign permission area");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlap.gameObject, foreignScene);
        Check(dialogue.HasDialoguePermission(player), "Other scene's region ignored");
        state.permissionLevel = 0; playerData.ApplyRuntimeState(state); playerArea.transform.position = Vector3.up * 10;
        Physics2D.SyncTransforms();
        Object.DontDestroyOnLoad(actor);
        Check(dialogue.HasDialoguePermission(player) && CanTalk(dialogue, player), "Traveling actor uses gameplay scene and current position");
        playerArea.transform.position = actor.transform.position; Physics2D.SyncTransforms();
        Check(!dialogue.HasDialoguePermission(player), "Traveling actor still denied inside local restricted area");
        Check(!dialogue.HasDialoguePermission(null), "Missing actor rejected");
        for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }
}
