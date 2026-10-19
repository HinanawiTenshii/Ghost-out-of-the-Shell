using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Level2QuestFailuresSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Bait = QuestJournalManager.SandwormBaitQuestId, Forgery = QuestJournalManager.CounterfeitQuestId;
    const string Ritual = QuestJournalManager.SummoningQuestId, Knight = QuestJournalManager.KnightAmbushQuestId;
    const string Grave = QuestJournalManager.GravekeeperQuestId;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static QuestJournalManager journal;
    static PersistentInventory inventory;
    static Level2QuestFailuresSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, F).GetValue(o);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    static QuestJournalManager.SaveState Saved() => JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
    static void Reset() { inventory.ClearAll(); journal.ResetAllEntries(); }
    static void Accept(string id)
    {
        string parent = journal.GetParentEntryId(id);
        if (!string.IsNullOrEmpty(parent)) journal.AddOrUpdateEntry(parent, parent, "parent");
        journal.AddOrUpdateEntry(id, id, "test objective");
    }
    static ZeldaCharacterData Actor(QuestCriticalCharacterRole role = QuestCriticalCharacterRole.None)
    {
        var data = new GameObject("Test critical actor").AddComponent<SwordZeldaCharacterData>();
        typeof(ZeldaCharacterData).GetProperty("QuestFailureRole", F).SetValue(data, role);
        return data;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project required");
        PlayerSettings.productName = "level2-quest-failures-smoke";
        foreach (string floor in new[] { "Level2-Floor1", "Level2-Floor2", "Level2-Floor-1" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + floor + ".unity");
            var data = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ZeldaCharacterData>(true)).ToArray();
            foreach (var role in floor == "Level2-Floor1" ?
                new[] { QuestCriticalCharacterRole.BaitMerchant, QuestCriticalCharacterRole.CounterfeitCraftsman } :
                floor == "Level2-Floor2" ? new[] { QuestCriticalCharacterRole.GemMerchant } : new[] { QuestCriticalCharacterRole.Gravekeeper })
                Check(data.Count(d => QuestJournalManager.ResolveCriticalRole(d) == role) == 1, floor + " exact authored role " + role);
            if (floor == "Level2-Floor2")
            {
                var ambush = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KnightCaptainAmbush>(true)).Single();
                Check(ambush.captain != null, "authored captain exists");
            }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt("QuestFailureChecks", checks);
        SessionState.SetBool(nameof(Level2QuestFailuresSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Level2QuestFailuresSmoke), false))
            {
                SessionState.SetBool(nameof(Level2QuestFailuresSmoke), false); checks = SessionState.GetInt("QuestFailureChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 180; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_QUEST_FAILURES_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        journal = QuestJournalManager.GetOrCreate();
        var bag = new GameObject("Test Inventory"); bag.SetActive(false);
        inventory = bag.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        bag.SetActive(true); inventory.enabled = false;
        var roles = new[] { QuestCriticalCharacterRole.BaitMerchant, QuestCriticalCharacterRole.GemMerchant,
            QuestCriticalCharacterRole.CounterfeitCraftsman, QuestCriticalCharacterRole.KnightCaptain, QuestCriticalCharacterRole.Gravekeeper };
        var ids = new[] { Bait, Forgery, Forgery, Knight, Grave };
        for (int i = 0; i < roles.Length; i++) foreach (bool beforeAcceptance in new[] { false, true })
        {
            Reset(); if (!beforeAcceptance) Accept(ids[i]);
            var data = Actor(roles[i]);
            data.gameObject.SetActive(false); yield return null;
            Check(!journal.IsEntryFailed(ids[i]) && journal.GetFailureReason(ids[i]) == null, "streaming disable is not death " + roles[i]);
            data.gameObject.SetActive(true); data.KillFromPotion(); yield return null;
            if (beforeAcceptance)
            {
                Check(!journal.ContainsEntry(ids[i]) && journal.GetFailureReason(ids[i]) != null, "pre-accept death records hidden evidence");
                var pre = Saved(); journal.ResetAllEntries(); journal.ApplySaveState(pre);
                Accept(ids[i]);
            }
            Check(journal.IsEntryFailed(ids[i]) && !journal.IsEntryCompleted(ids[i]), "failed not completed " + roles[i]);
            Check(journal.TrackedEntryId != ids[i] && !journal.TrackEntry(ids[i]), "failed task cannot track");
            Check(QuestJournalManager.ResolveQuestMapTargetId(ids[i]) == null, "failed map marker removed");
            Check(!journal.RecordQuestCompletion(ids[i], true) && !journal.CompleteEntry(ids[i]), "failure cannot become completion");
            var saved = Saved(); journal.ResetAllEntries(); journal.ApplySaveState(saved);
            Check(journal.IsEntryFailed(ids[i]) && journal.TryGetEntry(ids[i], out var e) && e.Details.Contains("任务失败："), "failure and reason survive JSON save");
            Check(!journal.AddOrUpdateEntry(ids[i], "overwrite", "overwrite"), "later triggers cannot reset failure");
        }
        // Milestones are permanent facts, not the current bag contents.
        foreach (var itemRole in new[] { new[] { "decoy_bomb", "0" }, new[] { "golden_gem", "1" }, new[] { "counterfeit_trial_proof", "2" } })
        {
            Reset(); int i = int.Parse(itemRole[1]);
            inventory.SetSlot(0, itemRole[0], 1); inventory.ClearAll();
            var saved = Saved(); journal.ResetAllEntries(); journal.ApplySaveState(saved);
            Actor(roles[i]).KillFromPotion(); yield return null; Accept(ids[i]);
            Check(!journal.IsEntryFailed(ids[i]), "acquired then spent remains safe " + itemRole[0]);
        }
        Reset(); inventory.SetSlot(0, "golden_gem", 1); inventory.ClearAll();
        Actor(QuestCriticalCharacterRole.CounterfeitCraftsman).KillFromPotion(); yield return null; Accept(Forgery);
        Check(journal.IsEntryFailed(Forgery), "gem alone does not release craftsman survival condition");
        Reset(); journal.RecordQuestCompletion(Knight, true);
        Actor(QuestCriticalCharacterRole.KnightCaptain).KillFromPotion(); yield return null; Accept(Knight);
        Check(journal.IsEntryCompleted(Knight) && !journal.IsEntryFailed(Knight), "possess captain before accepting then die is safe");
        Reset(); journal.UnlockChurchRelicPassword();
        Actor(QuestCriticalCharacterRole.Gravekeeper).KillFromPotion(); yield return null; Accept(Grave);
        Check(!journal.IsEntryFailed(Grave), "known password releases keeper condition");
        // Delivery unlocks repeat transactions without completing the acquisition objective.
        Reset(); Accept(Bait);
        var merchant = Actor(QuestCriticalCharacterRole.BaitMerchant);
        var progression = merchant.gameObject.AddComponent<DialogueQuestProgression>(); Set(progression, "questId", Bait);
        progression.CompleteQuest();
        Check(progression.IsCompleted && journal.BaitToolsDelivered && !journal.IsEntryCompleted(Bait), "shop unlocked but bomb objective remains active");
        merchant.KillFromPotion(); yield return null;
        Check(journal.IsEntryFailed(Bait), "death after tools but before bomb fails");
        Reset(); Accept(Bait); journal.RecordBaitToolsDelivered("Level2-Floor1"); inventory.SetSlot(0, "decoy_bomb", 1);
        Check(journal.IsEntryCompleted(Bait), "obtaining actual bomb completes objective");
        Actor(QuestCriticalCharacterRole.BaitMerchant).KillFromPotion(); yield return null;
        Check(!journal.IsEntryFailed(Bait), "completed acquisition stays completed");
        Reset(); Accept(Bait);
        var seller = Actor(QuestCriticalCharacterRole.BaitMerchant);
        var sellerQuest = seller.gameObject.AddComponent<DialogueQuestProgression>(); Set(sellerQuest, "questId", Bait);
        var offer = seller.gameObject.AddComponent<DialogueRecipeExchangeEffect>();
        Set(offer, "requiredQuest", sellerQuest);
        Set(offer, "merchandise", Resources.Load<GameObject>("PickupItems/Bombs/DecoyBombPickupItem").GetComponent<PickupItemBase>());
        Set(offer, "costs", new[] { new InventoryItemCost {
            item = Resources.Load<GameObject>("PickupItems/GoldCoinPickupItem").GetComponent<PickupItemBase>(), quantity = 6 } });
        inventory.SetSlot(0, "gold_coin", 20);
        Check(!offer.CanSelect(null, null, out _), "bomb shop still locked before delivering tools");
        sellerQuest.CompleteQuest();
        Check(offer.CanSelect(null, null, out _) && !journal.IsEntryCompleted(Bait), "delivery opens actual recipe shop");
        Check(offer.TryExecute(null, null) && inventory.GetQuantity("decoy_bomb") == 1 && inventory.GetQuantity("gold_coin") == 14,
            "first real exchange gives bomb and deducts six coins");
        Check(journal.IsEntryCompleted(Bait) && offer.TryExecute(null, null) && inventory.GetQuantity("decoy_bomb") == 2,
            "quest completion retains repeated purchasing");
        seller.KillFromPotion(); yield return null;
        Check(!journal.IsEntryFailed(Bait), "successful buyer may lose merchant later");
        Reset(); Accept(Bait); var legacy = Saved(); legacy.failureVersion = 0; legacy.completed.Add(Bait);
        journal.ResetAllEntries(); journal.ApplySaveState(legacy);
        Check(journal.BaitToolsDelivered && !journal.IsEntryCompleted(Bait), "legacy tool completion migrates to shop-unlocked stage");
        Actor(QuestCriticalCharacterRole.BaitMerchant).KillFromPotion(); yield return null;
        Check(journal.IsEntryFailed(Bait), "legacy no-bomb stage observes survival condition");
        Reset(); inventory.SetSlot(0, "decoy_bomb", 1); journal.ApplySaveState(legacy);
        Check(journal.BaitToolsDelivered && journal.IsEntryCompleted(Bait), "legacy save already carrying bomb remains completed");
        Reset(); Accept(Knight);
        var realCaptain = Actor();
        var captainMover = realCaptain.gameObject.AddComponent<ZeldaFourWayMover>(); captainMover.enabled = false;
        var captainAi = realCaptain.gameObject.AddComponent<SquadPatrolCharacterAi>(); captainAi.enabled = false;
        var director = new GameObject("Actual Captain Director").AddComponent<KnightCaptainAmbush>();
        director.captain = captainAi; director.guards = Array.Empty<SquadPatrolCharacterAi>();
        yield return null;
        Check(QuestJournalManager.ResolveCriticalRole(realCaptain) == QuestCriticalCharacterRole.KnightCaptain, "real squad director identifies captain");
        realCaptain.KillFromPotion(); yield return null;
        Check(journal.IsEntryFailed(Knight), "actual captain death callback preserves critical role");
        Object.Destroy(director.gameObject); yield return null;
        // Runtime identity retains a critical NPC's role without its original dialogue components.
        Reset(); var original = Actor(QuestCriticalCharacterRole.GemMerchant);
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(original.CaptureRuntimeState()));
        Object.Destroy(original.gameObject); yield return null;
        var reconstructed = Actor(); reconstructed.ApplyRuntimeState(state);
        var otherScene = SceneManager.CreateScene("QuestFailureOtherFloor"); SceneManager.MoveGameObjectToScene(reconstructed.gameObject, otherScene);
        reconstructed.KillFromPotion(); yield return null; Accept(Forgery);
        Check(journal.IsEntryFailed(Forgery), "reconstructed NPC death in another scene uses saved role");
        // Real death/ritual ordering: normal sacrifices must not fail the quest.
        Reset();
        var circleGo = new GameObject("Test Circle"); circleGo.SetActive(false);
        var circle = circleGo.AddComponent<SummoningCircle>(); Set(circle, "journalQuestId", Ritual);
        var identities = Enumerable.Range(0, 6).Select(_ => ScriptableObject.CreateInstance<CharacterPersistentIdentity>()).ToArray();
        for (int i = 0; i < 6; i++) circle.BindCharacter(i, identities[i]);
        circleGo.SetActive(true); Accept(Ritual);
        for (int i = 0; i < 6; i++)
        {
            var sacrifice = Actor(); typeof(ZeldaCharacterData).GetField("persistentIdentity", F).SetValue(sacrifice, identities[i]);
            sacrifice.KillFromPotion();
            Check(!journal.IsEntryFailed(Ritual), "accepted soul avoids failure during absorption " + i);
        }
        Check(circle.CaptureRuntimeState().consumedIds.Length == 6, "six souls reserved before completion animations");
        var ritualSave = Saved(); Object.Destroy(circleGo); yield return null;
        journal.ResetAllEntries(); journal.ApplySaveState(ritualSave);
        Check(!journal.IsEntryFailed(Ritual), "in-flight sacrifices save safely");
        Reset(); journal.RecordSummoningMarkerBindings(identities, Array.Empty<string>());
        var outside = Actor(); typeof(ZeldaCharacterData).GetField("persistentIdentity", F).SetValue(outside, identities[0]);
        outside.KillFromPotion(); yield return null;
        Check(!journal.ContainsEntry(Ritual), "failed sacrifice before reading does not accept quest");
        var beforeRead = Saved(); journal.ResetAllEntries(); journal.ApplySaveState(beforeRead); Accept(Ritual);
        Check(journal.IsEntryFailed(Ritual) && !journal.IsTrackedSummoningTarget(identities[1].StableId), "reading after outside death fails with no remaining world markers");
        Reset();
        var beforeCircleLoaded = Actor(); typeof(ZeldaCharacterData).GetField("persistentIdentity", F)
            .SetValue(beforeCircleLoaded, Resources.Load<CharacterPersistentIdentity>("ItemIdentity/GrandPriest"));
        beforeCircleLoaded.KillFromPotion(); yield return null; Accept(Ritual);
        Check(journal.IsEntryFailed(Ritual), "original bound identity death before circle scene is visited is remembered");
        // Failure UI remains selectable for inspection but cannot be tracked.
        var menuGo = new GameObject("Failure Menu", typeof(RectTransform)); menuGo.SetActive(false);
        menuGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        menuGo.AddComponent<CanvasScaler>(); menuGo.AddComponent<GraphicRaycaster>();
        var menu = menuGo.AddComponent<TabJournalMenuController>(); Set(menu, "menuFont", Resources.GetBuiltinResource<Font>("Arial.ttf"));
        menuGo.SetActive(true); menu.OpenMenu(); Call(menu, "SetSelectedTab", 1); yield return null;
        Set(menu, "selectedQuestId", Ritual); Call(menu, "RefreshQuestJournal");
        Check(Get<Text>(menu, "questTrackButtonText").text == "任务失败" && !Get<Button>(menu, "questTrackButton").interactable, "failed UI shows disabled failure button");
        Check(Get<Text>(menu, "questDetailsText").text.Contains("任务失败："), "failure explanation in journal");
        menu.CloseMenu(); Object.Destroy(menuGo); yield return null;
        Reset(); Accept(Grave); Check(!journal.IsEntryFailed(Grave), "fresh game/reset clears death evidence");
    }
}
