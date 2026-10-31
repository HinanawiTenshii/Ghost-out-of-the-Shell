using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class InteractionObstructionSmoke
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ZeldaFourWayMover actor;
    static InteractionObstructionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "interaction-obstruction-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(InteractionObstructionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(InteractionObstructionSmoke), false))
            { SessionState.SetBool(nameof(InteractionObstructionSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 100; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Interaction test timeout");
            if (suite.MoveNext()) return;
            suite = null; Clear(); Debug.Log("INTERACTION_OBSTRUCTION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); Debug.Log("INTERACTION_PASS " + label); }
    static GameObject Go(string name, Vector2 position = default)
    { var go = new GameObject(name); go.transform.position = position; owned.Add(go); return go; }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static void ClearArbiter()
    {
        ((IList)typeof(ZeldaInteractionArbiter).GetField("Offers", F).GetValue(null)).Clear();
        ((IList)typeof(ZeldaInteractionArbiter).GetField("Requests", F).GetValue(null)).Clear();
    }
    static void Clear()
    {
        ClearArbiter();
        for (int i = owned.Count-1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); Time.timeScale = 1;
    }
    static void Setup()
    {
        Clear();
        var go = Go("Actor"); go.AddComponent<SwordZeldaCharacterData>(); actor = go.AddComponent<ZeldaFourWayMover>();
        var body = go.GetComponent<BoxCollider2D>(); body.offset = new Vector2(0,.25f); body.size = new Vector2(.3f,.5f);
        actor.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(actor);
    }
    static BoxCollider2D Box(string name, Vector2 position, Vector2 size, string layer = "Default", bool trigger = false)
    {
        var go = Go(name, position); go.layer = LayerMask.NameToLayer(layer);
        var box = go.AddComponent<BoxCollider2D>(); box.size = size; box.isTrigger = trigger; Physics2D.SyncTransforms(); return box;
    }
    static bool Select(Component owner, Vector2? point = null)
    {
        ClearArbiter(); bool shown = false;
        ZeldaInteractionArbiter.OfferInteraction(owner, actor, KeyCode.E, point ?? (Vector2)owner.transform.position, v => shown = v);
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", F).Invoke(null, null);
        Check(shown == ZeldaInteractionArbiter.IsSelected(owner, actor, KeyCode.E), "prompt agrees with selection");
        return shown;
    }
    static bool Execute(Component owner, Action action, Vector2? point = null)
    {
        ZeldaInteractionArbiter.Submit(owner, actor, KeyCode.E, point ?? (Vector2)owner.transform.position, action);
        var requests = (IList)typeof(ZeldaInteractionArbiter).GetField("Requests", F).GetValue(null);
        Check(requests.Count == 1, "one E request queued");
        object request = requests[0]; requests.Clear();
        return (bool)typeof(ZeldaInteractionArbiter).GetMethod("TryExecuteSelectedRequest", F).Invoke(null, new[] { request });
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave) yield return null;
        foreach (string layer in new[] { "Blocks", "Hidden Blocks", "Default", "Visible Non Blocking", "Ignore Raycast" })
        {
            Setup(); var item = Box("Pickup target", new Vector2(1.1f,.25f), new Vector2(.3f,.5f), "Default", true);
            Check(Select(item), "clear item offered: " + layer);
            var wall = Box("Obstacle", new Vector2(.55f,.25f), new Vector2(.08f,2), layer);
            Check(!Select(item), "obstructed E prompt hidden: " + layer);
            int effects = 0; Check(!Execute(item, () => effects++) && effects == 0, "blocked E has no side effects");
            wall.enabled = false; Check(Select(item), "disabled obstacle restores interaction");
            wall.enabled = true; Physics2D.SyncTransforms();
            Check(!Execute(item, () => effects++) && effects == 0, "obstacle appearing after selection prevents commit");
            wall.gameObject.SetActive(false); Check(Select(item) && Execute(item, () => effects++), "inactive obstacle permits E");
            Check(effects == 1, "exactly one interaction committed");
        }
        Setup(); var notice = Go("Solid notice", new Vector2(1.1f,.25f)).AddComponent<SignpostInteraction>();
        var own = notice.gameObject.AddComponent<BoxCollider2D>(); own.size = Vector2.one * .4f;
        var child = Box("Own child", new Vector2(1,.25f), Vector2.one*.2f); child.transform.SetParent(notice.transform, true);
        Physics2D.SyncTransforms(); Check(Select(notice), "notice own solid colliders do not block reading");
        for (int i = 0; i < 70; i++) Box("Interaction trigger", new Vector2(.55f,.25f), Vector2.one*2, "Default", true);
        Check(Select(notice), "many overlapping trigger areas do not cause false obstruction");
        var group = Go("Scene group"); notice.transform.SetParent(group.transform, true);
        var siblingWall = Box("Sibling obstacle", new Vector2(.55f,.25f), new Vector2(.08f,2)); siblingWall.transform.SetParent(group.transform, true);
        Check(Select(notice), "signpost reading ignores sibling furniture");
        siblingWall.gameObject.layer = LayerMask.NameToLayer("Blocks"); Physics2D.SyncTransforms();
        Check(!Select(notice), "shared scene parent does not exempt unrelated obstacles");
        siblingWall.enabled = false;
        Check(Select(notice) && Execute(notice, () => Call(notice,"OpenWindow")) && SignpostInteraction.BlocksInput,
            "real notice reading window opens when clear");
        Call(notice,"CloseWindow"); yield return null;

        Setup(); var doc = Go("Readable text", new Vector2(1.1f,0)).AddComponent<DocumentReader>();
        var obstruction = Box("Glass", new Vector2(.55f,.125f), new Vector2(.08f,2), "Visible Non Blocking");
        var documentGlass = obstruction.gameObject.AddComponent<DoorData>();
        typeof(DoorData).GetField("breakMaterial",F).SetValue(documentGlass, DoorData.BreakMaterial.Glass);
        Check(!Select(doc) && !Execute(doc, () => Call(doc,"OpenDocument")) && !DocumentReader.IsDocumentOpen,
            "glass prevents real document opening");
        obstruction.enabled = false;
        Check(Select(doc) && Execute(doc, () => Call(doc,"OpenDocument")) && DocumentReader.IsDocumentOpen,
            "real document opens after obstacle removal");
        Call(doc,"CloseDocument"); yield return null;

        Setup(); doc = Go("Document across furniture", new Vector2(1.1f,0)).AddComponent<DocumentReader>();
        var furniture = Box("Solid table surrounding document", new Vector2(.65f,.125f), new Vector2(1.4f,1));
        furniture.gameObject.AddComponent<DoorData>(); // Destructible furniture is not a door.
        foreach(string layer in new[]{"Default", "Visible Non Blocking", "Ignore Raycast"})
        {
            furniture.gameObject.layer=LayerMask.NameToLayer(layer);Physics2D.SyncTransforms();
            Check(Select(doc),"reading ignores furniture including overlapping endpoints: "+layer);
        }
        Check(Execute(doc,()=>Call(doc,"OpenDocument"))&&DocumentReader.IsDocumentOpen,"real document opens across solid furniture");
        Call(doc,"CloseDocument");yield return null;
        furniture.gameObject.SetActive(false);
        var hinge=Go("Arbitrarily named door",new Vector2(.55f,.125f)).AddComponent<DoorHingeInteraction>();
        var leaf=Box("Door child",new Vector2(.55f,.125f),new Vector2(.08f,2));leaf.transform.SetParent(hinge.transform,true);
        Check(!Select(doc),"closed hinge door blocks reading on Default");
        leaf.enabled=false;Check(Select(doc),"open door collider releases reading");
        leaf.enabled=true;Physics2D.SyncTransforms();
        Check(!Execute(doc,()=>{throw new Exception("Read through closing door");}),"door closing after prompt blocks final reading request");
        hinge.gameObject.SetActive(false);
        var sliding=Go("Sliding barrier").AddComponent<DoubleSlidingDoor>();sliding.enabled=false;
        var slidingLeaf=Box("Sliding door child",new Vector2(.55f,.125f),new Vector2(.08f,2),"Visible Non Blocking");
        slidingLeaf.transform.SetParent(sliding.transform,true);
        Check(!Select(doc),"sliding door child blocks reading on non-wall layer");
        slidingLeaf.gameObject.SetActive(false);Check(Select(doc),"inactive sliding leaf no longer blocks");
        sliding.gameObject.SetActive(false);
        var wallTrigger=Box("Explicit wall",new Vector2(.55f,.125f),new Vector2(.08f,2),"Hidden Blocks",true);
        Check(!Select(doc),"wall-trigger barrier still blocks reading");
        wallTrigger.gameObject.SetActive(false);

        Setup(); var inventory = PersistentInventory.Instance ?? Go("Inventory").AddComponent<PersistentInventory>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PickupItems/GoldCoinPickupItem.prefab");
        var coinObject = Object.Instantiate(prefab, new Vector3(1.1f,0), Quaternion.identity); owned.Add(coinObject);
        var coin = coinObject.GetComponent<PickupItemBase>();
        var wall2 = Box("Wall", new Vector2(.55f,.125f), new Vector2(.08f,2), "Blocks");
        Check(!Select(coin) && !Execute(coin, () => coin.TryPickUp()) && coin != null,
            "blocked real pickup remains in world");
        wall2.enabled = false;
        Check(Select(coin) && Execute(coin, () => Check(coin.TryPickUp(), "actual inventory pickup succeeds")),
            "unblocked pickup commits through arbiter");
        yield return null;
        Check(coin == null, "successful pickup consumes world item");

        Setup();
        var reachableCoin = Object.Instantiate(prefab, new Vector3(1.1f,0), Quaternion.identity); owned.Add(reachableCoin);
        var tableCoin = reachableCoin.GetComponent<PickupItemBase>();
        var tabletop = Box("Table with item on it", new Vector2(.8f,.125f), new Vector2(1.2f,1));
        foreach (string layer in new[] { "Default", "Visible Non Blocking", "Ignore Raycast" })
        {
            tabletop.gameObject.layer = LayerMask.NameToLayer(layer); Physics2D.SyncTransforms();
            Check(Select(tableCoin), "furniture does not block pickup on layer " + layer);
        }
        var data = Go("D.A.T.A", new Vector2(1.1f,0)).AddComponent<GrowthCollectible>();
        Check(Select(data), "D.A.T.A follows the same furniture pickup rule");
        tabletop.gameObject.SetActive(false);
        var glass = Box("Unrelated name", new Vector2(.55f,.125f), new Vector2(.08f,2));
        var glassData = glass.gameObject.AddComponent<DoorData>();
        typeof(DoorData).GetField("breakMaterial",F).SetValue(glassData, DoorData.BreakMaterial.Glass);
        foreach (int durability in new[] { 2, int.MaxValue })
        {
            typeof(DoorData).GetField("durability",F).SetValue(glassData,durability);
            Check(!Select(tableCoin) && !Select(data), "ordinary/reinforced glass blocks all pickups");
        }
        glass.gameObject.layer = LayerMask.NameToLayer("Visible Non Blocking"); Physics2D.SyncTransforms();
        Check(!Select(tableCoin), "transparent layer glass still blocks pickup");
        glass.enabled = false;
        var childGlass = Box("Glass child collider", new Vector2(.55f,.125f), new Vector2(.08f,2));
        childGlass.transform.SetParent(glass.transform,true);
        Check(!Select(tableCoin), "glass material is found on collider parent");
        childGlass.enabled = false;
        Check(Select(tableCoin), "disabled/broken glass releases pickup");
        childGlass.enabled = true; Physics2D.SyncTransforms();
        Check(!Execute(tableCoin, () => { throw new Exception("blocked pickup committed"); }), "glass appearing after prompt blocks final E request");
        glass.gameObject.SetActive(false);
        Check(Select(tableCoin), "inactive glass releases pickup");
        tabletop.gameObject.SetActive(true); tabletop.gameObject.layer = LayerMask.NameToLayer("Hidden Blocks"); Physics2D.SyncTransforms();
        Check(!Select(tableCoin), "hidden wall blocks pickup even at endpoints");
        tabletop.isTrigger = true; Physics2D.SyncTransforms();
        Check(!Select(tableCoin), "explicit wall trigger still blocks pickup");
        tabletop.gameObject.SetActive(false);
        var ghost = Go("Ghost actor").AddComponent<GhostZeldaCharacterData>();
        actor.enabled = false; actor = ghost.gameObject.AddComponent<ZeldaFourWayMover>(); actor.ReceiveControl(Vector2.right);
        ZeldaRuntimeRegistry.ClaimControlledMover(actor);
        ClearArbiter();
        typeof(PickupItemBase).GetMethod("UpdatePickupPrompt",F).Invoke(tableCoin,new object[] { actor });
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", F).Invoke(null,null);
        Check(!ZeldaInteractionArbiter.IsSelected(tableCoin, actor, KeyCode.E) && !tableCoin.TryPickUp(), "ghost still cannot pick up items");

        Setup(); var blockedTarget = Box("Near target", new Vector2(1.1f,.25f), Vector2.one*.2f, "Default", true);
        var openTarget = Box("Far reachable target", new Vector2(1.2f,1.3f), Vector2.one*.2f, "Default", true);
        Box("Small obstacle", new Vector2(.55f,.25f), new Vector2(.08f,.3f));
        ClearArbiter(); bool nearShown = true, farShown = false;
        ZeldaInteractionArbiter.OfferInteraction(blockedTarget, actor, KeyCode.E, blockedTarget.transform.position, v => nearShown = v);
        ZeldaInteractionArbiter.OfferInteraction(openTarget, actor, KeyCode.E, openTarget.transform.position, v => farShown = v);
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", F).Invoke(null, null);
        Check(!nearShown && farShown, "blocked nearest object does not steal a reachable object's E selection");

        Setup(); var hatch = Go("Trapdoor", new Vector2(1.1f,0)).AddComponent<TrapdoorInteraction>();
        Box("Hatch blocker", new Vector2(.55f,.125f), new Vector2(.08f,2));
        Check(!Select(hatch, actor.transform.position), "hatch's actor-position offer cannot bypass obstruction");
        Setup(); var exit = Go("Transition area", new Vector2(10,0)).AddComponent<SceneTransitionPoint>();
        var area = exit.GetComponent<BoxCollider2D>(); area.isTrigger = true; area.size = new Vector2(22,2);
        Box("Wall away from local exit overlap", new Vector2(5,0), new Vector2(.2f,3));
        Check(Select(exit, actor.transform.position), "wide transition still uses reachable local overlap, not remote pivot");

        Setup(); var door = Go("Door hinge in wall", new Vector2(1.4f,.25f)).AddComponent<DoorHingeInteraction>();
        var panel = Box("Door panel", new Vector2(1.1f,.25f), new Vector2(.2f,1)); panel.transform.SetParent(door.transform,true);
        var renderer = panel.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.one*.5f, 5);
        var behind = Box("Wall behind hinge", new Vector2(1.45f,.25f), new Vector2(.15f,2));
        Check(Select(door), "reach door surface rather than wall-embedded hinge");
        panel.enabled = false;
        Check(Select(door), "open door with disabled collider remains interactable at visible panel");
        var front = Box("Separate obstruction before door", new Vector2(.55f,.25f), new Vector2(.08f,2));
        Check(!Select(door), "open door behind another obstruction remains blocked");
        front.enabled = false;
        bool starts = Physics2D.queriesStartInColliders;
        try
        {
            Physics2D.queriesStartInColliders = false;
            front.transform.position = actor.InteractionBodyCenter; front.enabled = true; Physics2D.SyncTransforms();
            Check(!Select(door), "embedded source blocked even when physics start-in-collider queries disabled");
        }
        finally { Physics2D.queriesStartInColliders = starts; }

        Setup(); actor.transform.position = new Vector2(100,0);
        var puppetBody = Box("Local puppet", new Vector2(0,.25f), new Vector2(.3f,.5f));
        var puppet = puppetBody.gameObject.AddComponent<ClockworkPuppetRuntime>();
        typeof(ClockworkPuppetRuntime).GetField("bodyCollider",F).SetValue(puppet,puppetBody);
        typeof(ClockworkPuppetRuntime).GetField("manualInteractionDistance",F).SetValue(puppet,2f);
        var lever = Go("Puppet lever", new Vector2(.8f,.25f)).AddComponent<LeverData>();
        var leverBody = lever.GetComponent<Collider2D>() ?? lever.gameObject.AddComponent<BoxCollider2D>();
        var args = new object[] { 0f };
        Check(Call(puppet,"FindNearestManualLever",args) == (object)lever, "remote puppet checks reach from itself, not distant host");
        var divider = Box("Puppet obstruction", new Vector2(.4f,.25f), new Vector2(.08f,2));
        Check(Call(puppet,"FindNearestManualLever",args) == null, "puppet cannot operate a lever through obstruction");
        var driver = puppetBody.gameObject.AddComponent<ClockworkPuppetBoxDriver>();
        typeof(ClockworkPuppetBoxDriver).GetField("drivingCollider",F).SetValue(driver,puppetBody);
        Check(Call(driver,"FindNearestManualLever",args) == null, "box-driven puppet also cannot operate through obstruction");
        divider.enabled = false;
        Check(Call(driver,"FindNearestManualLever",args) == (object)lever, "box-driven puppet retains nearby clear interaction");

        Setup(); var dialogueObject = Go("Merchant", new Vector2(1.1f,0)); dialogueObject.AddComponent<SwordZeldaCharacterData>();
        dialogueObject.AddComponent<ZeldaFourWayMover>().enabled = false;
        var dialogue = dialogueObject.AddComponent<CharacterDialogue>();
        Box("Shop counter", new Vector2(.55f,.125f), new Vector2(.1f,2));
        Check(dialogue.CanConverse(actor) && Select(dialogue), "merchant keeps its intentional counter exception in arbiter");
        Clear();
    }
}
