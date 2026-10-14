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
public static class PotionShatterSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static readonly List<GameObject> owned = new List<GameObject>();
    static PotionShatterSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "potion-shatter-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(PotionShatterSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(PotionShatterSmoke), false))
            { SessionState.SetBool(nameof(PotionShatterSmoke), false); deadline = EditorApplication.timeSinceStartup + 90; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("POTION_SHATTER_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static ZeldaCharacterData Actor(Vector2 pos, bool controlled = false, bool ghost = false, bool mover = true)
    {
        var go = Own(new GameObject("Splash target")); go.SetActive(false); go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
        var data = ghost ? (ZeldaCharacterData)go.AddComponent<GhostZeldaCharacterData>() : go.AddComponent<ZeldaCharacterData>();
        Set(data, "health", 20); Set(data, "possessionEnergy", 5); Set(data, "skillValue", 6);
        if (mover) go.AddComponent<ZeldaFourWayMover>().enabled = controlled;
        go.SetActive(true);
        if (controlled) ZeldaRuntimeRegistry.ClaimControlledMover(go.GetComponent<ZeldaFourWayMover>());
        return data;
    }
    static PotionPickupItem Potion(AlchemyPotionKind kind)
    {
        var prefab = Resources.Load<GameObject>("PickupItems/Potion-" + kind);
        Check(prefab != null, "resource " + kind);
        return Own(Object.Instantiate(prefab)).GetComponent<PotionPickupItem>();
    }
    static void Clear()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
        foreach (var v in Object.FindObjectsOfType<PotionSplashVisual>()) Object.DestroyImmediate(v.gameObject);
        foreach (var v in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(v.gameObject);
        foreach (var v in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(v.gameObject);
        foreach (var v in Object.FindObjectsOfType<ThrownGlassBottle>()) Object.DestroyImmediate(v.gameObject);
        foreach (var v in Object.FindObjectsOfType<DisposableHandgunBullet>()) Object.DestroyImmediate(v.gameObject);
        if (PlayerGrowthAttributes.Instance != null) PlayerGrowthAttributes.Instance.ApplyRuntimeState(default);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0f;
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            var target = Actor(Vector2.right);
            var second = Actor(Vector2.up, mover: false);
            var outside = Actor(new Vector2(4.01f, 0));
            var potion = Potion(kind); Check(potion.SplashRadius == 4, "world radius four " + kind);
            potion.ReceiveAttack(0, null); potion.ReceiveAttack(99, null);
            Check(!potion.gameObject.activeSelf && !potion.TryPickUp(), "immediate consumption + pickup latch " + kind);
            Check(Object.FindObjectsOfType<PotionSplashVisual>().Length == 1, "one splash " + kind);
            Check(kind == AlchemyPotionKind.Death ? target.IsDead && second.IsDead :
                CharacterPotionBuffs.Has(target, kind) && CharacterPotionBuffs.Has(second, kind), "all NPC body effects " + kind);
            Check(!outside.IsDead && !CharacterPotionBuffs.Has(outside, kind), "outside center excluded " + kind);
            if (kind != AlchemyPotionKind.Death)
                Check(Mathf.Abs(target.GetComponent<CharacterPotionBuffs>().Remaining(kind) - AlchemyRecipeCatalog.GetDuration(kind)) < .01f,
                    "ordinary duration, not doubled area " + kind);
            var fx = Object.FindObjectOfType<PotionSplashVisual>();
            Check(CameraVisionStreamingRegion.Intersects(fx.gameObject.scene, new Bounds(new Vector3(4, 0), Vector3.zero)), "stream retention radius");
            Check(fx.GetComponent<AudioSource>().clip != null && !fx.GetComponent<AudioSource>().loop, "one-shot imported sound");
            for (int sample = 0; sample <= 10; sample++)
            {
                Call(fx, "WriteFrame", sample * PotionSplashVisual.Duration / 10f);
                Check(fx.GetComponent<MeshFilter>().sharedMesh.vertices.All(v => ((Vector2)v).magnitude <= 4.002f), "VFX never exceeds radius");
            }
            Clear(); yield return null;
        }
        // No fixed-cap physics query, child colliders never apply the heal multiple times.
        var crowd = new List<ZeldaCharacterData>();
        for (int i = 0; i < 90; i++)
        {
            var c = Actor(new Vector2((i % 10 - 5) * .2f, (i / 10 - 4) * .2f));
            Set(c, "currentHealth", 1); c.gameObject.AddComponent<CircleCollider2D>().radius = .05f; crowd.Add(c);
        }
        var edge = Actor(new Vector2(4, 0)); Set(edge, "currentHealth", 1);
        var ghost = Actor(Vector2.zero, ghost: true);
        var adjacentScene = SceneManager.CreateScene("Unrelated splash scene");
        var unrelated = Actor(Vector2.zero); SceneManager.MoveGameObjectToScene(unrelated.gameObject, adjacentScene);
        Set(unrelated, "currentHealth", 1);
        var heal = Potion(AlchemyPotionKind.Vitality); heal.ReceiveAttack(1, null);
        Check(crowd.All(c => c.CurrentHealth == 7) && edge.CurrentHealth == 7, "all 90 targets heal six once + exact radius inclusive");
        Check(unrelated.CurrentHealth == 1 && !CharacterPotionBuffs.Has(ghost, AlchemyPotionKind.Vitality), "other scenes and ghosts excluded");
        Clear(); yield return null;

        var npc = Actor(Vector2.zero);
        Check(npc.TrySpendPossessionEnergy(3), "spend NPC base energy");
        CharacterPotionBuffs.TryApplySplash(npc, AlchemyPotionKind.Spirit);
        Check(npc.FinalPossessionEnergy == 5 && npc.MaxPossessionEnergy == 5, "NPC spirit restores four capped");
        CharacterPotionBuffs.TryApplySplash(npc, AlchemyPotionKind.Spirit);
        Check(npc.MaxPossessionEnergy == 7 && npc.FinalPossessionEnergy == 5, "NPC capacity two, no free current energy");
        CharacterPotionBuffs.TryApplySplash(npc, AlchemyPotionKind.Spirit);
        Check(npc.FinalPossessionEnergy == 7, "second splash fills temporary capacity");
        var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(npc.CaptureRuntimeState()));
        var restored = Actor(new Vector2(2, 0)); restored.ApplyRuntimeState(saved);
        Check(restored.FinalPossessionEnergy == 7 && restored.MaxPossessionEnergy == 7, "body spirit current + timer survives JSON restore");
        restored.GetComponent<ZeldaFourWayMover>().enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(restored.GetComponent<ZeldaFourWayMover>());
        Check(restored.FinalPossessionEnergy == 7, "possessing NPC retains energy");
        CharacterPotionBuffs.TryApply(restored, AlchemyPotionKind.Spirit);
        Check(restored.MaxPossessionEnergy == 7, "global + body reserves do not stack to four");
        Check(restored.TrySpendPossessionEnergy(6) && restored.FinalPossessionEnergy == 1, "temporary slots are spendable after possession");
        restored.RestorePossessionEnergy(6); Check(restored.FinalPossessionEnergy == 7, "combined reserves restore without overfilling");
        Set(restored.GetComponent<CharacterPotionBuffs>(), "expiresAt", new float[10]);
        PlayerGrowthAttributes.Instance.ApplyRuntimeState(default);
        Check(restored.MaxPossessionEnergy == 5 && restored.FinalPossessionEnergy == 5, "expiry removes capacity and remaining temporary energy");
        Clear(); yield return null;

        var owner = Actor(new Vector2(-2, 0));
        var p = Potion(AlchemyPotionKind.Might); Physics2D.SyncTransforms();
        Check(FortressBallistaBolt.CanProjectileHit(p.GetComponent<Collider2D>(), owner.transform, p.gameObject.scene), "siege projectiles recognize potion trigger");
        DisposableHandgunBullet.Fire(owner, Vector2.right, 10);
        var bullet = Object.FindObjectOfType<DisposableHandgunBullet>();
        Call(bullet, "MoveSwept", Vector2.right * 3);
        Check(!p.gameObject.activeSelf && CharacterPotionBuffs.Has(owner, AlchemyPotionKind.Might), "handgun swept hit shatters, shooter in radius affected");
        Clear(); yield return null;
        owner = Actor(new Vector2(-2, 0)); p = Potion(AlchemyPotionKind.Veil); Physics2D.SyncTransforms();
        ThrownGlassBottle.Throw(owner, Vector2.right, 10, 0, null);
        Call(Object.FindObjectOfType<ThrownGlassBottle>(), "MoveSwept", Vector2.right * 3);
        Check(!p.gameObject.activeSelf && CharacterPotionBuffs.Has(owner, AlchemyPotionKind.Veil), "thrown bottle contact shatters potion");
        Clear(); yield return null;
        owner = Actor(new Vector2(-2, 0)); p = Potion(AlchemyPotionKind.Berserk);
        var hitRoot = Own(new GameObject("Melee contact", typeof(SpriteRenderer), typeof(BoxCollider2D)));
        var hit = hitRoot.AddComponent<ZeldaAttackHitbox>();
        hit.Configure(.1f, Vector2.one, Color.clear, 0, owner, ZeldaAttackVisualShape.Box);
        Call(hit, "TryDamage", p.GetComponent<Collider2D>());
        Check(!p.gameObject.activeSelf && CharacterPotionBuffs.Has(owner, AlchemyPotionKind.Berserk), "zero-power melee is still an attack");
        Clear(); yield return null;
        owner = Actor(new Vector2(-3, 0)); p = Potion(AlchemyPotionKind.Might);
        var bomb = Resources.Load<BombPickupItem>("PickupItems/Bombs/BombPickupItem").CreateConfiguredPlacedBomb(Vector3.zero, owner);
        bomb.DetonateImmediately();
        var blast = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
        Call(blast, "TryDamage", p.GetComponent<Collider2D>());
        Check(!p.gameObject.activeSelf && Object.FindObjectsOfType<PotionSplashVisual>().Length == 1, "bomb explosion shatters without recursive blast");
        Clear(); yield return null;

        var actor = Actor(Vector2.zero, controlled: true);
        var bagRoot = Own(new GameObject("Potion bag")); bagRoot.SetActive(false);
        var bag = bagRoot.AddComponent<PersistentInventory>(); Set(bag, "saveToPlayerPrefs", false); Set(bag, "persistBetweenScenes", false);
        bagRoot.SetActive(true); bag.enabled = false; bag.ClearAll();
        bag.SetSlot(0, AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might), 1); bag.SelectSlot(0);
        Check(bag.TryUseSelectedItem() && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId) == 1, "drink still returns glass bottle");
        Check(Object.FindObjectsOfType<PotionSplashVisual>().Length == 0, "drinking never splashes");
        actor.GetComponent<ZeldaFourWayMover>().CancelAttackForStun();
        bag.ClearAll(); bag.SetSlot(0, AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Weakness), 1);
        Check(bag.TryDropSelectedItem(), "Q can place potion"); yield return null;
        var dropped = Object.FindObjectsOfType<PotionPickupItem>().Single(); dropped.ReceiveAttack(1, actor);
        Check(CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Weakness) && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId) == 0,
            "Q-placed potion bursts, no bottle refunded");
        Clear(); yield return null;
        Preview(); Clear(); yield return null;
        var fxLife = PotionSplashVisual.Spawn(SceneManager.GetActiveScene(), Vector3.zero, 4, Color.red);
        Time.timeScale = 1;
        float stop = Time.time + PotionSplashVisual.Duration + .2f;
        while (Time.time < stop) yield return null;
        Check(fxLife == null, "VFX/sound/retention clean up after lifetime");
    }
    static void Preview()
    {
        for (int i = 0; i < 10; i++)
        {
            var kind = (AlchemyPotionKind)i;
            var pos = new Vector3((i % 5 - 2) * 10, i < 5 ? 5 : -5);
            var fx = PotionSplashVisual.Spawn(SceneManager.GetActiveScene(), pos, 4, AlchemyRecipeCatalog.GetColor(kind));
            Call(fx, "WriteFrame", .28f);
            var label = Own(new GameObject(kind.ToString())).AddComponent<TextMesh>();
            label.text = kind.ToString(); label.fontSize = 40; label.characterSize = .18f;
            label.anchor = TextAnchor.MiddleCenter; label.transform.position = pos + Vector3.down * 4.6f;
        }
        var camera = Own(new GameObject("Splash preview camera")).AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 10.5f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .10f);
        var rt = new RenderTexture(1920, 800, 24); camera.targetTexture = rt; camera.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var png = new Texture2D(1920, 800, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1920, 800), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../PotionSplash-preview.png"), png.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; Object.DestroyImmediate(png); Object.DestroyImmediate(rt);
    }
}
