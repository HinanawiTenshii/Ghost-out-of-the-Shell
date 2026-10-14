using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real authored conversations, real modal, travel and disk save/load in an isolated project.
[InitializeOnLoad]
public static class DialogueActorMemorySmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string A = "DialogueMemoryA", B = "DialogueMemoryB";
    const string PlayerPrefab = "Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab";
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static DialogueActorMemorySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static string Transcript => (string)Get(Window, "history");
    static CharacterDialogue.Node Node => (CharacterDialogue.Node)Get(Window, "current");
    static void Select(int i) => typeof(CharacterDialogueWindow).GetMethod("Select", F).Invoke(Window, new object[] { i });
    static CharacterDialogue Npc(string name) => Object.FindObjectsOfType<CharacterDialogue>(true).Single(c => c.name == name);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "dialogue-actor-memory-smoke";
        BuildFixture("Level2-Floor1", A, new[] { "诡异商人", "赝品工匠" }, true);
        BuildFixture("Level2-Floor2", B, new[] { "宝石商人" }, false);
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/" + A + ".unity" && s.path != "Assets/" + B + ".unity")
            .Concat(new[] { new EditorBuildSettingsScene("Assets/" + A + ".unity", true), new EditorBuildSettingsScene("Assets/" + B + ".unity", true) }).ToArray();
        EditorSceneManager.OpenScene("Assets/" + A + ".unity");
        SessionState.SetInt("DialogueMemoryChecks", checks);
        SessionState.SetBool(nameof(DialogueActorMemorySmoke), true); EditorApplication.EnterPlaymode();
    }
    static void BuildFixture(string original, string name, string[] names, bool buyers)
    {
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + original + ".unity");
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        for (int i = 0; i < names.Length; i++)
        {
            var owner = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true)).Single(c => c.name == names[i]);
            Check(!string.IsNullOrWhiteSpace(owner.DialogueMemoryId), names[i] + " authored memory key");
            var nodes = (CharacterDialogue.Node[])Get(owner, "nodes");
            Check(nodes.Any(n => !string.IsNullOrWhiteSpace(n.differentActorOpeningText)), names[i] + " authored alternate greeting");
            var clone = Object.Instantiate(owner.gameObject); clone.name = names[i];
            clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture);
            clone.transform.position = new Vector3(i * 6, 0); clone.SetActive(true);
            clone.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        }
        SceneManager.SetActiveScene(fixture);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3, 0, -10); camera.GetComponent<Camera>().orthographic = true;
        if (buyers) for (int i = 0; i < 2; i++)
        {
            var buyer = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            buyer.name = "Buyer " + i; buyer.transform.position = new Vector3(-3, -3 - i * 3);
            buyer.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        }
        EditorSceneManager.SaveScene(fixture, "Assets/" + name + ".unity");
        EditorSceneManager.CloseScene(source, true);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DialogueActorMemorySmoke), false))
            {
                SessionState.SetBool(nameof(DialogueActorMemorySmoke), false);
                checks = SessionState.GetInt("DialogueMemoryChecks", 0); deadline = EditorApplication.timeSinceStartup + 150;
                steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("DIALOGUE_ACTOR_MEMORY_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Control(ZeldaFourWayMover actor)
    {
        var previous = ZeldaRuntimeRegistry.GetControlledMover();
        if (previous != actor && previous != null) previous.enabled = false;
        actor.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(actor);
        var ai = actor.GetComponent<ZeldaCharacterAiBase>(); if (ai != null) ai.enabled = false;
    }
    static IEnumerator Talk(CharacterDialogue npc, ZeldaFourWayMover actor, bool different)
    {
        Control(actor); actor.transform.position = npc.transform.position + Vector3.right * 1.4f;
        Physics2D.SyncTransforms(); yield return null;
        var start = npc.StartNode;
        CharacterDialogueWindow.Open(npc, actor);
        Check(Window != null, npc.name + " window opens");
        string expected = different && !string.IsNullOrWhiteSpace(start.differentActorOpeningText) ? start.differentActorOpeningText : start.text;
        Check(Transcript == npc.SpeakerName + "\n" + expected + "\n\n", npc.name + " opening text " + (different ? "different" : "normal"));
        Check(Node == start, "alternate text shares original choices/effects");
        Check(!QuestJournalManager.GetOrCreate().HasDifferentDialogueActor(npc.DialogueMemoryId, actor.CharacterData.IndividualAccessId), "successful opening immediately records actor");
        yield return null;
    }
    static IEnumerator Close(CharacterDialogue npc) { CharacterDialogueWindow.CloseFor(npc); yield return null; }
    static bool Arrived(string scene) => SceneManager.GetActiveScene().name == scene && !DocumentReader.IsInputBlocked &&
        !SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress && !SceneTravelStateManager.Instance.IsQuickRestartPending &&
        !GameSaveSystem.IsLoading && !GameSaveSystem.IsPreparingSceneSave;
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var buyer = GameObject.Find("Buyer 0").GetComponent<ZeldaFourWayMover>();
        var other = GameObject.Find("Buyer 1").GetComponent<ZeldaFourWayMover>();
        string actorId = buyer.CharacterData.IndividualAccessId;
        Check(actorId != other.CharacterData.IndividualAccessId, "same profession and appearance still have different body identities");
        var inventoryObject = new GameObject("Test Inventory"); inventoryObject.SetActive(false);
        var inventory = inventoryObject.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        inventoryObject.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        var merchant = Npc("诡异商人"); var craft = Npc("赝品工匠");
        Check(merchant.DialogueMemoryId != craft.DialogueMemoryId, "NPC memories independent");
        int changes = 0; journal.JournalChanged += () => changes++;
        yield return Talk(merchant, buyer, false); yield return Close(merchant);
        yield return Talk(merchant, buyer, false); yield return Close(merchant);
        yield return Talk(merchant, other, true); yield return Close(merchant);
        yield return Talk(merchant, other, false); yield return Close(merchant);
        yield return Talk(merchant, buyer, true); yield return Close(merchant);
        Check(changes == 0 && journal.Entries.Count == 0, "memory updates do not create journal entries or notifications");
        Control(other); other.transform.position = new Vector3(99, 99); Physics2D.SyncTransforms(); yield return null;
        CharacterDialogueWindow.Open(merchant, other);
        Check(Window == null && journal.HasDifferentDialogueActor(merchant.DialogueMemoryId, other.CharacterData.IndividualAccessId), "rejected opening cannot overwrite remembered actor");
        var before = JsonUtility.ToJson(journal.CaptureSaveState());
        for (int i = 0; i < 10; i++) { var ignored = merchant.StartNode; merchant.CanConverse(other); }
        Check(before == JsonUtility.ToJson(journal.CaptureSaveState()), "availability checks are read-only");
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, QuestJournalManager.Level2SandwormQuestTitle, "hunt");
        yield return Talk(merchant, buyer, false); Select(1); yield return null;
        Check(Node.id == "accepted", "accept quest normally"); yield return Close(merchant);
        yield return Talk(merchant, other, true);
        Check(Node.id == "waiting", "changed actor preserves accepted phase");
        inventory.SetSlot(0, "synthesis_tools", 1); Select(1); yield return null;
        Check(Node.id == "shop" && Transcript.EndsWith(merchant.FindNode("shop").text + "\n\n"), "handover uses normal continuation, not another opening");
        inventory.SetSlot(0, "gold_coin", 12); Select(1); yield return null; Select(1); yield return null;
        Check(inventory.GetQuantity("decoy_bomb") == 2 && inventory.GetQuantity("gold_coin") == 0, "repeated trades and exact prices preserved");
        Check(Transcript.Split(new[] { merchant.FindNode("waiting").differentActorOpeningText }, StringSplitOptions.None).Length == 2, "alternate greeting appears only once per window");
        yield return Close(merchant);
        yield return Talk(merchant, buyer, true); Check(Node.id == "shop", "changed actor final shop greeting"); yield return Close(merchant);
        yield return Talk(craft, buyer, false); yield return Close(craft);
        yield return Talk(craft, other, true); Select(1); yield return null; yield return Close(craft);
        yield return Talk(craft, buyer, true); Check(Node.id == "gem_needed", "changed actor craftsman waiting phase");
        inventory.ClearAll(); inventory.SetSlot(0, "golden_gem", 1); Select(1); yield return null;
        Check(Node.id == "finished" && inventory.GetQuantity("counterfeit_trial_proof") == 1, "different actor can deliver and receive only one reward"); yield return Close(craft);
        yield return Talk(craft, other, true); Check(Node.id == "finished", "completed phase alternate greeting"); yield return Close(craft);
        yield return Talk(craft, buyer, true); yield return Close(craft);
        merchant.gameObject.SetActive(false); merchant.gameObject.SetActive(true); merchant.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        yield return Talk(merchant, buyer, false); yield return Close(merchant);
        var manager = SceneTravelStateManager.GetOrCreate();
        manager.TravelToScene(B, Vector2.right * 1.4f); while (!Arrived(B)) yield return null;
        Check(merchant == null && craft == null, "original NPCs actually unloaded");
        Check(ZeldaRuntimeRegistry.GetControlledMover() == buyer && buyer.CharacterData.IndividualAccessId == actorId, "body identity preserved on real travel");
        var gem = Npc("宝石商人");
        yield return Talk(gem, buyer, false); yield return Close(gem);
        var temporary = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        temporary.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        yield return Talk(gem, temporary.GetComponent<ZeldaFourWayMover>(), true);
        Select(1); yield return null;
        Check(Node.id == "gold_price" && Transcript.EndsWith(gem.FindNode("gold_price").text + "\n\n"), "gem inquiry keeps gated price node and normal continuation"); yield return Close(gem);
        yield return Talk(gem, buyer, true); yield return Close(gem); Object.Destroy(temporary); yield return null;
        var saveSystem = GameSaveSystem.Instance;
        Check(saveSystem.Save(2, false), "real disk save in another scene");
        var save = saveSystem.Read(2);
        Check(save.journal.dialogueActors.Count == 3 && save.journal.dialogueActors.All(a => a.characterId == actorId), "three independent NPC memories written to disk");
        Check(save.player.character.individualAccessId == actorId, "disk player identity matches dialogue memory");
        journal.ResetAllEntries();
        Check(saveSystem.Load(2), "real disk load begins");
        while (!Arrived(B)) yield return null;
        var oldBuyer = buyer; buyer = ZeldaRuntimeRegistry.GetControlledMover();
        Check(buyer != null && buyer != oldBuyer && buyer.CharacterData.IndividualAccessId == actorId, "reconstructed player retains same body identity");
        gem = Npc("宝石商人");
        yield return Talk(gem, buyer, false); yield return Close(gem);
        manager.TravelToScene(A, Vector2.right * 1.4f); while (!Arrived(A)) yield return null;
        merchant = Npc("诡异商人"); craft = Npc("赝品工匠");
        yield return Talk(merchant, buyer, false); Check(Node.id == "shop", "merchant phase and identity persist after cross-scene disk load"); yield return Close(merchant);
        yield return Talk(craft, buyer, false); Check(Node.id == "finished", "craftsman phase and identity persist after cross-scene disk load"); yield return Close(craft);
        var legacy = journal.CaptureSaveState(); legacy.dialogueActors = null;
        journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(legacy)));
        Check(journal.CaptureSaveState().dialogueActors.Count == 0 && merchant.StartNode.id == "shop", "old save has empty memory without resetting quest progress");
        yield return Talk(merchant, buyer, false); yield return Close(merchant);
        Set(merchant, "dialogueMemoryId", ""); yield return Talk(merchant, buyer, false); yield return Close(merchant);
        journal.ResetAllEntries(); Check(journal.CaptureSaveState().dialogueActors.Count == 0, "new game clears all dialogue memories");
    }
}
