using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Validate the actual saved scene, then invoke the same potion callback used by E.
// No authored scene writes. Run only in the isolated full-project test copy.
[InitializeOnLoad]
public static class Level2InjectorPoolBindingsSmoke
{
    const string Key = "Level2InjectorPoolBindingsSmoke";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static double deadline;
    static Level2InjectorPoolBindingsSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "injector-pool-binding-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        Check(UnityEngine.Object.FindObjectsOfType<LiquidInjector>(true).Length == 2, "exactly two authored injectors");
        VerifyLinks("注液器（左）", "左侧恢复池", "左侧回复池（水流）");
        VerifyLinks("注液器（右）", "右侧回复池", "右侧恢复池（水流）");
        SessionState.SetInt(Key + "Checks", checks);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, F).GetValue(owner);
    static LiquidInjector Injector(string name) => UnityEngine.Object.FindObjectsOfType<LiquidInjector>(true).Single(i => i.name == name);
    static TemporaryCharacterEffectArea[] VerifyLinks(string name, params string[] expected)
    {
        var injector = Injector(name);
        var areas = Get<TemporaryCharacterEffectArea[]>(injector, "linkedAreas");
        Check(areas.Length == 2 && areas.All(a => a != null), name + ": both component references resolved");
        Check(areas.Select(a => a.name).SequenceEqual(expected), name + ": same-side pool and stream assigned");
        Check(areas.All(a => a.gameObject.scene == injector.gameObject.scene && a.AreaCollider != null && a.AreaCollider.isTrigger), name + ": valid scene trigger regions");
        Check(Get<LiquidInjector.InjectionEvent>(injector, "onInjected").GetPersistentEventCount() == 0, name + ": empty invalid event slots removed");
        return areas;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false);
                checks = SessionState.GetInt(Key + "Checks", 0);
                deadline = EditorApplication.timeSinceStartup + 90;
                test = Test();
            }
            if (test == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout waiting for scene");
            if (!test.MoveNext())
            {
                test = null;
                Debug.Log("LEVEL2_INJECTOR_POOL_BINDINGS_PASS checks=" + checks);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static ZeldaCharacterData Actor(string name, Vector3 position, bool controlled)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .25f;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>();
        mover.enabled = controlled;
        if (controlled) { ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.ReceiveControl(Vector2.up); }
        var state = data.CaptureRuntimeState();
        state.health = state.currentHealth = 8;
        data.ApplyRuntimeState(state);
        return data;
    }

    static void Inject(LiquidInjector injector, ZeldaFourWayMover mover, AlchemyPotionKind kind)
    {
        mover.EndItemAttack();
        mover.transform.position = injector.transform.position + Vector3.down;
        var bag = PersistentInventory.Instance;
        bag.ClearAll();
        string itemId = AlchemyRecipeCatalog.GetItemId(kind);
        bag.SetSlot(0, itemId, 1); bag.SelectSlot(0);
        Check(injector.TryInject(), injector.name + ": selected potion injection succeeds");
        Check(bag.GetQuantity(itemId) == 0 && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId) == 1, "one potion consumed and one bottle returned");
        Check(injector.CurrentEffectColor == AlchemyRecipeCatalog.GetColor(kind), "indicator adopts selected potion color");
    }

    static void Step(TemporaryCharacterEffectArea area) => typeof(TemporaryCharacterEffectArea).GetMethod("TickEffects", F).Invoke(area, new object[] { .02f });
    static void VerifyPotionArea(TemporaryCharacterEffectArea area, AlchemyPotionKind kind)
    {
        Check(area.AppliesPotion && area.PotionKind == kind, area.name + ": correct injected potion");
        Check(area.CaptureEffectSettings().bubbleColor == AlchemyRecipeCatalog.GetColor(kind), area.name + ": matching bubble color");
        var probe = Actor("Pool effect probe", area.AreaCollider.bounds.center, false);
        Step(area);
        Check(CharacterPotionBuffs.Has(probe, kind), area.name + ": entering character receives buff");
        var buff = probe.GetComponent<CharacterPotionBuffs>();
        Check(buff.IsAreaPotionFrozen && Mathf.Abs(buff.Remaining(kind) - 2f * CharacterPotionBuffs.Duration(kind)) < .05f, area.name + ": doubled duration held while inside");
        probe.transform.position = area.AreaCollider.bounds.max + new Vector3(100, 100, 0);
        Step(area);
        Check(!buff.IsAreaPotionFrozen && buff.Remaining(kind) > 0f, area.name + ": countdown released on exit");
        probe.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(probe.gameObject);
    }

    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked || CharacterPotionBuffs.IsRestoringWorld || GameSaveSystem.IsPreparingSceneSave)
            yield return null;
        // Avoid distance loading fixtures while testing the actual authored regions.
        foreach (var streaming in UnityEngine.Object.FindObjectsOfType<CameraVisionObjectStreaming>()) streaming.enabled = false;
        var left = VerifyLinks("注液器（左）", "左侧恢复池", "左侧回复池（水流）");
        var right = VerifyLinks("注液器（右）", "右侧回复池", "右侧恢复池（水流）");
        Check(left.Concat(right).Distinct().Count() == 4, "no cross-linked or duplicate regions");
        Check(left.Concat(right).All(a => a.CaptureEffectSettings().restoreHealth && !a.AppliesPotion), "initial healing preserved");
        var mover = Actor("Injection operator", Injector("注液器（左）").transform.position, true).GetComponent<ZeldaFourWayMover>();
        Inject(Injector("注液器（左）"), mover, AlchemyPotionKind.Berserk);
        Check(right.All(a => a.CaptureEffectSettings().restoreHealth && !a.AppliesPotion), "left injection leaves right regions unchanged");
        Inject(Injector("注液器（右）"), mover, AlchemyPotionKind.Veil);
        foreach (var area in left) VerifyPotionArea(area, AlchemyPotionKind.Berserk);
        foreach (var area in right) VerifyPotionArea(area, AlchemyPotionKind.Veil);
        Check(left.All(a => a.PotionKind == AlchemyPotionKind.Berserk), "right injection leaves left potion unchanged");
        yield return null;
    }
}
