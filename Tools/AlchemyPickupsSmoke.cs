using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated Unity project only: real inventory, AI, physics, prefab rendering and save-state round trips.
[InitializeOnLoad]
public static class AlchemyPickupsSmoke
{
    const string Pending = "AlchemyPickupsSmoke.Pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator test; static int checks, lastFrame = -1;
    static readonly string[] Names = { "RedHerbPowderPickupItem", "GreenHerbPowderPickupItem", "BlueHerbPowderPickupItem", "GlassBottlePickupItem" };
    static AlchemyPickupsSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext()) { test = null; Debug.Log("ALCHEMY_PICKUPS_SMOKE_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static FieldInfo Field(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { var f = type.GetField(name, Private); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(obj, args);
    static PickupItemBase Prefab(int i) => Resources.Load<GameObject>("PickupItems/" + Names[i]).GetComponent<PickupItemBase>();
    static ThrownGlassBottle Flight(ZeldaCharacterData owner, Vector2 facing, float radius = 5)
    {
        Check(ThrownGlassBottle.Throw(owner, facing, 6, radius, null), "Valid throw accepted");
        foreach (var b in UnityEngine.Object.FindObjectsOfType<ThrownGlassBottle>()) if (!Get<bool>(b, "finished")) return b;
        throw new Exception("No active flight found");
    }
    static ZeldaCharacterAiBase Listener(string name, Vector2 position)
    {
        var go = new GameObject(name); go.transform.position = position;
        var ai = go.AddComponent<ZeldaCharacterAiBase>(); ai.GetComponent<ZeldaFourWayMover>().enabled = false;
        Set(ai, "visionRadius", .05f); Set(ai, "peripheralVisionRadius", .05f);
        ai.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        return ai;
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Alchemy Items A"); SceneManager.SetActiveScene(scene);
        var actor = new GameObject("Bottle thrower"); var owner = actor.AddComponent<ZeldaCharacterData>();
        var mover = actor.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Set(mover, "facingDirection", Vector2.right);
        var inventoryRoot = new GameObject("Alchemy Test Inventory"); inventoryRoot.SetActive(false);
        var inventory = inventoryRoot.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        inventoryRoot.SetActive(true); inventory.enabled = false; inventory.ClearAll();
        var discoveries = new List<string>(); for (int i = 0; i < 4; i++) discoveries.Add(Prefab(i).DescriptionDiscoveryId);
        ItemDescriptionWindow.ApplyDiscoveryHistory(discoveries.ToArray());
        for (int i = 0; i < 4; i++)
        {
            var item = UnityEngine.Object.Instantiate(Prefab(i));
            Check(item.ItemVisual.InventoryIcon != null && item.ItemName != "未命名物品", "Production icon/name " + Names[i]);
            Check(item.TryPickUp(), "E pickup enters real inventory " + Names[i]);
            Check(inventory.Contains(item.ItemId), "Inventory resolves new resource ID");
            Call(mover, "EndItemAttack");
            if (i < 3)
            {
                Check(!item.CanUseFromInventory && !item.ExecuteUse(owner), "Powder cannot consume itself or apply effects");
                Check(!inventory.TryUseSelectedItem() && inventory.GetQuantity(item.ItemId) == 1, "R is inert for powder");
                Check(inventory.TryDropSelectedItem(), "Q drops powder");
                foreach (var drop in UnityEngine.Object.FindObjectsOfType<HerbPowderPickupItem>())
                    if (drop != item && drop.ItemId == item.ItemId) { Check(drop.TryPickUp(), "Dropped powder can be picked up again"); break; }
                Call(mover, "EndItemAttack");
            }
        }
        yield return null; Call(mover, "EndItemAttack");
        Check(!Prefab(3).CanBeStoredInCardboardBox(owner), "R always throws instead of nearby-box storage");
        // Per-instance radius/speed survive inventory JSON and drop/re-pickup.
        inventory.ClearAll(); var custom = UnityEngine.Object.Instantiate(Prefab(3)) as GlassBottlePickupItem;
        Set(custom, "investigationRadius", 7.25f); Set(custom, "throwSpeed", 8f); Check(custom.TryPickUp(), "Pick up configured bottle");
        string json = JsonUtility.ToJson(inventory.CaptureRuntimeState());
        inventory.ClearAll(); inventory.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        var configuredState = inventory.GetSlot(inventory.SelectedSlotIndex).ItemState;
        Check(configuredState.Contains("7.25"), "Inventory JSON retains configured radius");
        Check(inventory.TryDropSelectedItem(), "Q drops intact bottle");
        yield return null; Call(mover, "EndItemAttack");
        GlassBottlePickupItem dropped = null;
        foreach (var item in UnityEngine.Object.FindObjectsOfType<GlassBottlePickupItem>()) if (item != custom) dropped = item;
        Check(dropped != null && Mathf.Abs(dropped.InvestigationRadius - 7.25f) < .001f, "Q restores instance parameters without shattering");
        Check(dropped.TryPickUp(), "Dropped bottle remains collectible"); Call(mover, "EndItemAttack");
        Check(inventory.TryUseSelectedItem() && !inventory.Contains("glass_bottle"), "R consumes exactly one and starts flight");
        ThrownGlassBottle flight = null;
        foreach (var b in UnityEngine.Object.FindObjectsOfType<ThrownGlassBottle>()) if (!Get<bool>(b,"finished")) flight = b;
        Check(flight != null && flight.gameObject.scene == scene && Mathf.Abs(Get<float>(flight,"investigationRadius") - 7.25f) < .001f, "Flight inherits scene and instance radius");
        Check(flight.GetComponent<CameraVisionStreamingRegion>() != null && mover.IsAttacking, "Flight retention and item hand gesture active");
        UnityEngine.Object.Destroy(flight.gameObject); yield return null; Call(mover, "EndItemAttack");

        // Sweeps stop at the earliest physical surface, including a thin wall crossed in one step.
        var wall = new GameObject("Thin physical wall"); wall.transform.position = new Vector3(2,0,0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.04f, 1);
        var near = Listener("Nearby listener", new Vector2(2,1.5f)); var far = Listener("Outside radius", new Vector2(8,2));
        var otherScene = SceneManager.CreateScene("Alchemy Items B");
        var other = Listener("Other-scene listener", new Vector2(2,1)); SceneManager.MoveGameObjectToScene(other.gameObject, otherScene);
        flight = Flight(owner, Vector2.right, 2.5f);
        Check(!(bool)Call(flight, "MoveSwept", new Vector2(10,0)) && Get<bool>(flight,"finished"), "Thin wall collision shatters immediately");
        Check(flight.transform.position.x < 2 && flight.transform.position.x > 1.7f, "Bottle never tunnels through wall");
        Check(near.CurrentState == ZeldaAiState.Search && far.CurrentState == ZeldaAiState.Idle && other.CurrentState == ZeldaAiState.Idle, "Only same-scene AI inside configured radius investigates");
        Check(Vector2.Distance(Get<Vector2>(near,"investigationIncidentPosition"), flight.transform.position) < .001f, "Investigation targets break point, not thrower");
        Check(near.RequiresVisionStreamingRetention, "Investigating AI continues simulating offscreen");
        int bursts = UnityEngine.Object.FindObjectsOfType<GlassBottleShatterVisual>().Length;
        Call(flight, "Shatter", (Collider2D)null); Check(UnityEngine.Object.FindObjectsOfType<GlassBottleShatterVisual>().Length == bursts, "Repeated collision cannot duplicate shatter");
        UnityEngine.Object.Destroy(wall); UnityEngine.Object.Destroy(near.gameObject); UnityEngine.Object.Destroy(far.gameObject); UnityEngine.Object.Destroy(other.gameObject);
        yield return null;

        var victim = new GameObject("Direct damage victim"); victim.transform.position = new Vector3(2,0,0);
        var victimData = victim.AddComponent<ZeldaCharacterData>(); var box = victim.AddComponent<BoxCollider2D>(); box.size = Vector2.one * .5f;
        var child = new GameObject("Second hurt collider"); child.transform.SetParent(victim.transform, false); child.AddComponent<CircleCollider2D>().radius = .2f;
        int before = victimData.CurrentHealth; flight = Flight(owner, Vector2.right, 0);
        Call(flight, "MoveSwept", Vector2.right * 5); Check(victimData.CurrentHealth == before - 1, "AttackPower 1 direct-hit damage exactly once");
        Call(flight, "MoveSwept", Vector2.right); Call(flight, "Shatter", box);
        Check(victimData.CurrentHealth == before - 1, "Multiple victim colliders cannot multiply damage");
        UnityEngine.Object.Destroy(victim); yield return null;

        var zone = new GameObject("Permission trigger"); zone.transform.position = Vector3.right * 2;
        zone.AddComponent<BoxCollider2D>().isTrigger = true;
        flight = Flight(owner, Vector2.right, 0);
        Check((bool)Call(flight,"MoveSwept",Vector2.right * 4), "Nonphysical interaction zones do not shatter bottle");
        Set(flight,"elapsed",5.97f); Call(flight,"FixedUpdate"); Check(!Get<bool>(flight,"finished"), "Still flying before six seconds");
        Call(flight,"FixedUpdate"); Check(Get<bool>(flight,"finished"), "Six-second timeout shatters once");
        UnityEngine.Object.Destroy(zone); yield return null;
        var closeWall = new GameObject("Wall at throwing hand"); closeWall.transform.position = Vector3.right * .35f;
        closeWall.AddComponent<BoxCollider2D>().size = new Vector2(.04f, 1);
        Check(ThrownGlassBottle.Throw(owner, Vector2.right, 6, 0, null), "Throw next to wall still consumes a bottle");
        int active = 0; foreach (var b in UnityEngine.Object.FindObjectsOfType<ThrownGlassBottle>()) if(!Get<bool>(b,"finished")) active++;
        Check(active == 0, "Initial hand sweep cannot skip close wall"); UnityEngine.Object.Destroy(closeWall); yield return null;
        Check(!ThrownGlassBottle.Throw(null, Vector2.right, 6, 5, null) && !ThrownGlassBottle.Throw(owner, Vector2.zero, 6, 5, null), "Invalid throw is nonconsuming");

        // Real GPU preview of the actual inventory/world sprites.
        actor.transform.position = new Vector3(-100, -100, 0);
        var previewObjects = new List<GameObject>();
        for (int i = 0; i < 4; i++)
        {
            var item = UnityEngine.Object.Instantiate(Prefab(i)); item.enabled = false;
            item.transform.position = new Vector3(-1.2f + i * .8f, 10, 0); previewObjects.Add(item.gameObject);
        }
        var camera = new GameObject("Alchemy Items Preview").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = .6f;
        camera.transform.position = new Vector3(0,10,-10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.09f,.15f);
        var rt = new RenderTexture(1120,400,24); camera.targetTexture = rt; camera.aspect = 2.8f;
        camera.Render(); RenderTexture.active = rt; var image = new Texture2D(1120,400,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,1120,400),0,0); image.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath + "/../AlchemyPickups-preview.png",image.EncodeToPNG());
        Check(image.GetPixel(160,200).r > .1f, "Actual GPU preview contains powder");
        foreach(var go in previewObjects) UnityEngine.Object.Destroy(go);
        camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(camera.gameObject);
        yield return null;
    }
}
