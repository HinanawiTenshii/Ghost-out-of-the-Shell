using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class QuestHierarchySmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const string Parent = QuestJournalManager.Level2SandwormQuestId;
    const string Bait = QuestJournalManager.SandwormBaitQuestId;
    const string Mortar = QuestJournalManager.SandwormMortarQuestId;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static QuestHierarchySmoke() { EditorApplication.update += Tick; }
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Debug.Log("HIERARCHY_CHECK " + label); }
    static void Seed(QuestJournalManager j) => j.AddOrUpdateEntry(Parent, QuestJournalManager.Level2SandwormQuestTitle, "击杀沙虫并取得试炼证明", "Level2-Floor1");
    static QuestJournalManager.SaveState Save(QuestJournalManager j) => JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(j.CaptureSaveState()));

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated test only");
        PlayerSettings.productName = "quest-hierarchy-smoke";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string floor in new[] { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2" })
        {
            string path = "Assets/Scenes/Level2/" + floor + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var npc in Object.FindObjectsOfType<CharacterDialogue>(true).Where(d => d.gameObject.scene == scene &&
                (d.name == "诡异商人" || d.name == "大工程师")))
            {
                Check(npc.FindNode("before_hunt") != null && npc.FindNode("before_hunt").choices.Length == 1,
                    npc.name + " authored pre-parent conversation with exit only");
                Check(!npc.FindNode("before_hunt").text.Contains("1551"), "locked greeting does not reveal safe password");
                if (npc.name == "诡异商人")
                    Check(Get<string>(npc.GetComponent<DialogueQuestProgression>(), "questTitle") == "它不喜欢烟雾", "merchant authored title renamed");
                else
                    Check(Get<QuestJournalEntryDefinition>(npc.GetComponent<DialogueActorAgreement>(), "questOnAccept").title == "→→↑", "engineer authored title uses right-right-up");
                var clone = Object.Instantiate(npc.gameObject); clone.name = npc.name; clone.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(clone, fixture); clone.transform.position = npc.name == "诡异商人" ? Vector3.zero : Vector3.right * 5;
                clone.SetActive(true); clone.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            }
            typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null,
                new object[] { path, "Assets/Resources/MiniMaps/" + floor + ".asset", floor });
            AssetDatabase.SaveAssetIfDirty(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + floor));
            EditorSceneManager.CloseScene(scene, true);
        }
        SceneManager.SetActiveScene(fixture);
        SessionState.SetBool(nameof(QuestHierarchySmoke), true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(QuestHierarchySmoke), false))
            { SessionState.SetBool(nameof(QuestHierarchySmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 150; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("hierarchy timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("QUEST_HIERARCHY_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        Time.timeScale = 0;
        var j = QuestJournalManager.GetOrCreate(); j.ResetAllEntries();
        var rows = new List<QuestJournalManager.HierarchyRow>();
        Check(!j.AddOrUpdateEntry("child", "child", "details", "", "parent"), "child cannot trigger before parent");
        Check(!j.RecordQuestCompletion("child") && !j.CaptureSaveState().pendingCompleted.Contains("child"), "early child completion not queued");
        Check(!j.ConfigureParent("parent", "child") && !j.ConfigureParent("self", "self"), "cycles and self-parent rejected");
        var trigger = new GameObject("gated trigger").AddComponent<QuestJournalEventTrigger>();
        Set(trigger, "journalChanges", new[] { new QuestJournalEventChange { entryId = "child", parentEntryId = "parent", title = "child" } });
        trigger.TriggerJournalChanges(); Check(!trigger.HasTriggered, "blocked one-shot remains usable");
        var state = Save(j); j.ResetAllEntries(); j.ApplySaveState(state);
        Check(!j.CanTriggerEntry("child"), "untriggered parent relationship survives save");
        j.AddOrUpdateEntry("parent", "parent", "details");
        j.GetVisibleHierarchy(rows); Check(rows.Count == 1 && !rows[0].hasChildren, "no foldout for untriggered children");
        trigger.TriggerJournalChanges(); Check(trigger.HasTriggered && j.ContainsEntry("child"), "parent enables same trigger later");
        j.AddOrUpdateEntry("root2", "root2", "");
        j.AddOrUpdateEntry("child2", "child2", "", "", "parent");
        j.AddOrUpdateEntry("grandchild", "grandchild", "", "", "child");
        j.GetVisibleHierarchy(rows);
        Check(string.Join(",", rows.Select(r => r.entry.Id)) == "parent,child,grandchild,child2,root2", "depth-first order groups late children under parent");
        Check(rows[2].depth == 2, "nested hierarchy supported");
        j.TrackEntry("grandchild"); j.SetEntryCollapsed("parent", true); j.GetVisibleHierarchy(rows);
        Check(rows.Count == 2 && j.TrackedEntryId == "grandchild", "folding does not untrack child");
        state = Save(j); j.ResetAllEntries(); j.ApplySaveState(state);
        Check(j.IsEntryCollapsed("parent") && j.GetParentEntryId("grandchild") == "child", "fold state and nested links persist");
        j.SetEntryCollapsed("parent", false); j.CompleteEntry("grandchild");
        Check(j.TrackedEntryId == "child" && !j.IsEntryCompleted("child"), "child completion returns tracking to parent without completing it");
        j.RemoveEntry("parent");
        Check(j.TrackedEntryId == "root2" && !j.AppendEntryDetails("child", "too early"), "removed parent cannot leave a hidden child tracked or updated");
        j.ResetAllEntries(); Check(!j.IsEntryCollapsed("parent") && j.GetParentEntryId("child") == "", "new game resets generic hierarchy");
        VerifyRenamedSaveTitles(j);

        var invGo = new GameObject("test inventory"); invGo.SetActive(false);
        var inv = invGo.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        invGo.SetActive(true); inv.enabled = false; inv.ClearAll();
        var actor = new GameObject("controlled actor").AddComponent<SwordZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var merchant = Object.FindObjectsOfType<CharacterDialogue>().Single(d => d.name == "诡异商人");
        var engineer = Object.FindObjectsOfType<CharacterDialogue>().Single(d => d.name == "大工程师");
        var accept = merchant.FindNode("start").choices[1].effect;
        var direct = merchant.FindNode("start").choices[2].effect;
        var agreement = engineer.GetComponent<DialogueActorAgreement>();
        inv.SetSlot(0, "synthesis_tools", 1);
        Check(merchant.GetStartNode(mover).id == "before_hunt" && engineer.GetStartNode(mover).id == "before_hunt", "both NPCs use pre-parent dialogue");
        Check(!accept.TryExecute(merchant, mover) && !direct.TryExecute(merchant, mover) && !agreement.TryExecute(engineer, mover), "hidden actions cannot bypass prerequisites");
        Check(inv.GetQuantity("synthesis_tools") == 1 && !agreement.IsAccepted && j.Entries.Count == 0, "blocked actions consume no items and record no agreement");
        Seed(j);
        Check(merchant.GetStartNode(mover).id == "start" && engineer.GetStartNode(mover).id == "start", "parent unlocks original dialogue");
        inv.ClearAll(); Check(accept.TryExecute(merchant, mover), "accept merchant child");
        Check(j.GetParentEntryId(Bait) == Parent && merchant.GetStartNode(mover).id == "waiting", "merchant keeps accepted stage under parent");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Bait) == QuestJournalManager.SandwormToolsTarget, "bait target starts at tools");
        var tools = Object.Instantiate(Resources.Load<SynthesisToolsPickupItem>("PickupItems/SynthesisToolsPickupItem"));
        Check(tools.TryPickUp(), "pick up actual tools"); ItemDescriptionWindow.GetOrCreate().CloseWindow();
        Check(QuestJournalManager.ResolveQuestMapTargetId(Bait) == QuestJournalManager.SandwormMerchantTarget, "tools pickup switches map target to merchant");
        Check(merchant.FindNode("waiting").choices[1].effect.TryExecute(merchant, mover), "deliver tools");
        Check(j.IsEntryCompleted(Bait) && !j.IsEntryCompleted(Parent) && merchant.GetStartNode(mover).id == "shop", "delivery completes only child and opens shop");
        inv.SetSlot(0, "gold_coin", 12);
        var buy = merchant.FindNode("shop").choices[1].effect;
        Check(buy.TryExecute(merchant, mover) && buy.TryExecute(merchant, mover) && inv.GetQuantity("decoy_bomb") == 2,
            "merchant still supports repeated purchases");
        Check(agreement.TryExecute(engineer, mover) && j.ContainsEntry(Mortar), "engineer acceptance creates child");
        Check(engineer.GetStartNode(mover).id == "reminder", "accepting body receives reminder");
        var other = new GameObject("different body").AddComponent<SwordZeldaCharacterData>().gameObject.AddComponent<ZeldaFourWayMover>();
        Check(engineer.GetStartNode(other).id == "stranger", "engineer retains fixed-body stranger dialogue");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Mortar) == QuestJournalManager.SandwormDeviceTarget, "engineer target initially points to device");
        inv.ClearAll(); var device = Object.Instantiate(Resources.Load<MortarControlDevicePickupItem>("PickupItems/MortarControlDevicePickupItem"));
        Check(device.TryPickUp(), "pick up remote control device"); ItemDescriptionWindow.GetOrCreate().CloseWindow();
        Check(QuestJournalManager.ResolveQuestMapTargetId(Mortar) == QuestJournalManager.SandwormMortarTarget, "device switches target to mortar");
        state = Save(j); j.ResetAllEntries(); j.ApplySaveState(state);
        Check(engineer.GetStartNode(mover).id == "reminder" && engineer.GetStartNode(other).id == "stranger", "agreement survives journal serialization");

        foreach (var pair in new[] { new[] { "Level2-Floor1", QuestJournalManager.SandwormMerchantTarget },
            new[] { "Level2-Floor-1", QuestJournalManager.SandwormToolsTarget }, new[] { "Level2-Floor-1", QuestJournalManager.SandwormDeviceTarget },
            new[] { "Level2-Floor2", QuestJournalManager.SandwormMortarTarget } })
        {
            var map = new GameObject("map", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
            map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + pair[0]));
            Check(map.TryGetQuestTarget(pair[1], out _), "unloaded map contains " + pair[1]); Object.Destroy(map.gameObject);
        }

        // Full journal UI: real raycast target and pointer tint transitions.
        while (DocumentReader.IsInputBlocked) yield return null;
        var menuGo = new GameObject("hierarchy menu", typeof(RectTransform)); menuGo.SetActive(false);
        var canvas = menuGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = menuGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
        menuGo.AddComponent<GraphicRaycaster>(); var menu = menuGo.AddComponent<TabJournalMenuController>(); Set(menu, "menuFont", merchant.DisplayFont);
        menuGo.SetActive(true); menu.OpenMenu(); Call(menu, "SetSelectedTab", 1); yield return null;
        var content = Get<RectTransform>(menu, "questListContent");
        Func<string, Transform> row = id => content.Cast<Transform>().Single(t => t.gameObject.activeSelf && t.name == "Quest " + id);
        var parentRow = (RectTransform)row(Parent); var baitRow = (RectTransform)row(Bait); var mortarRow = (RectTransform)row(Mortar);
        Check(-baitRow.anchoredPosition.y - (-parentRow.anchoredPosition.y + parentRow.rect.height) == 4f, "child gap is compact 4 pixels");
        Check(baitRow.rect.width < parentRow.rect.width, "child is indented");
        var fold = parentRow.GetComponentInChildren<QuestFoldoutGraphic>(); var button = fold.GetComponent<Button>();
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, fold.rectTransform.TransformPoint(fold.rectTransform.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject == fold.gameObject, "triangle owns left-side raycast independently of parent");
        var colors = button.colors; colors.fadeDuration = 0; button.colors = colors;
        var normal = fold.canvasRenderer.GetColor(); button.OnPointerEnter(pointer); var hover = fold.canvasRenderer.GetColor();
        button.OnPointerDown(pointer); var pressed = fold.canvasRenderer.GetColor();
        Check(normal != hover && hover != pressed, "triangle has actual hover and press tint feedback");
        string tracked = j.TrackedEntryId; button.OnPointerUp(pointer); button.OnPointerClick(pointer); yield return null;
        Check(j.IsEntryCollapsed(Parent) && !content.Cast<Transform>().Any(t => t.gameObject.activeSelf && t.name == "Quest " + Bait) && j.TrackedEntryId == tracked,
            "triangle click hides children without changing tracking");
        row(Parent).GetComponentInChildren<Button>().onClick.Invoke();
        j.SetEntryCollapsed(Parent, false); Call(menu, "SelectQuestEntry", Bait); yield return null;
        Check(Get<Text>(menu, "questTitleText").text == "它不喜欢烟雾", "child selection displays renamed merchant title");
        Call(menu, "SelectQuestEntry", Mortar); yield return null;
        var titleText = Get<Text>(menu, "questTitleText");
        Check(titleText.text == "→→↑", "engineer UI title is exactly three right-right-up arrows");
        titleText.font.RequestCharactersInTexture("→↑", titleText.fontSize, titleText.fontStyle);
        foreach (char arrow in "→↑")
            Check(titleText.font.GetCharacterInfo(arrow, out var glyph, titleText.fontSize, titleText.fontStyle) &&
                glyph.glyphWidth > 0 && glyph.glyphHeight > 0, "menu font renders arrow " + arrow);
        Get<Button>(menu, "questTrackButton").onClick.Invoke(); Check(j.TrackedEntryId == Mortar, "child is individually trackable");
        Capture(menu, canvas); menu.CloseMenu(); yield return null;
        j.RecordSandwormMortarInstallation();
        Check(j.IsEntryCompleted(Mortar) && j.TrackedEntryId == Parent && !j.IsEntryCompleted(Parent), "installation completes child and returns to hunt");
        state = Save(j); j.ResetAllEntries(); j.ApplySaveState(state);
        Check(j.IsEntryCompleted(Bait) && j.IsEntryCompleted(Mortar) && j.GetParentEntryId(Mortar) == Parent, "completed children persist in hierarchy");

        // Legacy agreement (no child record) and merchant progress survive without auto-accepting parent.
        j.ResetAllEntries(); inv.ClearAll();
        var legacy = new QuestJournalManager.SaveState();
        legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Bait, title = "沙虫的诱饵", details = "old progress" });
        legacy.dialogueActors.Add(new QuestJournalManager.SavedDialogueActor { dialogueId = QuestJournalManager.SandwormMortarAgreementId, characterId = actor.IndividualAccessId });
        j.ApplySaveState(legacy); j.GetVisibleHierarchy(rows);
        Check(rows.Count == 0 && j.ContainsEntry(Bait) && !j.ContainsEntry(Parent), "old child retained but cannot bypass absent parent");
        Check(engineer.GetStartNode(mover).id == "before_hunt", "old agreement still observes pre-parent dialogue");
        Seed(j);
        Check(j.ContainsEntry(Mortar) && merchant.GetStartNode(mover).id == "waiting" && engineer.GetStartNode(mover).id == "reminder", "parent reveals preserved merchant and migrated engineer progress");
        j.ResetAllEntries(); j.RecordSandwormMortarInstallation(); Seed(j);
        Check(!j.ContainsEntry(Mortar), "early installation does not auto-trigger child");
        Check(agreement.TryExecute(engineer, mover) && j.IsEntryCompleted(Mortar), "already-installed mortar completes only after acceptance");
    }

    static void VerifyRenamedSaveTitles(QuestJournalManager journal)
    {
        foreach (bool completed in new[] { false, true })
        {
            var legacy = new QuestJournalManager.SaveState { tracked = Bait };
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Parent, title = "香料与獠牙", details = "parent" });
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Bait, title = "沙虫的诱饵", details = "saved merchant progress", scene = "Level2-Floor1" });
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Mortar, title = "人肉信标", details = "saved engineer progress", scene = "Level2-Floor-1" });
            if (completed) legacy.completed = new List<string> { Bait, Mortar };
            legacy.collapsed.Add(Parent);
            journal.ApplySaveState(legacy);
            foreach (string id in new[] { Bait, Mortar })
            {
                var entry = journal.Entries.Single(e => e.Id == id);
                Check(entry.Title == (id == Bait ? "它不喜欢烟雾" : "→→↑"), "old save title migrated: " + id);
                Check(journal.IsEntryCompleted(id) == completed && entry.Details == (id == Bait ? "saved merchant progress" : "saved engineer progress"),
                    "rename preserves progress and completion: " + id);
            }
            Check(journal.TrackedEntryId == Bait && journal.IsEntryCollapsed(Parent) && journal.GetParentEntryId(Mortar) == Parent,
                "rename preserves tracking, fold state and parent relationship");
            var savedAgain = Save(journal);
            Check(savedAgain.entries.Single(e => e.id == Bait).title == "它不喜欢烟雾" && savedAgain.entries.Single(e => e.id == Mortar).title == "→→↑",
                "subsequent saves use new titles");
            journal.ResetAllEntries();
        }
    }

    static void Capture(TabJournalMenuController menu, Canvas canvas)
    {
        var camera = new GameObject("preview camera").AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 360; camera.transform.position = new Vector3(0,0,-100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(12,20,35,255); camera.cullingMask = 1 << 30;
        foreach (Transform t in menu.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var rt = new RenderTexture(1280,720,24); camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
        Canvas.ForceUpdateCanvases(); camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../QuestHierarchy-preview.png"), image.EncodeToPNG());
        RenderTexture.active = old; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        Object.Destroy(camera.gameObject); Object.Destroy(rt); Object.Destroy(image);
    }
}
