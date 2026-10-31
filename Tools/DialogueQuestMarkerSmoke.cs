using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in the disposable Unity copy; never modify the user's open scene.
[InitializeOnLoad]
public static class DialogueQuestMarkerSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DialogueQuestMarkerSmoke() { EditorApplication.update += Tick; }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static void Call(object o, string name) => o.GetType().GetMethod(name, F).Invoke(o, null);
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Debug.Log("DIALOGUE_MARKER_CHECK " + label); }
    static GameObject Root(CharacterDialogue d) => Get<GameObject>(d.GetComponent<QuestJournalInteractionMarker>(), "markerRoot");
    static SpriteRenderer Icon(CharacterDialogue d) => Get<SpriteRenderer>(d, "icon");
    static void Refresh(CharacterDialogue d)
    { Call(d, "LateUpdate"); Call(d.GetComponent<QuestJournalInteractionMarker>(), "LateUpdate"); }
    static bool Visible(CharacterDialogue d) { Refresh(d); return Root(d).activeInHierarchy; }
    static QuestJournalManager.SaveState Save(QuestJournalManager j) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(j.CaptureSaveState()));

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "dialogue-quest-marker-smoke";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int index = 0;
        foreach (string floor in new[] { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2" })
        {
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + floor + ".unity", OpenSceneMode.Additive);
            foreach (var npc in Object.FindObjectsOfType<CharacterDialogue>(true).Where(d => d.gameObject.scene == source &&
                new[] { "诡异商人", "大工程师", "赝品工匠", "宝石商人", "弓箭铺店主" }.Contains(d.name)))
            {
                var clone = Object.Instantiate(npc.gameObject); clone.name = npc.name;
                clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture);
                clone.transform.position = Vector3.right * index++ * 4;
                clone.GetComponent<ZeldaCharacterAiBase>().enabled = false; clone.SetActive(true);
            }
            EditorSceneManager.CloseScene(source, true);
        }
        if (index != 5) throw new Exception("Expected all five authored NPCs, got " + index);
        SceneManager.SetActiveScene(fixture);
        SessionState.SetBool(nameof(DialogueQuestMarkerSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DialogueQuestMarkerSmoke), false))
            { SessionState.SetBool(nameof(DialogueQuestMarkerSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Marker tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DIALOGUE_QUEST_MARKER_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        Time.timeScale = 0;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var data = new GameObject("controlled test body").AddComponent<SwordZeldaCharacterData>();
        var player = data.gameObject.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        yield return null;
        var dialogues = Object.FindObjectsOfType<CharacterDialogue>();
        var merchant = dialogues.Single(d => d.name == "诡异商人");
        var engineer = dialogues.Single(d => d.name == "大工程师");
        var craftsman = dialogues.Single(d => d.name == "赝品工匠");
        foreach (var ordinary in dialogues.Where(d => d.name == "宝石商人" || d.name == "弓箭铺店主"))
            Check(ordinary.GetComponent<QuestJournalInteractionMarker>() == null, ordinary.name + " has no false new-task offer");
        Check(!Visible(merchant) && !Visible(engineer), "parent-locked quests have no marker");
        Check(Icon(merchant).gameObject.activeSelf && Icon(engineer).gameObject.activeSelf, "pre-parent dialogue remains available");
        Check(Visible(craftsman), "independent counterfeit task offers marker immediately");
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, "香料与獠牙", "击杀沙虫并取得试炼证明");
        Check(Visible(merchant) && Visible(engineer), "reading parent quest unlocks both child markers");
        foreach (var d in new[] { merchant, engineer, craftsman })
        {
            var glyph = Root(d).GetComponentsInChildren<Renderer>();
            Check(glyph.Length == 1 && Root(d).GetComponent<MeshFilter>() != null && glyph[0].sortingOrder == 29999,
                d.name + " reuses solid quest flag below vision mask");
            Check(glyph[0].bounds.min.y > Icon(d).bounds.max.y + .05f &&
                Mathf.Abs(Root(d).transform.position.x - Icon(d).transform.position.x) < .001f,
                d.name + " marker centered above speech icon without overlap");
            Check(Root(d).scene == d.gameObject.scene, d.name + " marker belongs to correct scene");
        }
        var originalSize = Root(craftsman).GetComponentInChildren<Renderer>().bounds.size;
        craftsman.transform.position += new Vector3(3, 2, 0); craftsman.transform.localScale *= .5f;
        Set(craftsman, "selected", true); Refresh(craftsman);
        Check(Vector3.Distance(originalSize, Root(craftsman).GetComponentInChildren<Renderer>().bounds.size) < .001f &&
            Root(craftsman).GetComponentInChildren<Renderer>().bounds.min.y > Icon(craftsman).bounds.max.y,
            "moving/scaling NPC and hovered bubble keep marker size and spacing");
        craftsman.enabled = false;
        Check(!Visible(craftsman), "disabled dialogue hides task offer");
        craftsman.enabled = true;
        Check(Visible(craftsman), "re-enabled dialogue restores task offer");
        craftsman.gameObject.SetActive(false);
        Check(!Root(craftsman).activeSelf, "distance unloading immediately hides unparented marker");
        craftsman.gameObject.SetActive(true);
        Check(Visible(craftsman) && craftsman.GetComponents<QuestJournalInteractionMarker>().Length == 1, "reload restores single marker");
        var progression = craftsman.GetComponent<DialogueQuestProgression>(); progression.enabled = false;
        Check(!Visible(craftsman), "disabled quest progression hides offer"); progression.enabled = true;
        var ai = craftsman.GetComponent<ZeldaCharacterAiBase>(); ai.enabled = true;
        foreach (var state in new[] { ZeldaAiState.Suspicious, ZeldaAiState.Alert, ZeldaAiState.Hostile })
        { Set(ai, "currentState", state); Check(!Visible(craftsman), state + " hides both hints"); }
        ai.enabled = false;
        Set(craftsman.GetComponent<ZeldaCharacterData>(), "isDead", true);
        Check(!Visible(craftsman), "dead NPC has no offer");
        Set(craftsman.GetComponent<ZeldaCharacterData>(), "isDead", false);
        var craftsmanMover = craftsman.GetComponent<ZeldaFourWayMover>();
        craftsmanMover.ReceiveControl(Vector2.down); ZeldaRuntimeRegistry.ClaimControlledMover(craftsmanMover);
        Check(!Visible(craftsman), "possessed NPC has no offer");
        craftsmanMover.enabled = false; ZeldaRuntimeRegistry.ClaimControlledMover(player);
        player.transform.position = craftsman.transform.position + Vector3.down * .5f;
        CharacterDialogueWindow.Open(craftsman, player);
        Check(CharacterDialogueWindow.IsOpen && !Visible(craftsman) && !Visible(merchant), "open dialogue hides world hints");
        CharacterDialogueWindow.CloseFor(craftsman);
        Check(Visible(craftsman), "closing without acceptance restores marker");
        Check(merchant.FindNode("start").choices[1].effect.TryExecute(merchant, player), "actual merchant accept action succeeds");
        Check(!Visible(merchant) && Icon(merchant).gameObject.activeSelf, "accepting hides quest glyph but retains dialogue");
        Check(engineer.GetComponent<DialogueActorAgreement>().TryExecute(engineer, player), "actual engineer agreement succeeds");
        Check(!Visible(engineer), "engineer agreement hides glyph");
        Check(progression.AcceptQuest() && !Visible(craftsman), "accepting counterfeit task hides glyph");
        var stateSave = Save(journal); journal.ResetAllEntries(); journal.ApplySaveState(stateSave);
        Check(!Visible(merchant) && !Visible(engineer) && !Visible(craftsman), "accepted offers stay hidden after saved-state restore");
        progression.CompleteQuest(); stateSave = Save(journal); journal.ResetAllEntries(); journal.ApplySaveState(stateSave);
        Check(!Visible(craftsman), "completed offer stays hidden after restore");
        journal.ResetAllEntries();
        Check(Visible(craftsman) && !Visible(merchant) && !Visible(engineer), "new game resets offers and parent requirements");
        // Legacy accepted agreements can predate their journal entry. Do not advertise again.
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, "香料与獠牙", "test");
        var agreement = engineer.GetComponent<DialogueActorAgreement>();
        journal.RememberDialogueActor(Get<string>(agreement, "memoryId"), data.IndividualAccessId);
        Check(!Visible(engineer), "legacy remembered agreement without entry does not re-offer");
        var otherScene = SceneManager.CreateScene("Marker travel fixture");
        SceneManager.MoveGameObjectToScene(craftsman.gameObject, otherScene); Refresh(craftsman);
        Check(Root(craftsman).scene == otherScene, "NPC scene change moves marker to new scene");
        Object.Destroy(Root(craftsman)); yield return null;
        Check(Visible(craftsman) && Root(craftsman).scene == otherScene, "marker recovers after old scene visual is destroyed");
        var root = Root(craftsman); Object.Destroy(craftsman.gameObject); yield return null; yield return null;
        Check(root == null, "destroyed NPC leaves no orphan glyph");
        var plain = new GameObject("non-dialogue quest trigger");
        var standard = QuestJournalInteractionMarker.Configure(plain, true, "ordinary-test-quest", null);
        Check(Get<GameObject>(standard, "markerRoot").activeSelf, "ordinary quest trigger still visible");
        journal.AddOrUpdateEntry("ordinary-test-quest", "ordinary", "test");
        Check(!Get<GameObject>(standard, "markerRoot").activeSelf, "ordinary quest trigger still hides after acceptance");
    }
}
