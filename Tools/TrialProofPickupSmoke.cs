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
public static class TrialProofPickupSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static TrialProofPickupSmoke() { EditorApplication.update += Tick; }
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
        PlayerSettings.productName = "trial-proof-pickup-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(TrialProofPickupSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TrialProofPickupSmoke), false))
            {
                SessionState.SetBool(nameof(TrialProofPickupSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 60;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("TRIAL_PROOF_PICKUP_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        const string id = TrialProofPickupItem.DefaultItemId;
        var prefab = Resources.Load<GameObject>("PickupItems/TrialProofPickupItem");
        Check(prefab != null, "prefab available from Resources");
        var template = prefab.GetComponent<TrialProofPickupItem>();
        Check(template.ItemName == "试炼证明" && template.ItemId == id, "name and stable item type");
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
        var item = Object.Instantiate(prefab).GetComponent<TrialProofPickupItem>();
        Check(item.GetComponent<Collider2D>().isTrigger, "nonblocking pickup");
        Check(!item.ExecuteUse(actor) && !item.CanBeStoredInCardboardBox(actor), "no direct-use effect or box use");
        var visual = item.GetComponent<TrialProofPickupItemVisual>();
        var rows = (string[])typeof(TrialProofPickupItemVisual).GetMethod("GetPixelRows", F).Invoke(visual, null);
        Check(rows.Length == 24 && rows.All(row => row.Length == 20), "valid pixel dimensions");
        Check(rows.All(row => Enumerable.Range(0, 10).All(x => (row[x] == '.') == (row[19-x] == '.'))), "symmetric ornament silhouette");
        Check(visual.InventoryIcon != null && visual.InventoryIcon.texture.filterMode == FilterMode.Point, "pixel inventory icon");
        Check(item.GetComponent<SpriteRenderer>().sprite == visual.InventoryIcon && visual.InventoryTint == Color.white, "world and inventory share embedded palette");
        Preview(visual.InventoryIcon);
        string instance = item.UniqueInstanceId;
        var second = Object.Instantiate(prefab).GetComponent<TrialProofPickupItem>();
        Check(instance != second.UniqueInstanceId, "independent copies have independent identity");
        Object.Destroy(second.gameObject);
        Check(item.TryPickUp() && inv.GetQuantity(id) == 1 && inv.ContainsItemInstance(instance), "pickup stores quest identity");
        Check(!item.TryPickUp(), "same-frame duplicate pickup prevented");
        CloseDescription();
        Check(!inv.TryUseSelectedItem() && inv.GetQuantity(id) == 1, "R cannot consume or place task item");
        yield return null;
        Check(inv.TryDropSelectedItem() && inv.GetQuantity(id) == 0, "Q drops selected task item");
        var dropped = Object.FindObjectsOfType<TrialProofPickupItem>().Single();
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
        var configured = configuredObject.GetComponent<TrialProofPickupItem>();
        Set(configured, "persistentIdentity", identity); configuredObject.SetActive(true);
        Check(configured.TryPickUp() && inv.ContainsItemInstance(identity.StableInstanceId), "optional authored quest identity supported");
        CloseDescription();
        var station = new GameObject("Submission Test").AddComponent<SpecificItemSubmissionStation>();
        station.enabled = false; Set(station, "requiredItemIdentity1", identity);
        Check(station.TrySubmitOneAvailableItem() && station.SubmittedItemCount == 1 && inv.GetQuantity(id) == 0, "existing quest station accepts and consumes configured trial proof");
        Check(!station.TrySubmitOneAvailableItem(), "quest item cannot be submitted twice");
        Object.Destroy(station.gameObject); Object.Destroy(identity);
        yield return null;
    }
    static void CloseDescription() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1; }
    static void Preview(Sprite icon)
    {
        var rt = RenderTexture.GetTemporary(240, 288, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Graphics.Blit(icon.texture, rt); RenderTexture.active = rt;
        var image = new Texture2D(240, 288, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 240, 288), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../TrialProof-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
    }
}
