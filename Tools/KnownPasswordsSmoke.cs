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
public static class KnownPasswordsSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Key = nameof(KnownPasswordsSmoke);
    const string Info = QuestJournalManager.KnownPasswordsEntryId;
    static int checks, frame = -1;
    static IEnumerator suite;
    static double deadline;
    static KnownPasswordsSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Debug.Log("PASSWORD_CHECK " + label); }
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static QuestJournalManager.SaveState Snapshot(QuestJournalManager journal) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
    static QuestJournalManager.Entry InfoEntry(QuestJournalManager journal)
    {
        var rows = new List<QuestJournalManager.HierarchyRow>(); journal.GetVisibleHierarchy(rows);
        return rows.Where(r => r.entry.Id == Info).Select(r => r.entry).SingleOrDefault();
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated test project only");
        PlayerSettings.productName = "known-passwords-smoke";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        foreach (string sceneName in new[] { "Level2-Floor1", "Level2-Floor-1" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + sceneName + ".unity", OpenSceneMode.Additive);
            Component source = sceneName == "Level2-Floor1"
                ? (Component)Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.gameObject.scene == scene && d.name == "酒馆二楼密码")
                : Object.FindObjectsOfType<CharacterDialogue>(true).Single(d => d.gameObject.scene == scene && d.name == "大工程师");
            var clone = Object.Instantiate(source.gameObject); clone.name = source.name;
            clone.transform.SetParent(null); SceneManager.MoveGameObjectToScene(clone, fixture);
            clone.transform.position = sceneName == "Level2-Floor1" ? Vector3.zero : Vector3.right * 5;
            var ai = clone.GetComponent<ZeldaCharacterAiBase>(); if (ai != null) ai.enabled = false;
            clone.SetActive(true);
            foreach (var key in Object.FindObjectsOfType<KeyPickupItem>(true).Where(k => k.gameObject.scene == scene))
            {
                var keyCopy = Object.Instantiate(key.gameObject); keyCopy.name = "TintTest " + key.name;
                keyCopy.transform.SetParent(null); SceneManager.MoveGameObjectToScene(keyCopy, fixture);
                keyCopy.transform.position = Vector3.right * 50; keyCopy.SetActive(true);
            }
            if (sceneName == "Level2-Floor-1")
                foreach (var doc in Object.FindObjectsOfType<DocumentReader>(true).Where(d => d.gameObject.scene == scene &&
                    (d.name == "娱乐中心客房密码" || d.name == "仓库大门密码")))
                {
                    var copy = Object.Instantiate(doc.gameObject); copy.name = doc.name; copy.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(copy, fixture); copy.transform.position = Vector3.zero; copy.SetActive(true);
                }
            EditorSceneManager.CloseScene(scene, true);
        }
        SceneManager.SetActiveScene(fixture);
        if (!AssetDatabase.IsValidFolder("Assets/KnownPasswordTest")) AssetDatabase.CreateFolder("Assets", "KnownPasswordTest");
        EditorSceneManager.SaveScene(fixture, "Assets/KnownPasswordTest/Level2-Floor1.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false)) { SessionState.SetBool(Key, false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("password smoke timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("KNOWN_PASSWORDS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); suite = null; EditorApplication.Exit(1); }
    }

    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var originalScene = SceneManager.GetActiveScene();
        var doc = Object.FindObjectsOfType<DocumentReader>().Single(d => d.name == "酒馆二楼密码");
        var engineer = Object.FindObjectOfType<CharacterDialogue>();
        Check(doc.DocumentText.Contains("9090") && engineer.FindNode("accepted").text.Contains("1551"), "fixed authored clues match recorded codes");
        Check(InfoEntry(journal) != null && !InfoEntry(journal).CanTrack, "level2 gets an informational row even before discovery");
        Check(new[] { "9090", "1551", "9014", "9871" }.All(code => !InfoEntry(journal).Details.Contains(code)), "loading the objects does not reveal passwords");
        foreach (var reading in Object.FindObjectsOfType<DocumentReader>())
            Check(reading.DocumentText.Length > 95 && Get<bool>(reading, "keepAuthoredTextOnLoad"), "expanded document keeps authored copy on load " + reading.name);
        Check(journal.Entries.Count == 0 && !journal.TrackEntry(Info) && QuestJournalManager.ResolveQuestMapTargetId(Info) == null,
            "password notebook is not a quest and cannot be tracked or mapped");

        var data = new GameObject("test player").AddComponent<SwordZeldaCharacterData>();
        var mover = data.gameObject.AddComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        yield return null;
        var agreement = engineer.GetComponent<DialogueActorAgreement>();
        Check(engineer.GetStartNode(mover).id == "before_hunt" && !agreement.TryExecute(engineer, mover), "pre-parent dialogue cannot accept engineer offer");
        Check(!journal.CaptureSaveState().engineerSafePasswordKnown, "rejected engineer interaction does not reveal password");
        journal.AddOrUpdateEntry("test.task", "测试追踪", "保持追踪"); journal.TrackEntry("test.task");
        int notifications = 0, trackingChanges = 0, changes = 0;
        journal.JournalEntriesChanged += _ => notifications++;
        journal.TrackedQuestChanged += () => trackingChanges++;
        journal.JournalChanged += () => changes++;
        Call(doc, "OpenDocument");
        Check(DocumentReader.IsDocumentOpen && journal.CaptureSaveState().tavernRoomPasswordKnown, "reading actual tavern document unlocks its fixed code");
        Check(InfoEntry(journal).Details.Contains("9090") && !InfoEntry(journal).Details.Contains("1551"), "only learned code is shown");
        Check(journal.TrackedEntryId == "test.task" && notifications == 0 && trackingChanges == 0 && changes == 1,
            "unlock refreshes notebook without quest popup or tracking changes");
        Call(doc, "CloseDocument"); yield return null; yield return null;
        Call(doc, "OpenDocument"); Check(changes == 1, "rereading is idempotent"); Call(doc, "CloseDocument"); yield return null; yield return null;
        // The other two fixed triggers must come from the underground scene, not a same-named copy elsewhere.
        var underground = SceneManager.CreateScene("Level2-Floor-1");
        foreach (string documentName in new[] { "娱乐中心客房密码", "仓库大门密码" })
        {
            var reading = Object.FindObjectsOfType<DocumentReader>().Single(d => d.name == documentName);
            Call(reading, "OpenDocument"); Call(reading, "CloseDocument"); yield return null; yield return null;
            var before = journal.CaptureSaveState();
            Check(documentName == "娱乐中心客房密码" ? !before.entertainmentRoomPasswordKnown : !before.warehouseGatePasswordKnown,
                "same-named document outside authored scene does not unlock " + documentName);
            SceneManager.MoveGameObjectToScene(reading.gameObject, underground);
            SceneManager.MoveGameObjectToScene(mover.gameObject, underground); SceneManager.SetActiveScene(underground);
            Call(reading, "OpenDocument");
            string code = documentName == "娱乐中心客房密码" ? "9014" : "9871";
            Check(DocumentReader.IsDocumentOpen && InfoEntry(journal).Details.Contains(code), "actual fixed underground reading unlocks " + code);
            int changeCount = changes;
            Call(reading, "CloseDocument"); yield return null; yield return null;
            Call(reading, "OpenDocument"); Check(changes == changeCount, "underground reread is idempotent " + code);
            Call(reading, "CloseDocument"); yield return null; yield return null;
            SceneManager.SetActiveScene(originalScene); SceneManager.MoveGameObjectToScene(mover.gameObject, originalScene);
            SceneManager.MoveGameObjectToScene(reading.gameObject, originalScene);
        }
        yield return SceneManager.UnloadSceneAsync(underground);
        Check(notifications == 0 && trackingChanges == 0 && journal.TrackedEntryId == "test.task", "all three readings keep task tracking and popup state intact");
        journal.AddOrUpdateEntry(QuestJournalManager.Level2SandwormQuestId, "香料与獠牙", "test");
        Check(agreement.TryExecute(engineer, mover), "actual engineer acceptance succeeds");
        Check(journal.CaptureSaveState().engineerSafePasswordKnown && InfoEntry(journal).Details.Contains("1551"), "acceptance records engineer safe password");
        journal.UnlockChurchRelicPassword();
        Check(InfoEntry(journal).Details.Contains("7028"), "gravekeeper reward records church relic password");

        var menuGo = new GameObject("password journal", typeof(RectTransform)); menuGo.SetActive(false);
        var canvas = menuGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = menuGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
        menuGo.AddComponent<GraphicRaycaster>(); var menu = menuGo.AddComponent<TabJournalMenuController>(); Set(menu, "menuFont", engineer.DisplayFont);
        menuGo.SetActive(true); menu.OpenMenu(); Call(menu, "SetSelectedTab", 1); yield return null;
        var rows = Get<RectTransform>(menu, "questListContent");
        var row = rows.Find("Quest " + Info); Check(row != null && row.gameObject.activeSelf, "real quest menu displays known passwords row");
        string tracked = journal.TrackedEntryId; row.GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(Get<Text>(menu, "questTitleText").text == "已知密码" && Get<Text>(menu, "questDetailsText").text.Contains("9090"), "click row displays password details");
        Canvas.ForceUpdateCanvases();
        var details = Get<Text>(menu, "questDetailsText");
        Check(new[] { "9090", "1551", "9014", "9871", "7028" }.All(code => details.text.Contains(code)), "all five fixed records visible together");
        Check(details.preferredHeight <= details.rectTransform.rect.height, "full notebook fits description frame without clipping");
        Check(!Get<Button>(menu, "questTrackButton").gameObject.activeSelf && journal.TrackedEntryId == tracked, "track button hidden and current quest retained");
        Call(menu, "TrackSelectedQuest"); Check(journal.TrackedEntryId == tracked, "defensive direct track call has no effect");
        Call(menu, "SelectQuestEntry", "test.task"); Check(Get<Button>(menu, "questTrackButton").gameObject.activeSelf, "regular quest retains track action");
        Call(menu, "SelectQuestEntry", Info); Check(rows.Find("Quest " + Info) == row, "selection reuses row without flicker");
        menu.CloseMenu(); Object.Destroy(menuGo); yield return null;

        var state = Snapshot(journal); journal.ResetAllEntries();
        Check(new[] { "9090", "1551", "9014", "9871", "7028" }.All(code => !InfoEntry(journal).Details.Contains(code)), "new-game reset clears all passwords");
        journal.ApplySaveState(state); Check(new[] { "9090", "1551", "9014", "9871", "7028" }.All(code => InfoEntry(journal).Details.Contains(code)), "all codes survive JSON save roundtrip");
        Check(!state.entries.Any(e => e.id == Info), "informational row never enters quest save entries");
        foreach (string sceneName in new[] { "Level2-Floor2", "Level2-Floor-1", "Level2-Desert", "Level1-Floor1", "Level0" })
        {
            var scene = SceneManager.CreateScene(sceneName); SceneManager.SetActiveScene(scene);
            var entry = InfoEntry(journal);
            Check(sceneName.StartsWith("Level2") ? entry != null && entry.Details.Contains("9090") : entry == null, "correct scene scope and retained record " + sceneName);
            SceneManager.SetActiveScene(originalScene); yield return SceneManager.UnloadSceneAsync(scene);
        }
        state.engineerSafePasswordKnown = false; state.tavernRoomPasswordKnown = false;
        state.entertainmentRoomPasswordKnown = false; state.warehouseGatePasswordKnown = false;
        journal.ResetAllEntries(); journal.ApplySaveState(state);
        Check(InfoEntry(journal).Details.Contains("1551") && !InfoEntry(journal).Details.Contains("9090"), "legacy engineer agreement restores only its proven password");
        state.dialogueActors.Clear(); journal.ResetAllEntries(); journal.ApplySaveState(state);
        Check(!InfoEntry(journal).Details.Contains("1551"), "quest presence alone does not reveal unknown password");

        while (DocumentReader.IsInputBlocked) yield return null;
        var inventory = PersistentInventory.Instance;
        if (inventory == null)
        {
            var go = new GameObject("test inventory"); go.SetActive(false);
            inventory = go.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false); go.SetActive(true);
        }
        Set(inventory, "saveToPlayerPrefs", false); inventory.enabled = false;
        foreach (var key in Object.FindObjectsOfType<KeyPickupItem>().Where(k => k.name.StartsWith("TintTest ")).ToArray())
        {
            inventory.ClearAll(); Color color = key.ItemVisual.DisplayColor; string label = key.name;
            Check(key.GetComponent<SpriteRenderer>().color == color, "runtime world key uses authored tint " + label);
            Check(key.TryPickUp(), "pickup actual colored key " + label); ItemDescriptionWindow.GetOrCreate().CloseWindow();
            yield return null; yield return null;
            var slot = inventory.GetSlot(0); string id = slot.ItemInstanceId;
            Check(slot.HasVisualColor && slot.VisualColor == color, "inventory stores matching door tint " + label);
            var inventoryState = JsonUtility.FromJson<PersistentInventory.RuntimeState>(JsonUtility.ToJson(inventory.CaptureRuntimeState()));
            inventory.ClearAll(); inventory.ApplyRuntimeState(inventoryState);
            Check(inventory.GetSlot(0).VisualColor == color, "saved inventory restores key tint " + label);
            Check(inventory.TryDropSelectedItem(), "drop colored key " + label); yield return null;
            var dropped = Object.FindObjectsOfType<KeyPickupItem>().Single(k => k.UniqueInstanceId == id);
            Check(dropped.ItemVisual.DisplayColor == color && dropped.GetComponent<SpriteRenderer>().color == color,
                "dropped key retains instance color instead of prefab default " + label);
            Object.Destroy(dropped.gameObject); yield return null;
        }
    }
}
