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

// Run only in the disposable validation project, never against player saves.
[InitializeOnLoad]
public static class GoldCoinPickupSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static GoldCoinPickupSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, F).SetValue(owner, value);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated test project required");
        PlayerSettings.productName = "gold-coin-pickup-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(GoldCoinPickupSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(GoldCoinPickupSmoke), false))
            {
                SessionState.SetBool(nameof(GoldCoinPickupSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 60;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Gold coin tests timed out");
            if (suite.MoveNext()) return;
            suite = null;
            Debug.Log("GOLD_COIN_PICKUP_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        const string id = GoldCoinPickupItem.DefaultItemId;
        var prefab = Resources.Load<GameObject>("PickupItems/GoldCoinPickupItem");
        Check(prefab != null, "coin prefab resolves from Resources");
        var template = prefab.GetComponent<GoldCoinPickupItem>();
        Check(template.ItemId == id && template.ItemName == "金币" && template.MaxStackSize == 20, "identity and stack limit");
        Check(!template.CanUseFromInventory && template.RequiredUseSkill == 0, "passive item with zero skill requirement");
        Check(!template.ItemDescription.Contains("注意") && template.ItemDescription.Contains("换取货物"), "description matches request");
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == id) == 1, "save catalog automatically includes coin exactly once");
        var actor = new GameObject("Coin Collector").AddComponent<ZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var root = new GameObject("Coin Test Inventory"); root.SetActive(false);
        var inv = root.AddComponent<PersistentInventory>();
        Set(inv, "saveToPlayerPrefs", false); root.SetActive(true); inv.ClearAll(); inv.enabled = false;
        Vector3 singleCoinScale = Vector3.one;
        for (int i = 0; i < 50; i++)
        {
            var item = Object.Instantiate(prefab).GetComponent<GoldCoinPickupItem>();
            if (i == 0)
            {
                singleCoinScale = item.transform.localScale;
                Check(item.GetComponent<Collider2D>().isTrigger, "pickup never physically blocks characters");
                Check(!item.ExecuteUse(actor), "direct R execution has no effect");
                Preview(item.ItemVisual.InventoryIcon);
            }
            Check(item.TryPickUp(), "world pickup " + i);
            Check(!item.TryPickUp(), "same-frame duplicate pickup prevented " + i);
            ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        }
        Check(inv.GetSlot(0).Quantity == 20 && inv.GetSlot(1).Quantity == 20 && inv.GetSlot(2).Quantity == 10, "fifty coins form 20/20/10 stacks");
        Check(inv.Slots.Where(s => !s.IsEmpty).All(s => s.ItemInstanceId == "" && s.ItemName == "金币"), "fungible stacks retain correct name");
        Check(!inv.TryUseSelectedItem() && inv.GetQuantity(id) == 50, "inventory R neither consumes nor places currency");
        yield return null;
        inv.SelectSlot(0);
        Check(inv.TryDropSelectedItem() && inv.GetQuantity(id) == 30, "Q drops all twenty coins from selected slot");
        Check(inv.GetSlot(0).IsEmpty && inv.GetSlot(1).Quantity == 20 && inv.GetSlot(2).Quantity == 10, "other currency slots are untouched");
        var dropped = Object.FindObjectsOfType<GoldCoinPickupItem>().Single();
        Check(dropped.PickupQuantity == 20 && dropped.transform.localScale == singleCoinScale, "one unchanged-size visual represents twenty coins");
        Check(dropped.ItemName == "金币" && dropped.ItemDescription == template.ItemDescription, "dropped coin retains name and description");
        Check(dropped.TryPickUp() && inv.GetQuantity(id) == 50, "one interaction collects whole pile across available slots");
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        var saved = JsonUtility.ToJson(inv.CaptureRuntimeState());
        inv.ClearAll(); inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(saved));
        Check(inv.GetQuantity(id) == 50 && inv.Slots.All(s => s.Quantity <= 20), "disk-format inventory snapshot restores stacks");
        // Exercise actual legacy save/load under the isolated test product only.
        Set(inv, "saveKey", "GoldCoinPickupSmoke.Inventory"); Set(inv, "saveToPlayerPrefs", true); inv.Save();
        Set(inv, "saveToPlayerPrefs", false); inv.ClearAll(); Set(inv, "saveToPlayerPrefs", true); inv.Load();
        Check(inv.GetQuantity(id) == 50 && inv.GetSlot(0).ItemName == "金币", "PlayerPrefs load preserves currency and identity");
        Set(inv, "saveToPlayerPrefs", false); PlayerPrefs.DeleteKey("GoldCoinPickupSmoke.Inventory"); PlayerPrefs.Save();
        inv.PreserveForNextSceneLoad();
        typeof(PersistentInventory).GetMethod("OnSceneLoaded", F).Invoke(inv, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        Check(inv.GetQuantity(id) == 50 && inv.ResolveItemPrefab(id) == template, "scene travel retains coins and prefab lookup");
        inv.ClearAll();
        Check(inv.TryAddItem(id, 100) && inv.Slots.All(s => s.Quantity == 20), "generic future shop/reward API also caps stacks at twenty");
        var overflow = Object.Instantiate(prefab).GetComponent<GoldCoinPickupItem>();
        Check(!overflow.TryPickUp() && overflow != null && inv.GetQuantity(id) == 100, "full bag leaves world coin intact");
        Check(inv.TryRemoveItem(id, 1) && overflow.TryPickUp() && inv.GetQuantity(id) == 100, "pickup succeeds once one unit of capacity is freed");
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        inv.ClearAll();
        Check(inv.TryAddUniqueItem("key", "coin-regression-key-a") && inv.TryAddUniqueItem("key", "coin-regression-key-b"), "unique items still accepted");
        Check(inv.GetSlot(0).ItemInstanceId != inv.GetSlot(1).ItemInstanceId && inv.GetSlot(0).Quantity == 1, "keys do not become fungible");
        inv.ClearAll();
        Check(inv.TryAddUniqueItem("red_herb_powder", "coin-regression-herb-a") && inv.TryAddUniqueItem("red_herb_powder", "coin-regression-herb-b") && inv.GetSlot(0).Quantity == 2, "existing herb stacking unchanged");
        Check(inv.TryDropSelectedItem() && inv.GetQuantity("red_herb_powder") == 1, "herbs still drop only one per Q");
        Check(!inv.TryAddUniqueItem("key", "invalid-two-keys", 2) && !inv.TryAddUniqueItem("red_herb_powder", "invalid-two-herbs", 2), "multi-item pickup remains currency-only");
        Check(!inv.TryAddUniqueItem(id, "invalid-zero-coins", 0), "empty currency pickups rejected");
        foreach (var item in Object.FindObjectsOfType<PickupItemBase>()) Object.Destroy(item.gameObject);
        yield return null;
        inv.ClearAll();

        // Insufficient capacity is atomic: no partial pickup or lost remainder.
        Check(inv.TryAddItem(id, 14) && inv.TryDropSelectedItem(), "prepare fourteen-coin world pile");
        dropped = Object.FindObjectsOfType<GoldCoinPickupItem>().Single();
        Check(inv.TryAddItem(id, 95), "fill bag leaving only five units of space");
        Check(!dropped.TryPickUp() && dropped.PickupQuantity == 14 && inv.GetQuantity(id) == 95, "not enough capacity leaves whole pile intact");
        Check(inv.TryRemoveItem(id, 9), "free exactly fourteen spaces");
        int callbacks = 0; bool duplicateSucceeded = false;
        Action onPickup = () => { callbacks++; duplicateSucceeded |= dropped.TryPickUp(); };
        inv.InventoryChanged += onPickup;
        Check(dropped.TryPickUp() && inv.GetQuantity(id) == 100, "single interaction fills all fourteen spaces");
        inv.InventoryChanged -= onPickup;
        Check(callbacks == 1 && !duplicateSucceeded && !dropped.TryPickUp(), "whole-pile pickup emits once and resists reentrant or repeated pickup");
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        yield return null;

        foreach (int quantity in new[] { 1, 7, 20 })
        {
            inv.ClearAll(); inv.SetSlot(0, id, 3); inv.SetSlot(2, id, quantity); inv.SetSlot(4, id, 5);
            inv.SelectSlot(2);
            Check(inv.TryDropSelectedItem(), "middle slot drop quantity " + quantity);
            dropped = Object.FindObjectsOfType<GoldCoinPickupItem>().Single();
            Check(dropped.PickupQuantity == quantity && inv.GetSlot(0).Quantity == 3 && inv.GetSlot(2).IsEmpty && inv.GetSlot(4).Quantity == 5, "drop only selected middle stack " + quantity);
            Check(dropped.TryPickUp() && inv.GetQuantity(id) == 8 + quantity && inv.Slots.All(s => s.Quantity <= 20), "entire pile re-stacks correctly " + quantity);
            ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
            yield return null;
        }

        // Use the real scene snapshot and disk JSON paths, including recreation
        // of a dropped pickup that no longer exists in the scene hierarchy.
        inv.ClearAll();
        var scene = SceneManager.CreateScene("Gold Coin Pile Save"); SceneManager.SetActiveScene(scene);
        Check(inv.TryAddItem(id, 17) && inv.TryDropSelectedItem(), "prepare saved seventeen-coin pile");
        dropped = Object.FindObjectsOfType<GoldCoinPickupItem>().Single();
        var manager = SceneTravelStateManager.GetOrCreate();
        string worldJson = manager.CaptureDiskWorld();
        var snapshots = (IDictionary)manager.GetType().GetField("sceneStates", F).GetValue(manager);
        object memorySnapshot = snapshots[scene.name];
        dropped.GetType().GetMethod("ConfigureDroppedStack", F).Invoke(dropped, new object[] { 1 });
        Check((bool)manager.GetType().GetMethod("RestoreSceneSnapshot", F).Invoke(manager, new[] { (object)scene, memorySnapshot, false }), "cross-scene snapshot restore succeeds");
        Check(dropped.PickupQuantity == 17, "cross-scene snapshot preserves whole-pile quantity");
        string identity = dropped.UniqueInstanceId;
        Object.Destroy(dropped.gameObject); yield return null;
        object diskSnapshot = SnapshotFromDisk(worldJson, scene.name);
        Check((bool)manager.GetType().GetMethod("RestoreSceneSnapshot", F).Invoke(manager, new[] { (object)scene, diskSnapshot, false }), "disk snapshot reconstructs dropped pile");
        dropped = Object.FindObjectsOfType<GoldCoinPickupItem>().Single();
        Check(dropped.PickupQuantity == 17 && dropped.UniqueInstanceId == identity, "disk recreation preserves count and stable identity");
        Check(dropped.TryPickUp() && inv.GetQuantity(id) == 17, "restored pile still collects in one interaction");
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        yield return null;
    }
    static object SnapshotFromDisk(string json, string sceneName)
    {
        var type = typeof(SceneTravelStateManager);
        object world = JsonUtility.FromJson(json, type.GetNestedType("DiskWorld", BindingFlags.NonPublic));
        object saved = null;
        foreach (var entry in (IList)world.GetType().GetField("scenes", F).GetValue(world))
            if ((string)entry.GetType().GetField("name", F).GetValue(entry) == sceneName) saved = entry;
        Check(saved != null, "saved coin scene exists in disk JSON");
        object snapshot = Activator.CreateInstance(type.GetNestedType("SceneState", BindingFlags.NonPublic), true);
        Set(snapshot, "sceneName", sceneName);
        var objects = (IDictionary)snapshot.GetType().GetField("objects", F).GetValue(snapshot);
        foreach (var entry in (IList)saved.GetType().GetField("objects", F).GetValue(saved))
            objects.Add((string)entry.GetType().GetField("path", F).GetValue(entry), entry);
        return snapshot;
    }
    static void Preview(Sprite icon)
    {
        Check(icon != null && icon.texture.width == 16 && icon.texture.height == 16 && icon.texture.filterMode == FilterMode.Point, "pixel icon is shared with world visual");
        var rt = RenderTexture.GetTemporary(384, 384, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        Graphics.Blit(icon.texture, rt); RenderTexture.active = rt;
        var image = new Texture2D(384, 384, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../GoldCoin-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
    }
}
