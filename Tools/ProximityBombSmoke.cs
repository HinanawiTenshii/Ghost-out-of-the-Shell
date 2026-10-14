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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ProximityBombSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int frame = -1, checks;
    static IEnumerator suite;
    static double deadline;
    static readonly List<GameObject> owned = new List<GameObject>();
    static BombPickupItem proximity, normal, decoy;
    static ProximityBombSmoke() { EditorApplication.update += Tick; }
    static void Check(bool b, string label) { checks++; if (!b) throw new Exception(label); }
    static FieldInfo Field(object obj, string name)
    {
        for (var t = obj.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception("Missing field: " + name);
    }
    static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "proximity-bomb-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(ProximityBombSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(ProximityBombSmoke), false))
            {
                SessionState.SetBool(nameof(ProximityBombSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 90; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Proximity tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("PROXIMITY_BOMB_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static ZeldaCharacterData Actor(string name, Vector2 pos, bool ghost = false)
    {
        var go = Own(new GameObject(name)); go.SetActive(false); go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
        var data = ghost ? (ZeldaCharacterData)go.AddComponent<GhostZeldaCharacterData>() : go.AddComponent<ZeldaCharacterData>();
        Set(data, "health", 20); Set(data, "skillValue", 6);
        go.AddComponent<ZeldaFourWayMover>().enabled = false; go.SetActive(true);
        return data;
    }
    static PlacedBomb Place(ZeldaCharacterData owner, BombPickupItem prefab = null)
    {
        var bomb = (prefab != null ? prefab : proximity).CreateConfiguredPlacedBomb(Vector3.zero, owner);
        Own(bomb.gameObject); Physics2D.SyncTransforms(); return bomb;
    }
    static void Step(PlacedBomb bomb, float dt = .1f) { Physics2D.SyncTransforms(); bomb.Advance(dt); }
    static void Clear()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
        foreach (var b in Object.FindObjectsOfType<PlacedBomb>()) Object.DestroyImmediate(b.gameObject);
        foreach (var v in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(v.gameObject);
        foreach (var h in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(h.gameObject);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        proximity = Resources.Load<GameObject>("PickupItems/Bombs/ProximityBombPickupItem").GetComponent<BombPickupItem>();
        normal = Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem").GetComponent<BombPickupItem>();
        decoy = Resources.Load<GameObject>("PickupItems/Bombs/DecoyBombPickupItem").GetComponent<BombPickupItem>();
        Check(proximity.IsProximity && !proximity.IsDecoy && !normal.IsProximity && !decoy.IsProximity, "only new prefab has sensor mode");
        Check(proximity.ExplosionSize == Vector2.one * 3.6f, "3.6 by 3.6 explosion size");
        foreach (var name in new[] { "explosionAttackPower", "maxStackSize", "requiredUseSkill", "investigationRadius", "explosionHitboxDuration", "explosionSoundVolume", "explosionSoundPitch", "explosionSoundSpatialBlend", "explosionSoundMaxDistance", "investigationPulseDuration", "placedBombSize", "pickupDistance", "droppedPickupDelay" })
            Check(Get(proximity, name).Equals(Get(normal, name)), "ordinary bomb data retained: " + name);
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == "proximity_bomb") == 1, "unique resource/save identity");
        Preview();
        var owner = Actor("Original user", Vector2.zero);
        var bomb = Place(owner);
        Step(bomb, 3600); Check(bomb.IsArmed, "no timed fuse; owner never triggers");
        Check(CameraVisionStreamingRegion.Intersects(bomb.gameObject.scene, new Bounds(new Vector3(6, 0), Vector3.one)), "attraction region remains loaded");
        var ghost = Actor("Ghost", Vector2.zero, true);
        var dead = Actor("Dead body", Vector2.zero); Set(dead, "isDead", true);
        var inactive = Actor("Unloaded body", Vector2.zero); inactive.gameObject.SetActive(false);
        var otherScene = SceneManager.CreateScene("Proximity unrelated scene");
        var foreign = Actor("Other floor", Vector2.zero); SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        for (int i = 0; i < 48; i++) Own(new GameObject("Non-character overlap")).AddComponent<BoxCollider2D>().isTrigger = true;
        Step(bomb); Check(bomb.IsArmed, "scenery, ghosts, dead/inactive characters and other scenes ignored");
        var target = Actor("Unrelated live body", new Vector2(2.1f, 0));
        var vision = Own(new GameObject("Child awareness trigger")); vision.transform.SetParent(target.transform, false);
        vision.AddComponent<CircleCollider2D>().radius = 4; vision.GetComponent<Collider2D>().isTrigger = true;
        Step(bomb); Check(bomb.IsArmed, "outer actor's vision trigger does not trigger sensor");
        target.transform.position = new Vector3(1.85f, 0);
        Step(bomb, 0); Check(bomb.IsArmed, "pause does not trigger sensor");
        Step(bomb); Check(!bomb.IsArmed, "body crossing blast boundary triggers despite more than 32 overlaps");
        var hit = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
        Physics2D.SyncTransforms();
        Check(Vector2.Distance(hit.GetComponent<BoxCollider2D>().bounds.size, Vector2.one * 3.6f) < .001f, "damage geometry equals sensor geometry in world units");
        int health = target.CurrentHealth;
        Call(hit, "TryDamage", target.GetComponent<Collider2D>());
        Call(hit, "TryDamage", target.GetComponent<Collider2D>());
        Check(target.CurrentHealth == health - normal.ExplosionAttackPower, "ordinary power damages each target once");
        int userHealth = owner.CurrentHealth; Call(hit, "TryDamage", owner.GetComponent<Collider2D>());
        Check(owner.CurrentHealth == userHealth - normal.ExplosionAttackPower, "owner exempt from trigger, not explosion damage");
        Step(bomb); Check(Object.FindObjectsOfType<ZeldaAttackHitbox>().Length == 1, "no duplicate detonation");
        Clear(); yield return null;

        owner = Actor("Saved user", Vector2.zero); var ownerState = owner.CaptureRuntimeState();
        bomb = Place(owner); var scalar = SavedScalarFields.Capture(bomb, true);
        Object.DestroyImmediate(bomb.gameObject); Object.DestroyImmediate(owner.gameObject);
        owner = Actor("Reloaded user", Vector2.zero); owner.ApplyRuntimeState(ownerState);
        var newPlayer = Actor("New controlled body", Vector2.right * 10);
        newPlayer.GetComponent<ZeldaFourWayMover>().enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(newPlayer.GetComponent<ZeldaFourWayMover>());
        bomb = Place(null); SavedScalarFields.Apply(bomb, scalar); bomb.RestoreSavedOwner(newPlayer);
        Step(bomb, 3600); Check(bomb.IsArmed, "saved identity exempts original body even with mismatched owner reference");
        newPlayer.transform.position = Vector3.zero; Step(bomb);
        Check(!bomb.IsArmed, "new controlled body does not inherit exemption after possession");
        Clear(); yield return null;
        owner = Actor("Bomb regression user", Vector2.zero);
        var timed = Place(owner, normal); Step(timed, normal.FuseDuration + .1f);
        Check(!timed.IsArmed, "ordinary bomb still uses timed fuse");
        var bait = Place(owner, decoy); Step(bait, 3600);
        Check(bait.IsActiveDecoy, "decoy still never expires");
        bomb = Place(owner); int blasts = Object.FindObjectsOfType<ZeldaAttackHitbox>().Length;
        Check(bomb.TryConsumeExplosive(out int power) && power == normal.ExplosionAttackPower && !bomb.IsArmed &&
            Object.FindObjectsOfType<ZeldaAttackHitbox>().Length == blasts, "sandworm swallow removes sensor without ground blast");
        var pickup = Own(Object.Instantiate(proximity.gameObject)).GetComponent<BombPickupItem>();
        pickup.DetonateFromAttack(owner);
        Check(Object.FindObjectsOfType<ZeldaAttackHitbox>().Length == blasts + 1, "unarmed pickup still detonates from damage");
        Clear(); yield return null;

        owner = Actor("Inventory user", Vector2.zero); var mover = owner.GetComponent<ZeldaFourWayMover>(); mover.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var inventoryObject = Own(new GameObject("Sensor Inventory")); inventoryObject.SetActive(false);
        var inv = inventoryObject.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        inventoryObject.SetActive(true); inv.enabled = false; inv.ClearAll();
        Check(inv.TryAddItem(proximity.ItemId, 2, proximity.MaxStackSize) && inv.TryUseSelectedItem(), "R consumes one item and places sensor");
        Check(inv.GetQuantity(proximity.ItemId) == 1 && Object.FindObjectsOfType<PlacedBomb>().Single().IsProximity, "R creates armed sensor runtime");
        Object.DestroyImmediate(Object.FindObjectOfType<PlacedBomb>().gameObject);
        Check(inv.TryDropSelectedItem(), "Q drops pickup");
        yield return null;
        Check(Object.FindObjectsOfType<PlacedBomb>().Length == 0, "Q does not arm");
        var dropped = Object.FindObjectsOfType<BombPickupItem>().Single(p => p.IsProximity);
        Check(dropped.TryPickUp(), "Q pickup can be recovered"); ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 0;
        yield return null;
        var box = Own(Object.Instantiate(Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem"))).GetComponent<CardboardBoxPickupItem>();
        Check(box.TryStoreItem(inv.GetSlot(inv.SelectedSlotIndex), proximity, owner), "proximity bomb fits ordinary box workflow");
        inv.ClearAll(); Call(box, "TickStoredBomb", 3600f);
        Check(box.HasStoredItem && !box.IsDestroyed, "unarmed Q-stored sensor remains safe");
        box.ArmStoredBomb(owner); Physics2D.SyncTransforms(); Call(box, "TickStoredBomb", 3600f);
        Check(!box.IsDestroyed, "R-armed boxed sensor has no countdown and excludes owner");
        var savedBox = SavedScalarFields.Capture(box, true); Object.DestroyImmediate(box.gameObject);
        box = Own(Object.Instantiate(Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem"))).GetComponent<CardboardBoxPickupItem>();
        SavedScalarFields.Apply(box, savedBox); box.RestoreSavedContents(false);
        Physics2D.SyncTransforms(); Call(box, "TickStoredBomb", .1f);
        Check(!box.IsDestroyed, "boxed sensor restores without becoming timed");
        target = Actor("Box sensor target", Vector2.right); Physics2D.SyncTransforms(); Call(box, "TickStoredBomb", .1f);
        Check(box.IsDestroyed && !box.HasStoredItem && Object.FindObjectsOfType<ZeldaAttackHitbox>().Length == 1, "boxed sensor triggers and clears contents once");
        Clear(); yield return null;
    }
    static void Preview()
    {
        var a = Own(Object.Instantiate(normal.gameObject)); a.transform.position = new Vector3(-.65f, 0);
        var b = Own(Object.Instantiate(proximity.gameObject)); b.transform.position = Vector3.zero;
        var c = Place(null); c.transform.position = new Vector3(.65f, 0);
        var cam = Own(new GameObject("Bomb comparison preview")).AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = .42f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.045f, .065f, .13f);
        var rt = new RenderTexture(960, 400, 24); cam.targetTexture = rt; cam.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var png = new Texture2D(960, 400, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 960, 400), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../ProximityBomb-preview.png"), png.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = old; Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
        Clear();
    }
}
