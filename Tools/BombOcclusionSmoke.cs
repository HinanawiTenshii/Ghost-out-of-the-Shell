using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BombOcclusionSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static BombPickupItem normal;
    static BombOcclusionSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Set(object obj, string name, object value)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, F | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(obj, value); return; }
        }
        throw new Exception("Missing field " + name);
    }
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "bomb-occlusion-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(BombOcclusionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(BombOcclusionSmoke), false))
            {
                SessionState.SetBool(nameof(BombOcclusionSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 90;
                suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("BOMB_OCCLUSION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { suite = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static ZeldaCharacterData Actor(Vector2 pos)
    {
        var go = Own(new GameObject("Blast target")); go.SetActive(false); go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
        var data = go.AddComponent<ZeldaCharacterData>(); Set(data, "health", 30);
        go.AddComponent<ZeldaFourWayMover>().enabled = false; go.SetActive(true);
        return data;
    }
    static BoxCollider2D Wall(Vector2 pos, bool trigger = false)
    {
        var go = Own(new GameObject("Solid scenery")); go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>(); col.size = new Vector2(.1f, 3f); col.isTrigger = trigger;
        return col;
    }
    static DoorData Door(Vector2 pos, int durability = 1)
    {
        var go = Own(new GameObject("Destructible surface")); go.SetActive(false); go.transform.position = pos;
        var door = go.AddComponent<DoorData>(); Set(door, "durability", durability);
        Set(door, "breakSoundVolume", 0f); Set(door, "investigationRadius", 0f);
        go.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 1.5f); go.SetActive(true); return door;
    }
    static ZeldaAttackHitbox Blast(BombPickupItem template = null, ZeldaCharacterData owner = null)
    {
        var bomb = (template != null ? template : normal).CreateConfiguredPlacedBomb(Vector3.zero, owner);
        Own(bomb.gameObject); bomb.DetonateImmediately();
        return Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
    }
    static void Hit(ZeldaAttackHitbox hit, Component target) => Call(hit, "TryDamage", target.GetComponent<Collider2D>());
    static void Clear()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear();
        foreach (var b in Object.FindObjectsOfType<PlacedBomb>()) Object.DestroyImmediate(b.gameObject);
        foreach (var b in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(b.gameObject);
        foreach (var b in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(b.gameObject);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        normal = Resources.Load<BombPickupItem>("PickupItems/Bombs/BombPickupItem");
        foreach (string item in new[] { "BombPickupItem", "PurpleBombPickupItem", "DecoyBombPickupItem", "ProximityBombPickupItem" })
        {
            var template = Resources.Load<BombPickupItem>("PickupItems/Bombs/" + item);
            Check(template != null, item + " exists");
            var front = Actor(new Vector2(-.7f, 0)); var behind = Actor(new Vector2(.9f, 0));
            var nearDoor = Door(new Vector2(-.9f, .3f)); var farDoor = Door(new Vector2(.9f, .3f));
            var wall = Wall(Vector2.right * .45f); wall.gameObject.layer = LayerMask.NameToLayer("Blocks");
            var hit = Blast(template, front);
            Hit(hit, behind); Hit(hit, farDoor); Hit(hit, nearDoor); Hit(hit, front); Hit(hit, front);
            Check(behind.CurrentHealth == 30 && !farDoor.IsDestroyed, item + " wall protects actor and object");
            Check(front.CurrentHealth == 30 - template.ExplosionAttackPower, item + " exposed owner hit exactly once");
            Check(nearDoor.IsDestroyed, item + " exposed surface breakable");
            Clear(); yield return null;
        }

        // Default-layer indestructible glass must block just as Blocks walls do.
        var glass = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Glass.prefab")));
        glass.transform.position = Vector2.right * .45f; glass.transform.localScale = new Vector3(.1f, 3f, 1);
        Set(glass.GetComponent<DoorData>(), "durability", 999); Set(glass.GetComponent<DoorData>(), "breakSoundVolume", 0f);
        var victim = Actor(new Vector2(.9f, 0)); var rearDoor = Door(new Vector2(.95f, .3f));
        var blast = Blast(); Hit(blast, glass.transform); Hit(blast, rearDoor); Hit(blast, victim);
        Check(!glass.GetComponent<DoorData>().IsDestroyed && !rearDoor.IsDestroyed && victim.CurrentHealth == 30,
            "default-layer unbreakable glass shields rear objects");
        Clear(); yield return null;

        // Freeze occlusion before any callbacks; breaking the first surface cannot leak the same blast.
        var cover = Door(Vector2.right * .45f); victim = Actor(Vector2.right * .9f);
        rearDoor = Door(new Vector2(.9f, .7f)); rearDoor.GetComponent<BoxCollider2D>().size = Vector2.one * .2f;
        blast = Blast();
        Hit(blast, cover); Check(cover.IsDestroyed && !cover.GetComponent<Collider2D>().enabled, "front cover destroyed normally");
        Hit(blast, victim); Hit(blast, rearDoor); Hit(blast, victim);
        Check(victim.CurrentHealth == 30 && !rearDoor.IsDestroyed, "snapshot survives blocker removal and repeated stays");
        Object.DestroyImmediate(blast.gameObject); blast = Blast(); Hit(blast, victim); Hit(blast, rearDoor);
        Check(victim.CurrentHealth == 30 - normal.ExplosionAttackPower && rearDoor.IsDestroyed, "a later bomb reaches the newly open passage");
        Clear(); yield return null;

        // Interaction triggers / noncolliding decorations / characters do not form blast shields.
        Wall(Vector2.right * .3f, true); var disabled = Wall(Vector2.right * .4f); disabled.enabled = false;
        var body = Actor(Vector2.right * .5f); victim = Actor(Vector2.right * .9f);
        blast = Blast(); Hit(blast, body); Hit(blast, victim);
        Check(body.CurrentHealth == 30 - normal.ExplosionAttackPower && victim.CurrentHealth == 30 - normal.ExplosionAttackPower,
            "triggers, disabled solids and bodies do not block");
        Clear(); yield return null;

        var foreignScene = SceneManager.CreateScene("Other floor blockers");
        var foreign = Wall(Vector2.right * .4f); SceneManager.MoveGameObjectToScene(foreign.gameObject, foreignScene);
        victim = Actor(Vector2.right * .9f); blast = Blast(); Hit(blast, victim);
        Check(victim.CurrentHealth == 30 - normal.ExplosionAttackPower, "other loaded floors do not occlude");
        Clear(); yield return null;

        // No fixed buffer truncation with dense scene overlap and child colliders.
        for (int i = 0; i < 80; i++) Wall(new Vector2(.1f + i * .003f, 0), true);
        Wall(Vector2.right * .5f); victim = Actor(Vector2.right * .9f);
        var exposed = Door(Vector2.left * .6f);
        var child = new GameObject("Door child collider"); child.transform.SetParent(exposed.transform, false);
        child.transform.localPosition = Vector2.right * .1f; child.AddComponent<BoxCollider2D>().size = new Vector2(.1f, .3f);
        blast = Blast(); Hit(blast, victim); Hit(blast, exposed);
        Check(victim.CurrentHealth == 30 && exposed.IsDestroyed, "dense overlaps and a target's own child collider");
        Clear(); yield return null;

        // Bomb and battery chain reactions behind a wall are suppressed.
        Wall(Vector2.right * .4f);
        var unlit = Own(Object.Instantiate(normal.gameObject)); unlit.transform.position = new Vector2(.9f, 0);
        var armed = normal.CreateConfiguredPlacedBomb(new Vector2(.85f, .3f), null); Own(armed.gameObject);
        var battery = Own(Object.Instantiate(Resources.Load<GameObject>("PickupItems/BatteryPickupItem")));
        battery.transform.position = new Vector2(.9f, -.3f);
        blast = BlastOnlyNew(); Hit(blast, unlit.transform); Hit(blast, armed); Hit(blast, battery.transform);
        Check(armed.IsArmed && battery.activeSelf && Object.FindObjectsOfType<ZeldaAttackHitbox>().Length == 1,
            "wall blocks armed/unarmed bomb and battery chain detonation");
        Clear(); yield return null;

        // An installed battery must not have its own socket swallow its explosion.
        var socketGo = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/BatterySocket.prefab")));
        socketGo.transform.position = Vector3.zero;
        var socket = socketGo.GetComponent<BatterySocket>(); Set(socket, "batteryInstalled", true);
        victim = Actor(Vector2.left * .9f); var shielded = Actor(Vector2.right * .9f); Wall(Vector2.right * .5f);
        socket.ReceiveAttack(1, null); blast = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
        Hit(blast, victim); Hit(blast, shielded);
        Check(!socket.HasBattery && victim.CurrentHealth == 26 && shielded.CurrentHealth == 30,
            "battery socket explosion ignores its source casing, still respects external walls");
        Clear(); yield return null;

        bool oldStarts = Physics2D.queriesStartInColliders; Physics2D.queriesStartInColliders = false;
        var enclosing = Wall(Vector2.zero); enclosing.size = new Vector2(.2f, 3f);
        victim = Actor(Vector2.right * .9f); blast = Blast(); Hit(blast, victim);
        Check(victim.CurrentHealth == 30, "embedded origin remains occluded when ray start-in-collider queries are off");
        Physics2D.queriesStartInColliders = oldStarts; Clear(); yield return null;

        // Real trigger delivery (not only direct method calls).
        victim = Actor(Vector2.left * .8f); shielded = Actor(Vector2.right * .9f); Wall(Vector2.right * .4f);
        blast = Blast(); var oldSimulation = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
        Physics2D.Simulate(.02f); Physics2D.Simulate(.02f); Physics2D.simulationMode = oldSimulation;
        Check(victim.CurrentHealth == 30 - normal.ExplosionAttackPower && shielded.CurrentHealth == 30,
            "physics enter/stay applies exposed-only damage once");
        Clear(); yield return null;
    }
    static ZeldaAttackHitbox BlastOnlyNew()
    {
        var source = normal.CreateConfiguredPlacedBomb(Vector3.zero, null); Own(source.gameObject);
        source.DetonateImmediately(); return Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
    }
}
