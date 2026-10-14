using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copy into Assets/Editor of the isolated validation project only.
[InitializeOnLoad]
public static class CardboardBoxNpcVisionSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int frame = -1, checks;
    static ZeldaCharacterAiBase npc;
    static CardboardBoxNpcVisionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("CardboardBoxNpcVisionSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("CardboardBoxNpcVisionSmoke", false))
            { SessionState.SetBool("CardboardBoxNpcVisionSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Debug.Log("CARDBOARD_NPC_VISION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    static bool Blocked(Transform target = null) => (bool)Call(npc, "IsVisionBlocked", Vector2.zero, Vector2.right, 4f, target);
    static float Ray() => (float)Call(npc, "GetVisibleRayDistance", Vector2.zero, Vector2.right, 4f);
    static void Move(CardboardBoxPickupItem box, Vector3 pos)
    {
        box.transform.position = pos; Set(box, "baseLocalPosition", box.transform.localPosition);
        Physics2D.SyncTransforms();
    }
    static ZeldaFourWayMover Actor(string name, Vector3 position)
    {
        var obj = new GameObject(name); obj.transform.position = position;
        var mover = obj.AddComponent<ZeldaFourWayMover>(); mover.enabled = false;
        return mover;
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var observer = Actor("Observer", Vector3.zero);
        npc = observer.gameObject.AddComponent<ZeldaCharacterAiBase>(); npc.enabled = false;
        Set(npc, "facingDirection", Vector2.right); Set(npc, "visionRadius", 7f);
        var target = Actor("Target behind box", Vector3.right * 4);
        var prefab = Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem");
        Check(prefab != null, "actual cardboard box prefab exists");
        Check(!Blocked() && Mathf.Approximately(Ray(), 4), "baseline clear");
        Check((bool)Call(npc, "CanSeeWorldPoint", new Vector2(4, 0), target.transform), "target originally seen");
        var box = Object.Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        var renderer = box.GetComponent<SpriteRenderer>();
        var collider = box.GetComponent<Collider2D>();
        Physics2D.SyncTransforms();
        Check(Blocked(), "ground box blocks perception");
        Check(!(bool)Call(npc, "CanSeeWorldPoint", new Vector2(4, 0), target.transform), "target hidden behind ground box");
        Check(Ray() > 1.5f && Ray() < 2, "ground box clips vision ray to near surface");
        Check(!Blocked(box.transform), "ignore watched box so it can still be recognized");
        Check((bool)Call(npc, "CanSeeWorldPoint", new Vector2(2, 0), box.transform), "box itself visible");
        Check(collider.isTrigger && box.gameObject.layer == 0 && box.GetComponentsInChildren<Collider2D>().Length == 1,
            "pickup trigger, default layer and collision count unchanged");
        Set(npc, "visionRayCount", 48); Call(npc, "UpdateVisionMesh");
        Vector3[] vertices = Get<Mesh>(npc, "visionMesh").vertices;
        Check(Mathf.Abs(vertices[25].magnitude - Ray()) < .001f, "rendered cone center matches perception surface");

        Set(npc, "visionObstacleLayers", (LayerMask)LayerMask.GetMask("Blocks"));
        Check(Blocked(), "NPC-only box blocking works with Blocks-only obstacle mask");
        Move(box, new Vector3(2, 2, 0)); Check(!Blocked(), "moving box clears old shadow immediately");
        Move(box, new Vector3(2, 0, 0)); renderer.flipX = true;
        Check(Blocked(), "flipped sprite blocks"); renderer.flipX = false;
        box.transform.rotation = Quaternion.Euler(0, 0, 45); box.transform.localScale = new Vector3(1.4f, .5f, 1);
        Check(Blocked(), "rotated and scaled box blocks");
        box.transform.rotation = Quaternion.identity; box.transform.localScale = Vector3.one * .78f;
        renderer.enabled = false; Check(!Blocked(), "hidden renderer does not cast a shadow"); renderer.enabled = true;
        renderer.forceRenderingOff = true; Check(Blocked(), "breathing-overlay renderer handoff still blocks");
        box.gameObject.SetActive(false); Check(!Blocked(), "streamed-out box releases shadow");
        box.gameObject.SetActive(true); Check(Blocked(), "streamed-in box restores shadow");
        box.enabled = false; Check(!Blocked(), "disabled box unregistered"); box.enabled = true;
        Move(box, Vector3.zero); Check(!Blocked(), "observer inside passable box not blinded");
        Move(box, new Vector3(2, 0, 0));
        var second = Object.Instantiate(prefab, new Vector3(1, 0, 0), Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        Check(Ray() < 1 && Ray() > .5f, "nearest of multiple boxes wins");
        second.gameObject.SetActive(false);

        var wall = new GameObject("Existing wall"); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = Vector3.right;
        var wallCollider = wall.AddComponent<BoxCollider2D>(); wallCollider.size = Vector2.one * .2f;
        Physics2D.SyncTransforms(); Check(Mathf.Abs(Ray() - .9f) < .005f, "existing nearer wall still clips cone");
        wallCollider.isTrigger = true; Physics2D.SyncTransforms();
        Check(Ray() > 1.5f, "unrelated triggers remain transparent to NPC");
        wall.SetActive(false);

        var inventoryObj = new GameObject("Test inventory"); inventoryObj.SetActive(false);
        var inventory = inventoryObj.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        inventoryObj.SetActive(true); inventory.ClearAll(); inventory.enabled = false;
        Check(box.TryPickUp(), "actual pickup succeeds"); Check(!Blocked(), "pickup removes occlusion before end of frame");
        var broken = Object.Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        broken.ReceiveAttack(1, null); Check(Blocked(), "damaged box still blocks");
        broken.ReceiveAttack(1, null); Check(!Blocked(), "destroyed box clears occlusion immediately");

        var wearer = Actor("Controlled wearer", new Vector3(2, 0, 0)); wearer.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(wearer);
        var state = wearer.gameObject.AddComponent<CardboardBoxWearState>();
        state.Configure(prefab.GetComponent<CardboardBoxPickupItem>(), wearer);
        var wornVisual = wearer.transform.Find("Worn Cardboard Box Visual");
        Check(wornVisual != null, "actual worn sprite created from item catalog");
        wornVisual.localPosition = new Vector3(0, .08f, 0);
        Check(Blocked(), "worn box shields actors behind wearer");
        Check(!Blocked(wearer.transform), "box does not suppress wearer-target recognition rules");
        Object.DontDestroyOnLoad(wearer.gameObject);
        Check(Blocked(), "persistent controlled wearer still blocks active-scene NPCs");
        wearer.transform.position = Vector3.zero;
        Check(!Blocked(), "inside worn box does not blind observer");
        wearer.transform.position = new Vector3(2, 0, 0);
        state.TryBlockAttack(null); Check(Blocked(), "worn box survives first hit");
        state.TryBlockAttack(null); Check(!Blocked(), "worn box breaks and unregisters on second hit");
        yield return null;

        var ground = Object.Instantiate(prefab, new Vector3(2, 0, 0), Quaternion.identity).GetComponent<CardboardBoxPickupItem>();
        var additiveScene = SceneManager.CreateScene("Other loaded floor");
        SceneManager.MoveGameObjectToScene(ground.gameObject, additiveScene);
        Check(!Blocked(), "other loaded floor's box does not cast cross-scene shadow");
        SceneManager.MoveGameObjectToScene(ground.gameObject, npc.gameObject.scene);
        Check(Blocked(), "returning to same gameplay scene restores shadow");

        wearer.transform.position = Vector3.zero; Physics2D.SyncTransforms();
        var camObj = new GameObject("Player vision", typeof(Camera)); camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0, 0, -10);
        var vision = camObj.AddComponent<CameraCircularVision>(); vision.enabled = false;
        Set(vision, "visionRadius", 7f);
        Check(vision.IsWorldPositionVisible(new Vector3(4, 0, 0), target.transform), "player vision unchanged across box");
        wallCollider.isTrigger = false; wall.SetActive(true); Physics2D.SyncTransforms();
        Check(!vision.IsWorldPositionVisible(new Vector3(4, 0, 0), target.transform), "player vision still blocked by normal wall");
        wall.SetActive(false);
        ground.gameObject.SetActive(false);
        Check(!Blocked() && Mathf.Approximately(Ray(), 4), "final clear state leaves no stale occlusion");
        yield return null;
    }
}
