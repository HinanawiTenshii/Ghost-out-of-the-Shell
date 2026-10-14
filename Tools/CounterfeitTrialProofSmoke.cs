using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in a disposable validation project, never against player saves.
[InitializeOnLoad]
public static class CounterfeitTrialProofSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static CounterfeitTrialProofSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object owner, string field, object value)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var member = type.GetField(field, F);
            if (member == null) continue;
            member.SetValue(owner, value); return;
        }
        throw new MissingFieldException(field);
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "counterfeit-trial-proof-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(CounterfeitTrialProofSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(CounterfeitTrialProofSmoke), false))
            {
                SessionState.SetBool(nameof(CounterfeitTrialProofSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 60;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("COUNTERFEIT_TRIAL_PROOF_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        const string id = CounterfeitTrialProofPickupItem.DefaultItemId;
        var prefab = Resources.Load<GameObject>("PickupItems/CounterfeitTrialProofPickupItem");
        Check(prefab != null, "prefab available from Resources");
        var template = prefab.GetComponent<CounterfeitTrialProofPickupItem>();
        Check(template.ItemName == "赝品试炼证明" && template.ItemId == id, "name and stable item type");
        Check(template.MaxStackSize == 1 && template.RequiredUseSkill == 0, "single quest item, no skill requirement");
        Check(template.ItemDescription.Contains("金色宝石") && template.ItemDescription.Contains("祭坛"), "authored description");
        Check(template.PersistentIdentity == null && string.IsNullOrEmpty(template.ConfiguredUniqueInstanceId), "generic prefab does not share one quest instance across every copy");
        Check(!template.CanUseFromInventory, "R disabled");
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == id) == 1, "catalog includes one template");
        var actor = new GameObject("Tool Collector").AddComponent<ZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var inventoryObject = new GameObject("Tool Test Inventory"); inventoryObject.SetActive(false);
        var inv = inventoryObject.AddComponent<PersistentInventory>();
        Set(inv, "saveToPlayerPrefs", false); inventoryObject.SetActive(true); inv.ClearAll(); inv.enabled = false;
        Check(inv.ResolveItemPrefab(id) == template, "inventory resolves drop template");
        var item = Object.Instantiate(prefab).GetComponent<CounterfeitTrialProofPickupItem>();
        Check(item.GetComponent<Collider2D>().isTrigger, "nonblocking pickup");
        Check(!item.ExecuteUse(actor) && !item.CanBeStoredInCardboardBox(actor), "no direct-use effect or box use");
        var visual = item.GetComponent<CounterfeitTrialProofPickupItemVisual>();
        var rows = (string[])typeof(CounterfeitTrialProofPickupItemVisual).GetMethod("GetPixelRows", F).Invoke(visual, null);
        Check(rows.Length == 24 && rows.All(row => row.Length == 20), "valid pixel dimensions");
        Check(rows.All(row => row.Count(c => c != '.') <= 8), "crude narrow setting without genuine ornamental wings");
        Check(visual.InventoryIcon != null && visual.InventoryIcon.texture.filterMode == FilterMode.Point, "pixel inventory icon");
        Check(item.GetComponent<SpriteRenderer>().sprite == visual.InventoryIcon && visual.InventoryTint == Color.white, "world and inventory share embedded palette");
        PreviewPair(Resources.Load<GameObject>("PickupItems/TrialProofPickupItem").GetComponent<TrialProofPickupItem>().ItemVisual.InventoryIcon, visual.InventoryIcon);
        string instance = item.UniqueInstanceId;
        var second = Object.Instantiate(prefab).GetComponent<CounterfeitTrialProofPickupItem>();
        Check(instance != second.UniqueInstanceId, "independent copies have independent identity");
        Object.Destroy(second.gameObject);
        Check(item.TryPickUp() && inv.GetQuantity(id) == 1 && inv.ContainsItemInstance(instance), "pickup stores quest identity");
        Check(!item.TryPickUp(), "same-frame duplicate pickup prevented");
        CloseDescription();
        Check(!inv.TryUseSelectedItem() && inv.GetQuantity(id) == 1, "R cannot consume or place task item");
        yield return null;
        Check(inv.TryDropSelectedItem() && inv.GetQuantity(id) == 0, "Q drops selected task item");
        var dropped = Object.FindObjectsOfType<CounterfeitTrialProofPickupItem>().Single();
        Check(dropped.UniqueInstanceId == instance && dropped.ItemName == template.ItemName, "drop preserves instance and name");
        Check(dropped.TryPickUp() && inv.ContainsItemInstance(instance), "dropped item can be collected again");
        CloseDescription();
        string json = JsonUtility.ToJson(inv.CaptureRuntimeState());
        inv.ClearAll(); inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        Check(inv.ContainsItemInstance(instance) && inv.GetQuantity(id) == 1, "JSON save round-trip preserves task identity");
        inv.PreserveForNextSceneLoad();
        typeof(PersistentInventory).GetMethod("OnSceneLoaded", F).Invoke(inv, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        Check(inv.ContainsItemInstance(instance) && inv.ResolveItemPrefab(id) == template, "scene travel retains item and drop template");
        yield return null;
        inv.ClearAll();
        var identity = ScriptableObject.CreateInstance<PickupItemPersistentIdentity>();
        var configuredObject = Object.Instantiate(prefab); configuredObject.SetActive(false);
        var configured = configuredObject.GetComponent<CounterfeitTrialProofPickupItem>();
        Set(configured, "persistentIdentity", identity); configuredObject.SetActive(true);
        Check(configured.TryPickUp() && inv.ContainsItemInstance(identity.StableInstanceId), "optional authored quest identity supported");
        CloseDescription();
        var station = new GameObject("Submission Test").AddComponent<SpecificItemSubmissionStation>();
        station.enabled = false; Set(station, "requiredItemIdentity1", identity);
        Check(station.TrySubmitOneAvailableItem() && station.SubmittedItemCount == 1 && inv.GetQuantity(id) == 0, "existing quest station accepts and consumes configured trial proof");
        Check(!station.TrySubmitOneAvailableItem(), "quest item cannot be submitted twice");
        Object.Destroy(station.gameObject); Object.Destroy(identity);
        yield return null;
        var altarTests = VerifyAltar(inv, actor, mover);
        while (altarTests.MoveNext()) yield return null;
    }
    static void CloseDescription() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1; }
    static IEnumerator VerifyAltar(PersistentInventory inv, ZeldaCharacterData actor, ZeldaFourWayMover mover)
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        const string fake = CounterfeitTrialProofPickupItem.DefaultItemId;
        const string real = TrialProofPickupItem.DefaultItemId;
        var altarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/TrialProofAltar.prefab");
        var altar = Object.Instantiate(altarPrefab).GetComponent<TrialProofAltar>();
        var door = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/DoubleSlidingDoor.prefab")).GetComponent<DoubleSlidingDoor>();
        door.transform.position = new Vector3(8, 0);
        actor.transform.position = new Vector3(0, -1.3f);
        mover.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.EndItemAttack();
        inv.ClearAll(); inv.TryAddItem(fake, 2, 1); inv.SelectSlot(4);
        Check(altar.InteractionMessage == "按[E]供奉赝品试炼证明", "fake proof advertises E offering");
        Check(!altar.TryOffer() && inv.GetQuantity(fake) == 2, "missing door never consumes counterfeit");
        Set(altar, "linkedDoor", door);
        Set(altar, "counterfeitProofPrefab", null); // Existing unpacked scene components have no new serialized reference.
        bool atomic = false, reentered = false;
        Action observed = () => {
            atomic = altar.IsComplete && altar.OfferedItemId == fake && door.IsOpenRequested && inv.GetQuantity(fake) == 1;
            reentered = altar.TryOffer();
        };
        inv.InventoryChanged += observed;
        Check(altar.TryOffer(), "counterfeit alone can complete existing/unpacked altar");
        inv.InventoryChanged -= observed;
        Check(atomic && !reentered, "type, completion, door and payment commit before callbacks without reentry");
        Check(altar.OfferedProofPrefab is CounterfeitTrialProofPickupItem, "correct offered template retained");
        Check(inv.GetQuantity(fake) == 1 && inv.SelectedSlotIndex == 4, "only one fake consumed from any slot");
        mover.EndItemAttack();
        Check(!altar.TryOffer(out var done) && done == "你已证明了自己", "counterfeit completion cannot be offered again");
        float until = Time.time + 1.05f; while (Time.time < until) yield return null;
        Check(door.IsFullyOpen, "counterfeit animates sliding gate like genuine proof");
        Check(altar.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Offered Trial Proof" && r.sprite == altar.OfferedProofPrefab.ItemVisual.InventoryIcon), "altar displays the counterfeit sprite");
        string json = JsonUtility.ToJson(altar.CaptureRuntimeState());
        altar.ApplyRuntimeState(default);
        altar.ApplyRuntimeState(JsonUtility.FromJson<TrialProofAltar.RuntimeState>(json));
        Check(altar.IsComplete && altar.OfferedItemId == fake && door.IsFullyOpen, "JSON restores counterfeit completion and gate");
        var manager = SceneTravelStateManager.Instance;
        if (manager == null) manager = new GameObject("Altar Save Tests").AddComponent<SceneTravelStateManager>();
        var scene = SceneManager.GetActiveScene();
        var snapshot = Call(manager, "CreateSceneSnapshot", scene);
        var states = (IDictionary)snapshot.GetType().GetField("objects", F).GetValue(snapshot);
        foreach (var key in states.Keys.Cast<object>().ToArray())
        { var state = states[key]; states[key] = JsonUtility.FromJson(JsonUtility.ToJson(state), state.GetType()); }
        string disk = manager.CaptureDiskWorld();
        Check(disk.Contains("\"offeredItemId\":\"" + fake + "\""), "disk scene record keeps counterfeit type");
        altar.ApplyRuntimeState(default);
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, snapshot, false), "scene-return restore succeeds");
        Call(manager, "FinalizeRestoredDevices", scene, snapshot);
        Check(altar.IsComplete && altar.OfferedItemId == fake && door.IsFullyOpen, "scene return restores counterfeit offering without payment");
        Check(inv.GetQuantity(fake) == 1, "save restoration never consumes another item");
        altar.gameObject.SetActive(false); altar.gameObject.SetActive(true); yield return null;
        Check(altar.OfferedItemId == fake && !altar.TryOffer(), "distance reload preserves one-time counterfeit state");
        altar.ApplyRuntimeState(JsonUtility.FromJson<TrialProofAltar.RuntimeState>("{\"offered\":true}"));
        Check(altar.OfferedItemId == real && altar.OfferedProofPrefab is TrialProofPickupItem, "old bool-only save remains genuine and complete");
        yield return null;
        Check(altar.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Offered Trial Proof" && r.sprite == altar.OfferedProofPrefab.ItemVisual.InventoryIcon), "type-only state change refreshes displayed proof");
        altar.ApplyRuntimeState(default); mover.EndItemAttack();
        inv.ClearAll(); inv.TryAddItem(real, 1, 1); inv.TryAddItem(fake, 1, 1); inv.SelectSlot(4);
        Check(altar.TryOffer() && altar.OfferedItemId == fake && inv.GetQuantity(real) == 1 && inv.GetQuantity(fake) == 0,
            "unselected mixture spends counterfeit first and preserves genuine item");
        altar.ApplyRuntimeState(default); mover.EndItemAttack();
        inv.ClearAll(); inv.TryAddItem(real, 1, 1); inv.TryAddItem(fake, 1, 1); inv.SelectSlot(0);
        Check(altar.InteractionMessage == "按[E]供奉试炼证明", "explicit selection changes offering prompt");
        Check(altar.TryOffer() && altar.OfferedItemId == real && inv.GetQuantity(real) == 0 && inv.GetQuantity(fake) == 1,
            "explicit genuine selection honored without consuming fake");
        altar.ApplyRuntimeState(default); mover.EndItemAttack();
        inv.ClearAll(); inv.TryAddItem(real, 1, 1); inv.TryAddItem(fake, 1, 1); inv.SelectSlot(1);
        Check(altar.TryOffer() && altar.OfferedItemId == fake && inv.GetQuantity(real) == 1, "explicit counterfeit selection honored");
        altar.ApplyRuntimeState(default); mover.EndItemAttack(); inv.ClearAll();
        Check(!altar.TryOffer(out var missing) && missing == "需要试炼证明", "neither proof keeps original missing-item prompt");
        Object.Destroy(altar.gameObject); Object.Destroy(door.gameObject);
        yield return null;
    }
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static void PreviewPair(Sprite real, Sprite fake)
    {
        var left = new GameObject("Genuine Proof Preview").AddComponent<SpriteRenderer>();
        var right = new GameObject("Counterfeit Proof Preview").AddComponent<SpriteRenderer>();
        left.sprite = real; right.sprite = fake;
        left.gameObject.layer = right.gameObject.layer = 30;
        left.transform.position = new Vector3(-.57f, 0); right.transform.position = new Vector3(.57f, 0);
        var camera = new GameObject("Proof Preview Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = .60f;
        camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.07f, .10f, .14f, 1);
        var rt = RenderTexture.GetTemporary(640, 360, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(640, 360, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../CounterfeitTrialProof-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(camera.gameObject); Object.Destroy(left.gameObject); Object.Destroy(right.gameObject);
    }
}
