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

[InitializeOnLoad]
public static class GravekeeperQuestSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string ScenePath = "Assets/Scenes/Level2/Level2-Floor-1.unity";
    const string Q = QuestJournalManager.GravekeeperQuestId;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static GravekeeperQuestSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Debug.Log("GRAVEKEEPER_CHECK " + label); }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static string Node => Get<CharacterDialogue.Node>(Window, "current").id;
    static void Select(int i) => typeof(CharacterDialogueWindow).GetMethod("Select", F).Invoke(Window, new object[] { i });
    static CharacterDialogue.Choice Goodbye() => new CharacterDialogue.Choice { text = "再见。" };
    static CharacterDialogue.Choice Action(string text, string next, DialogueOptionEffect effect) =>
        new CharacterDialogue.Choice { text = text, action = CharacterDialogue.ChoiceAction.Continue, nextNodeId = next, effect = effect };
    static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

    public static void VerifyInstalled()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project required");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var npc = Object.FindObjectsOfType<CharacterDialogue>(true).Single(x => x.gameObject.scene == scene && x.name == "守墓人");
        var target = Object.FindObjectsOfType<GraveyardCrystalQuestTarget>(true).Single(x => x.gameObject.scene == scene);
        var progression = npc.GetComponent<DialogueQuestProgression>();
        Check(Get<DialogueQuestProgression>(npc, "questProgression") == progression && progression.QuestId == Q, "installed dialogue progression reference");
        Check(Get<GravekeeperDialogueEffect.Operation>(npc.FindNode("start").choices[1].effect, "operation") == GravekeeperDialogueEffect.Operation.Accept, "installed acceptance operation");
        Check(Get<GravekeeperDialogueEffect.Operation>(npc.FindNode("waiting").choices[1].effect, "operation") == GravekeeperDialogueEffect.Operation.Report, "installed report operation");
        Check(npc.FindNode("finished").text.Contains("7028") && npc.DisplayFont != null, "installed reward text and font");
        foreach (var effect in npc.GetComponents<GravekeeperDialogueEffect>())
            Check(Get<GraveyardCrystalQuestTarget>(effect, "crystalTarget") == target && Get<DialogueQuestProgression>(effect, "progression") == progression, "installed effect bindings");
        var map = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1");
        foreach (string id in new[] { QuestJournalManager.GravekeeperCrystalTarget, QuestJournalManager.GravekeeperReturnTarget })
            Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, id, out var pos) &&
                map.QuestTargets.Any(t => t.questId == id && Vector2.Distance(t.position, pos) < .001f), "installed live and cached map target " + id);
        Debug.Log("GRAVEKEEPER_INSTALLED_PASS checks=" + checks);
        KnownPasswordsSmoke.Run();
    }

    // Author only the isolated copy, then transfer just its new YAML objects to the working scene.
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project required");
        PlayerSettings.productName = "gravekeeper-quest-smoke";
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var npc = Object.FindObjectsOfType<PriestZeldaCharacterData>(true).Single(x => x.gameObject.scene == scene && x.name == "守墓人").gameObject;
        var crystal = Object.FindObjectsOfType<EnergyCrystal>(true).Single(x => x.gameObject.scene == scene);
        Vector3 npcPosition = npc.transform.position, crystalPosition = crystal.transform.position;
        var target = Ensure<GraveyardCrystalQuestTarget>(crystal.gameObject);
        var progression = Ensure<DialogueQuestProgression>(npc);
        Set(progression, "questId", Q); Set(progression, "questTitle", QuestJournalManager.GravekeeperQuestTitle);
        Set(progression, "questDetails", QuestJournalManager.GravekeeperAcceptedDetails);
        Set(progression, "completedDetails", QuestJournalManager.GravekeeperCompletedDetails);
        Set(progression, "acceptedNodeId", "waiting"); Set(progression, "completedNodeId", "finished");
        var effects = npc.GetComponents<GravekeeperDialogueEffect>();
        var accept = effects.Length > 0 ? effects[0] : npc.AddComponent<GravekeeperDialogueEffect>();
        var report = effects.Length > 1 ? effects[1] : npc.AddComponent<GravekeeperDialogueEffect>();
        foreach (var effect in new[] { accept, report }) { Set(effect, "progression", progression); Set(effect, "crystalTarget", target); }
        Set(accept, "operation", GravekeeperDialogueEffect.Operation.Accept);
        Set(report, "operation", GravekeeperDialogueEffect.Operation.Report);
        var dialogue = Ensure<CharacterDialogue>(npc);
        Set(dialogue, "speakerName", "守墓人"); Set(dialogue, "questProgression", progression);
        Set(dialogue, "dialogueMemoryId", "level2.gravekeeper.dialogue");
        Set(dialogue, "displayFont", AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath("292af55397c443f6a5d7774297dac481")));
        Set(dialogue, "nodes", new[] {
            new CharacterDialogue.Node { id = "start",
                text = "别往埋骨地深处走。那些下葬的人又站起来了，打碎了还会重新拼好。\n\n原本用来安抚亡者的镇抚水晶受了污染。只要它还亮着，死者就不得安宁。我守了一辈子墓，却连让他们好好躺着都办不到。\n\n你若愿意把那颗水晶毁掉，我就告诉你教堂二楼圣物间的密码。那地方我从前去过，密码还记得。怎么样，肯帮这个忙吗？",
                choices = new[] { Goodbye(), Action("我去摧毁水晶。", "waiting", accept) } },
            new CharacterDialogue.Node { id = "waiting",
                text = "埋骨地那颗受污染的镇抚水晶，已经毁掉了吗？\n\n别只顾着打那些骷髅。水晶还在，它们就会一次次复原。等你把水晶毁掉，我答应的密码一定告诉你。",
                differentActorOpeningText = "你不是上次答应帮忙的那个人。是他请你来的？\n\n既然你也知道这件事，我就不多问了。埋骨地被污染的镇抚水晶毁掉了吗？只要那东西不再让死者复活，我答应的密码照样算数。",
                choices = new[] { Goodbye(), Action("被污染的水晶已经被摧毁了。", "finished", report) } },
            new CharacterDialogue.Node { id = "finished",
                text = "终于……他们总算能安静些了。谢谢你。\n\n说好的报酬：教堂二楼圣物间，密码是7028。记清楚，七、零、二、八。\n\n不过，知道密码还不够。要拿到圣物，你还得找到圣物间钥匙。我能告诉你的只有这些，剩下的就看你自己了。",
                differentActorOpeningText = "你和上回来报信的人不是同一个人。水晶已经毁了，这份人情我不会赖掉。\n\n若你也是来问圣物间的事，记住：教堂二楼，密码7028。不过要取得里面的圣物，还得有圣物间钥匙。别以为只知道密码就能拿到东西。",
                choices = new[] { Goodbye() } }
        });
        foreach (var component in new Component[] { target, progression, accept, report, dialogue }) EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Check(npc.transform.position == npcPosition && crystal.transform.position == crystalPosition, "scene placement untouched");
        Check(crystal.Skeletons.Count == 7, "all seven existing skeleton bindings preserved");
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, QuestJournalManager.GravekeeperCrystalTarget, out var cp) && cp == (Vector2)crystalPosition, "authored crystal map target");
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, QuestJournalManager.GravekeeperReturnTarget, out var np) && np == (Vector2)npcPosition, "authored keeper map target");
        Debug.Log("GRAVEKEEPER_POSITIONS crystal=" + cp.ToString("F6") + " keeper=" + np.ToString("F6"));
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { ScenePath, "Assets/Resources/MiniMaps/Level2-Floor-1.asset", scene.name });
        AssetDatabase.SaveAssets();

        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var copy = Object.Instantiate(npc); copy.name = npc.name; copy.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(copy, fixture); copy.transform.position = Vector3.zero;
        copy.GetComponent<ZeldaCharacterAiBase>().enabled = false; copy.SetActive(true);
        var crystalCopy = Object.Instantiate(crystal.gameObject); crystalCopy.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(crystalCopy, fixture); crystalCopy.transform.position = Vector3.right * 10;
        Set(crystalCopy.GetComponent<EnergyCrystal>(), "skeletons", new List<SkeletonZeldaCharacterData>());
        foreach (var effect in copy.GetComponents<GravekeeperDialogueEffect>()) Set(effect, "crystalTarget", crystalCopy.GetComponent<GraveyardCrystalQuestTarget>());
        crystalCopy.SetActive(true);
        EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(fixture);
        EditorSceneManager.SaveScene(fixture, "Assets/GravekeeperFixture.unity");
        SessionState.SetBool(nameof(GravekeeperQuestSmoke), true); EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(GravekeeperQuestSmoke), false))
            { SessionState.SetBool(nameof(GravekeeperQuestSmoke), false); steps.Push(Verify()); deadline = EditorApplication.timeSinceStartup + 150; }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            var step = steps.Peek();
            if (!step.MoveNext()) steps.Pop(); else if (step.Current is IEnumerator nested) steps.Push(nested);
            if (steps.Count == 0) { Debug.Log("GRAVEKEEPER_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception ex) { Debug.LogException(ex); steps.Clear(); EditorApplication.Exit(1); }
    }
    static IEnumerator Open(CharacterDialogue npc, ZeldaFourWayMover player)
    {
        player.transform.position = npc.transform.position + Vector3.right;
        while (CharacterDialogueWindow.BlocksInput || player.IsAttacking) yield return null;
        yield return null; CharacterDialogueWindow.Open(npc, player); yield return null;
        Check(Window != null && CharacterDialogueWindow.IsOpen, "actual dialogue opens");
    }
    static IEnumerator Close(CharacterDialogue npc) { CharacterDialogueWindow.CloseFor(npc); yield return null; yield return null; }
    static ZeldaFourWayMover Player(string name)
    {
        var go = new GameObject(name); go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(mover); return mover;
    }
    static QuestJournalManager.SaveState Save(QuestJournalManager journal) => JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
    static IEnumerator Verify()
    {
        while (GameSaveSystem.IsPreparingSceneSave || DocumentReader.IsInputBlocked) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var npc = Object.FindObjectOfType<CharacterDialogue>();
        var crystal = Object.FindObjectOfType<EnergyCrystal>();
        var player = Player("first visitor"); yield return null;
        var accept = npc.FindNode("start").choices[1].effect;
        var report = npc.FindNode("waiting").choices[1].effect;
        Check(!journal.ContainsEntry(Q) && !journal.CaptureSaveState().churchRelicPasswordKnown, "initial state reveals no password or quest");
        Check(!report.TryExecute(npc, player), "cannot report before acceptance");
        yield return Open(npc, player); Select(0); yield return null;
        Check(!journal.ContainsEntry(Q), "declining does not accept");
        yield return Open(npc, player); Select(1); yield return null;
        Check(Node == "waiting" && journal.ContainsEntry(Q), "accept moves to second stage");
        Check(!Get<Button[]>(Window, "buttons")[1].interactable, "report disabled until destruction");
        Check(!accept.TryExecute(npc, player), "cannot accept twice");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Q) == QuestJournalManager.GravekeeperCrystalTarget, "accepted quest tracks crystal");
        yield return Close(npc);
        crystal.gameObject.SetActive(false); yield return null;
        Check(!journal.GraveyardCrystalDestroyed && !report.TryExecute(npc, player), "streaming unload is not destruction");
        crystal.gameObject.SetActive(true);
        var other = new GameObject("unrelated crystal").AddComponent<EnergyCrystal>(); other.ReceiveAttack(1);
        Check(!journal.GraveyardCrystalDestroyed, "unrelated crystal cannot complete objective"); Object.Destroy(other.gameObject);
        crystal.ReceiveAttack(0); Check(!journal.GraveyardCrystalDestroyed, "zero damage does not count");
        var waitingSave = Save(journal);
        player.gameObject.SetActive(false); var second = Player("second visitor"); yield return null;
        journal.ApplySaveState(waitingSave);
        yield return Open(npc, second);
        Check(Get<string>(Window, "history").Contains("你不是上次"), "waiting different-body opening survives save roundtrip");
        yield return Close(npc);
        yield return Open(npc, second); Check(!Get<string>(Window, "history").Contains("你不是上次"), "same-body revisit returns to regular opening"); yield return Close(npc);
        crystal.ReceiveAttack(1);
        Check(crystal.IsBroken && journal.GraveyardCrystalDestroyed && !journal.IsEntryCompleted(Q), "correct crystal advances objective but requires report");
        Check(!journal.CaptureSaveState().churchRelicPasswordKnown, "destruction alone does not reveal code");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Q) == QuestJournalManager.GravekeeperReturnTarget, "destroyed crystal redirects marker to keeper");
        Check(journal.TryGetEntry(Q, out var entry) && entry.Details.Contains("向守墓人复命"), "journal updates objective");
        var texts = new List<string>(); var completed = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { Q, journal, texts, completed });
        Check(texts.Count == 2 && completed[0] && !completed[1], "HUD shows destruction done and report pending");

        var saved = Save(journal); var crystalState = crystal.CaptureRuntimeState();
        var old = npc.gameObject.scene; var next = SceneManager.CreateScene("GravekeeperReturn");
        var npcCopy = Object.Instantiate(npc.gameObject); npcCopy.SetActive(false); npcCopy.transform.SetParent(null); SceneManager.MoveGameObjectToScene(npcCopy, next);
        var crystalCopy = Object.Instantiate(crystal.gameObject); crystalCopy.SetActive(false); crystalCopy.transform.SetParent(null); SceneManager.MoveGameObjectToScene(crystalCopy, next);
        foreach (var effect in npcCopy.GetComponents<GravekeeperDialogueEffect>()) Set(effect, "crystalTarget", crystalCopy.GetComponent<GraveyardCrystalQuestTarget>());
        SceneManager.MoveGameObjectToScene(second.gameObject, next); SceneManager.SetActiveScene(next);
        var unloading = SceneManager.UnloadSceneAsync(old); while (!unloading.isDone) yield return null;
        journal.ResetAllEntries(); journal.ApplySaveState(saved);
        crystal = crystalCopy.GetComponent<EnergyCrystal>(); crystal.ApplyRuntimeState(crystalState); crystalCopy.SetActive(true);
        npcCopy.SetActive(true); npc = npcCopy.GetComponent<CharacterDialogue>();
        report = npc.FindNode("waiting").choices[1].effect;
        yield return Open(npc, second);
        Check(Node == "waiting" && Get<Button[]>(Window, "buttons")[1].interactable, "report available after scene unload and restore");
        Select(1); yield return null;
        Check(Node == "finished" && journal.IsEntryCompleted(Q), "report completes quest and gives final dialogue");
        Check(Get<string>(Window, "history").Contains("7028") && Get<string>(Window, "history").Contains("钥匙"), "reward explains password and separate key");
        Check(journal.CaptureSaveState().churchRelicPasswordKnown && QuestJournalManager.ResolveQuestMapTargetId(Q) == null, "reward unlocks notebook and removes target");
        Check(!report.TryExecute(npc, second), "cannot deliver twice");
        yield return Close(npc); saved = Save(journal); journal.ResetAllEntries(); journal.ApplySaveState(saved);
        second.gameObject.SetActive(false); var third = Player("third visitor"); yield return null;
        yield return Open(npc, third);
        Check(Node == "finished" && Get<string>(Window, "history").Contains("上回来报信"), "completed different-body opening persists"); yield return Close(npc);
        journal.ResetAllEntries(); crystal.ApplyRuntimeState(new EnergyCrystal.RuntimeState { broken = false });
        crystal.ReceiveAttack(1);
        Check(journal.GraveyardCrystalDestroyed && !journal.ContainsEntry(Q), "pre-acceptance destruction only saves evidence");
        accept = npc.FindNode("start").choices[1].effect;
        Check(accept.TryExecute(npc, third) && report.TryExecute(npc, third), "pre-destroyed crystal can accept and report immediately");
        journal.ResetAllEntries(); crystal.gameObject.SetActive(false);
        crystal.ApplyRuntimeState(crystalState); Check(!journal.GraveyardCrystalDestroyed, "inactive restore waits for target sync");
        Check(accept.TryExecute(npc, third) && journal.GraveyardCrystalDestroyed, "dialogue synchronizes old save broken crystal while streamed out");
        journal.ResetAllEntries();
        Check(!journal.GraveyardCrystalDestroyed && !journal.CaptureSaveState().churchRelicPasswordKnown, "new game resets evidence and reward");
    }
}
