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

// Copies the two exact scene-authored NPCs into small fixtures; never plays or saves the large levels.
[InitializeOnLoad]
public static class CounterfeitTrialDialogueSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string Quest = "level2_counterfeit_trial_proof", Gem = "golden_gem", Proof = "counterfeit_trial_proof";
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static CounterfeitTrialDialogueSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("check " + checks + ": " + label); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static CharacterDialogueWindow Window => Object.FindObjectOfType<CharacterDialogueWindow>();
    static Button[] Buttons => (Button[])Get(Window, "buttons");
    static string Node => ((CharacterDialogue.Node)Get(Window, "current")).id;
    static void Select(int index) => typeof(CharacterDialogueWindow).GetMethod("Select", F).Invoke(Window, new object[] { index });

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "counterfeit-trial-dialogue-smoke";
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var craft = Find(scene, "赝品工匠");
        Check(craft.GetComponent<StrangeBlacksmithZeldaCharacterData>() != null, "exact authored craftsman");
        Check(craft.transform.position == new Vector3(-80.41f, -33.49f, 0), "craftsman position preserved");
        Check(craft.DisplayFont != null && craft.SpeakerName == "赝品工匠", "craftsman pixel font and name");
        Check(craft.FindNode("start").choices.Length == 2 && craft.FindNode("gem_needed").choices.Length == 2 && craft.FindNode("finished").choices.Length == 1, "three requested phases");
        Check(craft.FindNode("start").text.Contains("祭坛") && craft.FindNode("gem_needed").text.Contains("深蓝色"), "quest dialogue explains altar and merchant");
        Check(craft.GetComponent<DialogueQuestProgression>().QuestId == Quest, "stable saved progression");
        var first = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var copy = Object.Instantiate(craft.gameObject); copy.name = craft.name;
        SceneManager.MoveGameObjectToScene(copy, first); copy.transform.position = Vector3.zero;
        EditorSceneManager.SaveScene(first, "Assets/CounterfeitCraftFixture.unity");
        EditorSceneManager.CloseScene(scene, true);
        scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity", OpenSceneMode.Additive);
        var merchant = Find(scene, "宝石商人");
        Check(merchant.transform.position == new Vector3(96.32f, 39.73f, 0), "merchant position preserved");
        Check(merchant.DisplayFont != null && merchant.SpeakerName == "宝石商人", "merchant pixel font and name");
        Check(merchant.GetComponents<DialogueQuestGateEffect>().Length == 2, "inquiry and transaction both gated without cross-scene references");
        var second = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        copy = Object.Instantiate(merchant.gameObject); copy.name = merchant.name;
        SceneManager.MoveGameObjectToScene(copy, second); copy.transform.position = Vector3.zero;
        EditorSceneManager.SaveScene(second, "Assets/CounterfeitMerchantFixture.unity");
        EditorSceneManager.CloseScene(scene, true);
        SessionState.SetInt("CounterfeitAuthoredChecks", checks);
        SessionState.SetBool(nameof(CounterfeitTrialDialogueSmoke), true); EditorApplication.EnterPlaymode();
    }
    static CharacterDialogue Find(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<CharacterDialogue>(true)).Single(c => c.name == name);
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(CounterfeitTrialDialogueSmoke), false))
            {
                SessionState.SetBool(nameof(CounterfeitTrialDialogueSmoke), false);
                checks = SessionState.GetInt("CounterfeitAuthoredChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 120; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("COUNTERFEIT_TRIAL_DIALOGUE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Open(CharacterDialogue owner, ZeldaFourWayMover player)
    {
        SceneManager.SetActiveScene(owner.gameObject.scene);
        SceneManager.MoveGameObjectToScene(player.gameObject, owner.gameObject.scene);
        player.transform.position = owner.transform.position + Vector3.right;
        // Picking up / dropping the gem performs a reach gesture; dialogue correctly
        // waits for that animation to finish before accepting another interaction.
        while (player.IsAttacking || CharacterDialogueWindow.BlocksInput) yield return null;
        yield return null; CharacterDialogueWindow.Open(owner, player);
        Check(Window != null && CharacterDialogueWindow.IsOpen, "actual dialogue window opens: " + owner.name +
            ", canConverse=" + owner.CanConverse(player) + ", permission=" + owner.HasDialoguePermission(player) +
            ", blocked=" + CharacterDialogueWindow.BlocksInput + ", playerActive=" + player.isActiveAndEnabled +
            ", distance=" + Vector2.Distance(player.transform.position, owner.transform.position)); yield return null;
    }
    static IEnumerator Close(CharacterDialogue owner) { CharacterDialogueWindow.CloseFor(owner); yield return null; }
    static void Fill(PersistentInventory inv, int coinStacks, int amount)
    {
        inv.ClearAll();
        for (int i = 0; i < inv.SlotCount; i++) inv.SetSlot(i, i < coinStacks ? "gold_coin" : "filler_" + i, i < coinStacks ? amount : 1);
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ApplySaveState(new QuestJournalManager.SaveState());
        var inventoryObject = new GameObject("Test Inventory"); inventoryObject.SetActive(false);
        var inv = inventoryObject.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        inventoryObject.SetActive(true); inv.ClearAll(); inv.enabled = false;
        var craft = Object.FindObjectsOfType<CharacterDialogue>().Single(c => c.name == "赝品工匠");
        var merchant = Object.FindObjectsOfType<CharacterDialogue>().Single(c => c.name == "宝石商人");
        craft.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        merchant.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        var playerObject = new GameObject("Test Buyer");
        playerObject.AddComponent<SwordZeldaCharacterData>(); var player = playerObject.AddComponent<ZeldaFourWayMover>();
        player.ReceiveControl(Vector2.left); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        var gemPrefab = Resources.Load<GameObject>("PickupItems/GoldenGemPickupItem");
        var gem = gemPrefab.GetComponent<GoldenGemPickupItem>();
        Check(gem.ItemId == Gem && gem.ItemName == "金色宝石" && gem.MaxStackSize == 1 && gem.RequiredUseSkill == 0, "golden gem quest item configuration");
        Check(!gem.CanUseFromInventory && !gem.ExecuteUse(player.GetComponent<ZeldaCharacterData>()), "R cannot consume gem");
        Check(gem.ItemDescription.Contains("祭坛") && gem.ItemDescription.Contains("魔力"), "styled item description");
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == Gem) == 1 && inv.ResolveItemPrefab(Gem) == gem, "new item resolves through catalog and inventory");
        var visual = gem.GetComponent<GoldenGemPickupItemVisual>();
        var rows = (string[])typeof(GoldenGemPickupItemVisual).GetMethod("GetPixelRows", F).Invoke(visual, null);
        Check(rows.Length == 24 && rows.All(r => r.Length == 20), "pixel dimensions valid");
        Check(visual.InventoryIcon != null && visual.InventoryIcon.texture.filterMode == FilterMode.Point, "native pixel icon generated");
        Preview(visual.InventoryIcon);
        var ask = merchant.FindNode("start").choices[1].effect;
        var buy = merchant.FindNode("gold_price").choices[1].effect;
        var accept = craft.FindNode("start").choices[1].effect;
        var deliver = craft.FindNode("gem_needed").choices[1].effect;
        inv.SetSlot(0, Gem, 1);
        Check(!deliver.TryExecute(craft, player) && inv.GetQuantity(Gem) == 1, "cannot forge before learning recipe"); inv.ClearAll();
        yield return Open(merchant, player);
        Check(Node == "start" && Buttons[0].gameObject.activeSelf && !Buttons[1].gameObject.activeSelf, "merchant only offers goodbye before quest");
        Select(1); Check(Node == "start" && !ask.TryExecute(merchant, player) && !buy.TryExecute(merchant, player), "hidden hotkey and direct gate calls cannot bypass quest");
        yield return Close(merchant);
        yield return Open(craft, player); Select(0); yield return null;
        Check(!journal.ContainsEntry(Quest), "goodbye leaves quest untouched");
        yield return Open(craft, player); Select(1); yield return null;
        Check(Node == "gem_needed" && journal.ContainsEntry(Quest) && !journal.IsEntryCompleted(Quest), "inquiry accepts quest and enters stage two");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Quest) == QuestJournalManager.CounterfeitMerchantTarget, "quest initially points to gem merchant");
        Check(Buttons[1].gameObject.activeSelf && !Buttons[1].interactable, "missing gem hand-in remains visible but disabled");
        Check(!accept.TryExecute(craft, player), "no double acceptance"); yield return Close(craft);
        // Unload the craftsman's scene completely. Merchant must depend on journal, never its component reference.
        var returnTemplate = Object.Instantiate(craft.gameObject); returnTemplate.SetActive(false); Object.DontDestroyOnLoad(returnTemplate);
        var originalScene = craft.gameObject.scene;
        SceneManager.MoveGameObjectToScene(playerObject, merchant.gameObject.scene); SceneManager.SetActiveScene(merchant.gameObject.scene);
        var unloading = SceneManager.UnloadSceneAsync(originalScene); while (!unloading.isDone) yield return null;
        Check(craft == null && ask.IsVisible(merchant, player), "merchant remains unlocked with original NPC and scene unloaded");
        var acceptedSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ApplySaveState(new QuestJournalManager.SaveState()); Check(!ask.IsVisible(merchant, player), "gate follows loaded save, not stale static cache");
        journal.ApplySaveState(acceptedSave);
        yield return Open(merchant, player);
        Check(Buttons[1].gameObject.activeSelf && Buttons[1].interactable, "quest inquiry visible after cross-floor save restore"); Select(1); yield return null;
        Check(Node == "gold_price" && !Buttons[1].interactable, "price node and disabled empty-wallet purchase"); yield return Close(merchant);
        Check(journal.EntryDetailsContain(Quest, QuestJournalManager.CounterfeitPriceKnown), "learning price records the 30 coin objective");
        var priceSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(priceSave);
        Check(journal.EntryDetailsContain(Quest, QuestJournalManager.CounterfeitPriceKnown) &&
            QuestJournalManager.ResolveQuestMapTargetId(Quest) == QuestJournalManager.CounterfeitMerchantTarget, "price stage and merchant marker survive save restore");
        inv.SetSlot(0, "gold_coin", 20); inv.SetSlot(1, "gold_coin", 9);
        Check(!buy.CanSelect(merchant, player, out _) && !buy.TryExecute(merchant, player) && inv.GetQuantity("gold_coin") == 29, "29 gold insufficient without loss");
        Check(!journal.EntryDetailsContain(Quest, QuestJournalManager.CounterfeitGemAcquired), "failed purchase never advances quest");
        inv.SetSlot(1, "gold_coin", 10);
        Check(QuestJournalManager.ResolveQuestMapTargetId(Quest) == QuestJournalManager.CounterfeitMerchantTarget, "collecting enough coins does not move the marker away from merchant");
        yield return Open(merchant, player); Select(1); yield return null; Check(Buttons[1].interactable, "30 gold across stacks enables purchase");
        Select(1); yield return null;
        Check(Node == "gold_price" && inv.GetQuantity("gold_coin") == 0 && inv.GetQuantity(Gem) == 1, "deducts 20+10 coins, grants one gem and stays open");
        Check(journal.EntryDetailsContain(Quest, QuestJournalManager.CounterfeitGemAcquired) &&
            QuestJournalManager.ResolveQuestMapTargetId(Quest) == QuestJournalManager.CounterfeitCraftsmanTarget, "only successful purchase points back to craftsman");
        Check(!Buttons[1].interactable, "purchase button refreshes immediately"); yield return Close(merchant);
        var gemSlot = Enumerable.Range(0, inv.SlotCount).First(i => inv.GetSlot(i).ItemId == Gem); inv.SelectSlot(gemSlot);
        var save = JsonUtility.FromJson<PersistentInventory.RuntimeState>(JsonUtility.ToJson(inv.CaptureRuntimeState()));
        inv.ClearAll(); inv.ApplyRuntimeState(save); Check(inv.GetQuantity(Gem) == 1, "purchased gem survives inventory save");
        Check(!inv.TryUseSelectedItem() && inv.GetQuantity(Gem) == 1, "inventory R remains disabled");
        while (DocumentReader.IsInputBlocked) yield return null;
        Check(inv.TryDropSelectedItem() && inv.GetQuantity(Gem) == 0, "Q drops purchased gem"); yield return null;
        var dropped = Object.FindObjectsOfType<GoldenGemPickupItem>().Single();
        Check(dropped.TryPickUp() && inv.GetQuantity(Gem) == 1, "dropped gem can be picked up");
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1; yield return null;
        // Reload a fresh copy of the exact authored NPC into a new scene, then forge in a full bag.
        var returnScene = SceneManager.CreateScene("Craftsman Return");
        craft = Object.Instantiate(returnTemplate).GetComponent<CharacterDialogue>(); craft.name = "赝品工匠";
        SceneManager.MoveGameObjectToScene(craft.gameObject, returnScene); craft.gameObject.SetActive(true); craft.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        Object.Destroy(returnTemplate);
        Check(craft.StartNode.id == "gem_needed", "fresh scene NPC starts at persisted second stage");
        deliver = craft.FindNode("gem_needed").choices[1].effect;
        inv.ClearAll(); inv.SetSlot(0, Gem, 1); for (int i = 1; i < inv.SlotCount; i++) inv.SetSlot(i, "filler_" + i, 1);
        yield return Open(craft, player); Check(Buttons[1].interactable, "hand-in uses freed gem slot even with full bag");
        int events = 0; bool atomic = false, reentered = false;
        Action observer = () => { events++; atomic = journal.IsEntryCompleted(Quest) && inv.GetQuantity(Gem) == 0 && inv.GetQuantity(Proof) == 1; reentered |= deliver.TryExecute(craft, player); };
        inv.InventoryChanged += observer; Select(1); inv.InventoryChanged -= observer; yield return null;
        Check(Node == "finished" && inv.GetSlot(0).ItemId == Proof, "one gem becomes one counterfeit and final dialogue appears");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Quest) == null, "completed forgery removes quest marker");
        Check(events == 1 && atomic && !reentered, "reward, cost and quest complete atomically before callbacks");
        Check(!Buttons[1].gameObject.activeSelf && !deliver.TryExecute(craft, player), "no duplicate forging reward"); yield return Close(craft);
        var completeSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ApplySaveState(new QuestJournalManager.SaveState()); journal.ApplySaveState(completeSave);
        craft.gameObject.SetActive(false); craft.gameObject.SetActive(true); craft.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        Check(craft.StartNode.id == "finished" && ask.IsVisible(merchant, player), "completed stage survives reload and previously learned merchant inquiry remains available");
        yield return Open(craft, player); Check(Node == "finished" && Buttons[0].gameObject.activeSelf && !Buttons[1].gameObject.activeSelf, "repeated conversation shows only completed-stage goodbye"); yield return Close(craft);
        Fill(inv, 3, 20);
        Check(buy.TryExecute(merchant, player) && inv.GetQuantity("gold_coin") == 30 && inv.GetQuantity(Gem) == 1, "normal 30-coin payment always frees a slot because coin stack cap is 20");
        // Exercise the reusable capacity guard with a smaller configured price that does not free a slot.
        var recipe = merchant.GetComponent<DialogueRecipeExchangeEffect>();
        var originalCosts = (InventoryItemCost[])Get(recipe, "costs");
        Set(recipe, "costs", new[] { new InventoryItemCost { item = originalCosts[0].item, quantity = 1 } });
        Fill(inv, 3, 20);
        Check(!buy.TryExecute(merchant, player) && inv.GetQuantity("gold_coin") == 60, "full bag capacity guard rejects without payment when no slot will be freed");
        Set(recipe, "costs", originalCosts);
        Fill(inv, 2, 15);
        Check(buy.TryExecute(merchant, player) && inv.GetQuantity(Gem) == 1 && inv.GetQuantity("gold_coin") == 0, "exact payment can free slots in a full bag");
        inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20); inv.SetSlot(1, "gold_coin", 20); inv.SetSlot(2, "gold_coin", 20);
        Check(buy.TryExecute(merchant, player) && buy.TryExecute(merchant, player) && inv.GetQuantity(Gem) == 2, "repeat purchases work without world stock or duplicate quest rewards");
        Check(!deliver.TryExecute(craft, player) && inv.GetQuantity(Gem) == 2, "completed craftsman will not consume spare gems");
        inv.ClearAll(); inv.SetSlot(0, "gold_coin", 20); inv.SetSlot(1, "gold_coin", 10);
        yield return Open(merchant, player); Select(1); yield return null; inv.ClearAll(); Select(1); yield return null;
        Check(Node == "gold_price" && inv.GetQuantity(Gem) == 0 && !Buttons[1].interactable, "stale wallet state revalidated when selecting"); yield return Close(merchant);
        yield return Open(merchant, player); journal.ApplySaveState(new QuestJournalManager.SaveState()); Select(1); yield return null;
        Check(Node == "start" && !Buttons[1].gameObject.activeSelf, "stale hidden quest option cannot bypass restored stage"); yield return Close(merchant);
        Object.Destroy(playerObject); Object.Destroy(craft.gameObject); Object.Destroy(merchant.gameObject); Object.Destroy(inventoryObject); yield return null;
    }
    static void Preview(Sprite sprite)
    {
        var renderer = new GameObject("Golden Gem Preview").AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.gameObject.layer = 30;
        var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = .53f; camera.cullingMask = 1 << 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .10f, .14f, 1);
        var rt = RenderTexture.GetTemporary(320, 360, 24, RenderTextureFormat.ARGB32); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(320, 360, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 320, 360), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../GoldenGem-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(camera.gameObject); Object.Destroy(renderer.gameObject);
    }
}
