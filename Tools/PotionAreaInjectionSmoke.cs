using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run in the isolated full-script Unity smoke project only. No authored scene or save-slot writes.
[InitializeOnLoad]
public static class PotionAreaInjectionSmoke
{
    const string Pending = "PotionAreaInjectionSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static PotionAreaInjectionSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("POTION_AREA_INJECTION_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(float actual, float expected, string message, float tolerance = .05f)
    { Check(Mathf.Abs(actual - expected) <= tolerance, message + ": expected=" + expected + ", actual=" + actual); }
    static FieldInfo Field(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags); if (field != null) return field; }
        throw new Exception("Missing field: " + name);
    }
    static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    static string Json(PersistentInventory bag) => JsonUtility.ToJson(bag.CaptureRuntimeState());
    static float Remaining(ZeldaCharacterData actor, AlchemyPotionKind kind)
    {
        var buffs = actor.GetComponent<CharacterPotionBuffs>();
        return buffs != null ? buffs.Remaining(kind) : 0f;
    }
    static void Step(TemporaryCharacterEffectArea area, float elapsed = .02f) => Call(area, "TickEffects", elapsed);
    static void Ready(LiquidInjector injector, ZeldaFourWayMover mover)
    { mover.EndItemAttack(); Set(injector, "nextInteractionTime", 0f); }
    static void Reset(ZeldaCharacterData actor, int current = 8)
    {
        var state = actor.CaptureRuntimeState();
        state.health = 8; state.currentHealth = current; state.possessionEnergy = 6;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        state.combatExpertiseRemaining = 0f; state.potionBuffs = default;
        actor.ApplyRuntimeState(state);
    }
    static ZeldaCharacterData Actor(string name, Vector2 point, bool controlled = false)
    {
        var root = new GameObject(name); root.transform.position = point;
        root.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
        var actor = root.AddComponent<SwordZeldaCharacterData>();
        var mover = root.AddComponent<ZeldaFourWayMover>(); mover.enabled = controlled;
        if (controlled) { ZeldaRuntimeRegistry.ClaimControlledMover(mover); mover.ReceiveControl(Vector2.up); }
        Reset(actor); return actor;
    }
    static TemporaryCharacterEffectArea Area(string name, Vector2 point, AlchemyPotionKind kind)
    {
        var root = new GameObject(name); root.transform.position = point;
        var collider = root.AddComponent<BoxCollider2D>(); collider.size = Vector2.one * 2f; collider.isTrigger = true;
        var area = root.AddComponent<TemporaryCharacterEffectArea>(); Set(area, "animateBubbles", false);
        area.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(kind)); return area;
    }
    static void Leave(ZeldaCharacterData actor, TemporaryCharacterEffectArea area)
    { actor.transform.position += Vector3.up * 4f; Step(area); }
    static void Potion(PersistentInventory bag, AlchemyPotionKind kind)
    { bag.ClearAll(); bag.SetSlot(0, AlchemyRecipeCatalog.GetItemId(kind), 1); bag.SelectSlot(0); }
    static IEnumerator Advance(float seconds)
    {
        Time.timeScale = 1f;
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
        Time.timeScale = 0f;
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Potion Area Injection Smoke"); SceneManager.SetActiveScene(scene);
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        InventoryChecks(scene);
        var durations = DurationChecks(); while (durations.MoveNext()) yield return durations.Current;
        var overlaps = OverlapChecks(); while (overlaps.MoveNext()) yield return overlaps.Current;
        InstantAndConsumedChecks();
        var spirit = SpiritChecks(); while (spirit.MoveNext()) yield return spirit.Current;
        SnapshotCallbackChecks();
        var streaming = StreamingEdgeChecks(); while (streaming.MoveNext()) yield return streaming.Current;
        RestoreBoundaryChecks();
        SaveChecks(scene);
        Time.timeScale = 1f;
    }
    static void InventoryChecks(Scene scene)
    {
        Time.timeScale = 1f;
        var inventoryRoot = new GameObject("Injection inventory"); inventoryRoot.SetActive(false);
        var bag = inventoryRoot.AddComponent<PersistentInventory>();
        Set(bag, "saveToPlayerPrefs", false); Set(bag, "persistBetweenScenes", false);
        inventoryRoot.SetActive(true); bag.enabled = false; bag.ClearAll();
        Check(PersistentInventory.Instance == bag, "Fixture owns inventory singleton");
        var operatorData = Actor("Injection operator", new Vector2(0f, -1.1f), true);
        var mover = operatorData.GetComponent<ZeldaFourWayMover>();
        var root = new GameObject("Potion injector"); var injector = root.AddComponent<LiquidInjector>();
        var a = Area("Injection area A", new Vector2(10, 0), AlchemyPotionKind.Vitality);
        var b = Area("Injection area B", new Vector2(14, 0), AlchemyPotionKind.Vitality);
        var inactive = Area("Injection inactive area", new Vector2(18, 0), AlchemyPotionKind.Vitality);
        inactive.gameObject.SetActive(false);
        var foreign = Area("Foreign-scene area", new Vector2(22, 0), AlchemyPotionKind.Vitality);
        var otherScene = SceneManager.CreateScene("Potion injection foreign scene");
        SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        Set(injector, "linkedAreas", new[] { a, b, inactive, foreign, a, null });
        Check(!injector.ConditionSatisfied, "Prefab-default legacy condition is locked");
        string bottle = GlassBottlePickupItem.DefaultItemId;
        Potion(bag, AlchemyPotionKind.Might);
        int events = 0, inventoryEvents = 0; bool recursive = false;
        Get<LiquidInjector.InjectionEvent>(injector, "onInjected").AddListener(who =>
        { events++; Check(who == operatorData, "Injection callback reports operator"); recursive |= injector.TryInject(); });
        Action inventoryChanged = () =>
        {
            inventoryEvents++; recursive |= injector.TryInject();
            recursive |= bag.TryConsumeSelectedItem(bottle);
            recursive |= bag.TryRemoveItem(bottle);
        };
        bag.InventoryChanged += inventoryChanged;
        // Native key injection is unavailable in batch mode: verify E arbitration and invoke its actual callback.
        ZeldaInteractionArbiter.OfferInteraction(injector, mover, KeyCode.E, root.transform.position, _ => { });
        ZeldaInteractionArbiter.Submit(injector, mover, KeyCode.E, root.transform.position, injector.Inject);
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Check(ZeldaInteractionArbiter.IsSelected(injector, mover, KeyCode.E), "Injector wins shared E arbitration");
        int revision = Get<int>(a, "effectRevision"); injector.Inject();
        bag.InventoryChanged -= inventoryChanged;
        Check(events == 1 && inventoryEvents == 1 && !recursive, "One atomic callback and inventory exchange; recursive use rejected");
        Check(bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might)) == 0 && bag.GetQuantity(bottle) == 1,
            "Selected potion consumed once and one standard glass bottle returned");
        Check(mover.IsUsingItemAttack && !CharacterPotionBuffs.Has(operatorData, AlchemyPotionKind.Might),
            "Injection reuses item gesture without drinking the operator's potion");
        Check(Get<int>(a, "effectRevision") == revision + 1, "Duplicate linked areas receive one application");
        foreach (var area in new[] { a, b, inactive })
        {
            var settings = area.CaptureEffectSettings();
            Check(settings.applyPotion && settings.potionKind == AlchemyPotionKind.Might && !settings.restoreHealth,
                "Every linked area receives potion mode and stops old periodic healing");
            Check(settings.bubbleColor == AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might), "Potion recolors linked bubbles");
        }
        Check(!inactive.gameObject.activeSelf && foreign.CaptureEffectSettings().potionKind == AlchemyPotionKind.Vitality,
            "Inactive target stays inactive; cross-scene target unchanged");
        Check(injector.CurrentEffectColor == AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might), "Injector window matches potion palette");
        Ready(injector, mover); bag.ClearAll();
        string before = Json(bag); var unchanged = JsonUtility.ToJson(a.CaptureEffectSettings());
        Check(!injector.TryInject() && Json(bag) == before && !mover.IsAttacking && events == 1,
            "Empty selection cannot inject, gesture, consume or emit success");
        bag.SetSlot(0, AlchemyRecipeCatalog.RedHerbId, 2); before = Json(bag);
        Check(!injector.TryInject() && Json(bag) == before && JsonUtility.ToJson(a.CaptureEffectSettings()) == unchanged,
            "Non-potion selection leaves inventory and area untouched");
        Potion(bag, AlchemyPotionKind.Veil); Set(injector, "linkedAreas", new[] { foreign, (TemporaryCharacterEffectArea)null });
        before = Json(bag);
        Check(!injector.TryInject() && Json(bag) == before && !mover.IsAttacking, "No valid target never consumes potion or returns free bottle");
        Set(injector, "linkedAreas", new[] { a, b });
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            Ready(injector, mover); Potion(bag, kind);
            Check(injector.TryInject(), "All ten recipes inject despite locked legacy condition: " + kind);
            Check(a.CaptureEffectSettings().potionKind == kind && a.CaptureEffectSettings().bubbleColor == AlchemyRecipeCatalog.GetColor(kind) &&
                injector.CurrentEffectColor == AlchemyRecipeCatalog.GetColor(kind), "Catalog identity and color preserved: " + kind);
            Check(bag.GetQuantity(bottle) == 1 && bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(kind)) == 0,
                "Every recipe exchanges exactly one potion for one bottle: " + kind);
        }
        Ready(injector, mover); Potion(bag, AlchemyPotionKind.Might); bag.SetSlot(1, bottle, 2);
        Check(injector.TryInject() && bag.GetQuantity(bottle) == 3 && bag.GetSlot(1).Quantity == 3 && bag.GetSlot(0).IsEmpty,
            "Injection return uses compatible existing two-bottle stack without exceeding three");
        Ready(injector, mover); bag.ClearAll();
        for (int i = 0; i < bag.SlotCount; i++) bag.SetSlot(i, AlchemyRecipeCatalog.RedHerbId, 3);
        bag.SetSlot(2, AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Veil), 1); bag.SelectSlot(2);
        Check(injector.TryInject() && bag.GetSlot(2).ItemId == bottle && bag.GetSlot(2).Quantity == 1,
            "Full inventory can return bottle into just-consumed potion slot");
        Ready(injector, mover); bag.SetSlot(2, AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Berserk), 1); bag.SelectSlot(2);
        Set(bag.GetSlot(2), "quantity", 2); before = Json(bag);
        unchanged = JsonUtility.ToJson(a.CaptureEffectSettings()); int beforeEvents = events;
        Check(!injector.TryInject() && Json(bag) == before && !mover.IsAttacking && events == beforeEvents &&
            JsonUtility.ToJson(a.CaptureEffectSettings()) == unchanged,
            "Malformed stacked potion with no bottle space fails atomically before changing area");
        Ready(injector, mover); mover.enabled = false; root.SetActive(false);
        Time.timeScale = 0f;
    }
    static IEnumerator DurationChecks()
    {
        var actors = new List<ZeldaCharacterData>(); var areas = new List<TemporaryCharacterEffectArea>();
        var kinds = new List<AlchemyPotionKind>();
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            if (kind == AlchemyPotionKind.Death || kind == AlchemyPotionKind.Spirit) continue;
            var point = new Vector2(10f * kinds.Count, 20f);
            var actor = Actor("Doubled " + kind, point); var area = Area("Doubled area " + kind, point, kind);
            Step(area); actors.Add(actor); areas.Add(area); kinds.Add(kind);
            Near(Remaining(actor, kind), CharacterPotionBuffs.Duration(kind) * 2f, "Area grants exactly double duration: " + kind);
            float display = actor.GetComponent<CharacterPotionBuffs>().DisplayRemaining(kind, out float displayDuration);
            Near(displayDuration, CharacterPotionBuffs.Duration(kind) * 2f, "HUD uses area duration denominator: " + kind);
            Near(display / displayDuration, 1f, "HUD ring stays full inside: " + kind);
            Check(area.OccupantCount == 1, "Physics query registers occupant: " + kind);
            var roundTrip = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(actor.CaptureRuntimeState()));
            actor.ApplyRuntimeState(roundTrip);
            Near(Remaining(actor, kind), CharacterPotionBuffs.Duration(kind) * 2f, "JSON preserves every doubled duration: " + kind);
        }
        var wait = Advance(.25f); while (wait.MoveNext()) yield return wait.Current;
        for (int i = 0; i < kinds.Count; i++)
        {
            Near(Remaining(actors[i], kinds[i]), CharacterPotionBuffs.Duration(kinds[i]) * 2f, "Inside area timer remains frozen: " + kinds[i]);
            Leave(actors[i], areas[i]);
        }
        wait = Advance(.25f); while (wait.MoveNext()) yield return wait.Current;
        for (int i = 0; i < kinds.Count; i++)
        {
            float full = CharacterPotionBuffs.Duration(kinds[i]) * 2f;
            Check(Remaining(actors[i], kinds[i]) < full - .1f && Remaining(actors[i], kinds[i]) > full - 3f,
                "Leaving starts ordinary countdown: " + kinds[i]);
            actors[i].transform.position = areas[i].transform.position; Step(areas[i]);
            Near(Remaining(actors[i], kinds[i]), full, "Same-kind reentry refreshes full double duration: " + kinds[i]);
            areas[i].enabled = false;
        }
        var disabledRemaining = Remaining(actors[0], kinds[0]);
        wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(Remaining(actors[0], kinds[0]) < disabledRemaining - .08f, "Disabling area releases its timer freeze");
    }
    static IEnumerator OverlapChecks()
    {
        var actor = Actor("Overlapping same-kind subject", new Vector2(0, 40));
        var a = Area("Same-kind first", actor.transform.position, AlchemyPotionKind.Might);
        var b = Area("Same-kind second", actor.transform.position, AlchemyPotionKind.Might);
        Step(a); Step(b);
        a.transform.position += Vector3.left * 8f; Step(a);
        var wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Near(Remaining(actor, AlchemyPotionKind.Might), 60f, "Leaving one same-kind overlap keeps timer frozen in the other");
        Leave(actor, b); Step(a);
        Check(a.OccupantCount == 0 && b.OccupantCount == 0, "Last-exit fixture is outside both same-kind areas");
        wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(Remaining(actor, AlchemyPotionKind.Might) < 59.9f, "Last same-kind exit releases freeze");
        a.enabled = b.enabled = false;

        actor = Actor("Different-kind replacement subject", new Vector2(10, 40));
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Sight);
        a = Area("Older might area", actor.transform.position, AlchemyPotionKind.Might);
        b = Area("Newer berserk area", actor.transform.position, AlchemyPotionKind.Berserk);
        Step(a); Step(b); Step(a);
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might) && CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Berserk) &&
            CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Sight), "Different new area replaces only previous zone-owned buff, retaining drink");
        b.transform.position += Vector3.up * 4f; Step(b); Step(a);
        wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might) && Remaining(actor, AlchemyPotionKind.Berserk) < 44.9f,
            "Exiting newest area does not roll back to or freeze inside older different-kind area");
        a.enabled = b.enabled = false;

        actor = Actor("Same-kind drink provenance subject", new Vector2(20, 40));
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        a = Area("Drink provenance area", actor.transform.position, AlchemyPotionKind.Might); Step(a);
        a.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(AlchemyPotionKind.Berserk)); Step(a);
        Check(CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Might) && CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Berserk),
            "Replacing area preserves ordinary drink even when it had the same kind as old zone");
        Near(Remaining(actor, AlchemyPotionKind.Might), 30f, "Surviving drink retains its ordinary duration");
        a.enabled = false;
    }
    static void InstantAndConsumedChecks()
    {
        var actor = Actor("Entry-only healing subject", new Vector2(0, 60)); Reset(actor, 1);
        var area = Area("Vitality entry-only area", actor.transform.position, AlchemyPotionKind.Vitality);
        // Multiple colliders belonging to one actor must not duplicate an entry.
        var extra = new GameObject("Second body collider"); extra.transform.SetParent(actor.transform, false); extra.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
        Step(area); Check(actor.CurrentHealth == 7 && actor.Health == 8, "Vitality entry restores exactly six once per actor");
        for (int i = 0; i < 20; i++) Step(area, .3f);
        Check(actor.CurrentHealth == 7 && actor.Health == 8, "Remaining inside does not repeat instant healing or add capacity");
        Leave(actor, area); actor.transform.position = area.transform.position; Step(area);
        Check(actor.CurrentHealth == 8 && actor.Health == 8, "Real reentry performs next instant heal");
        area.enabled = false;

        actor = Actor("Consumed veil subject", new Vector2(10, 60));
        area = Area("Veil area", actor.transform.position, AlchemyPotionKind.Veil); Step(area);
        CharacterPotionBuffs.NotifyAction(actor); Step(area, 3f);
        Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Veil), "Consumed Veil stays broken while continuously inside");
        Leave(actor, area); actor.transform.position = area.transform.position; Step(area);
        Near(Remaining(actor, AlchemyPotionKind.Veil), 30f, "Real reentry rearms consumed Veil"); area.enabled = false;

        actor = Actor("Consumed echo subject", new Vector2(20, 60)); Reset(actor, 4);
        area = Area("Echo area", actor.transform.position, AlchemyPotionKind.Echo); Step(area);
        Vector3 anchor = actor.transform.position; actor.transform.position += Vector3.right * .4f; Step(area);
        actor.TakeDamage(100);
        Check(!actor.IsDead && actor.CurrentHealth == 4 && Vector3.Distance(actor.transform.position, anchor) < .05f,
            "Echo recalls entry anchor rather than following occupant each tick");
        Step(area, 3f); Check(!CharacterPotionBuffs.Has(actor, AlchemyPotionKind.Echo), "Consumed Echo does not rearm while inside");
        Leave(actor, area); actor.transform.position = area.transform.position; Step(area);
        Near(Remaining(actor, AlchemyPotionKind.Echo), 240f, "Real reentry rearms Echo with doubled duration"); area.enabled = false;

        actor = Actor("Lethal area subject", new Vector2(30, 60));
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo);
        area = Area("Death area", actor.transform.position, AlchemyPotionKind.Death); Step(area);
        Check(actor.IsDead, "Death area is immediately lethal and bypasses ordinary Echo protection"); area.enabled = false;
        var ghostRoot = new GameObject("Area ghost"); ghostRoot.transform.position = new Vector2(40, 60); ghostRoot.AddComponent<BoxCollider2D>();
        var ghost = ghostRoot.AddComponent<GhostZeldaCharacterData>(); area = Area("Ghost ignored area", ghostRoot.transform.position, AlchemyPotionKind.Might);
        Step(area); Check(area.OccupantCount == 0 && !CharacterPotionBuffs.Has(ghost, AlchemyPotionKind.Might), "Ghosts never gain body area effects"); area.enabled = false;
    }
    static IEnumerator SpiritChecks()
    {
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var actor = Actor("Spirit controlled subject", new Vector2(0, 80), true);
        var area = Area("Spirit player area", actor.transform.position, AlchemyPotionKind.Spirit); Step(area);
        Check(actor.MaxPossessionEnergy == 8 && actor.FinalPossessionEnergy == 6, "Full-energy Spirit area grants two empty reserve capacity");
        Near(growth.PotionEnergyRemaining, 60f, "Spirit capacity duration doubles");
        Near(growth.PotionEnergyDisplayRemaining(out float spiritDisplayDuration), 60f, "Spirit HUD reads frozen remaining");
        Near(spiritDisplayDuration, 60f, "Spirit HUD uses doubled denominator");
        var wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Near(growth.PotionEnergyRemaining, 60f, "Spirit reserve duration frozen in area");
        Leave(actor, area); wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(growth.PotionEnergyRemaining < 59.9f, "Spirit reserve starts countdown on exit");
        string saved = JsonUtility.ToJson(growth.CaptureRuntimeState());
        growth.ApplyRuntimeState(default); growth.ApplyRuntimeState(JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(saved));
        Check(growth.PotionEnergyRemaining > 59f, "Spirit JSON round-trip does not clamp doubled duration to ordinary thirty seconds");
        area.enabled = false;
        growth.ApplyRuntimeState(default); Reset(actor); Check(actor.TrySpendPossessionEnergy(5), "Prepare missing player energy");
        area = Area("Spirit healing-only area", actor.transform.position, AlchemyPotionKind.Spirit); Step(area);
        Check(actor.FinalPossessionEnergy == 5 && actor.MaxPossessionEnergy == 6, "Spirit restores four once on entry");
        Step(area, 10f); Check(actor.FinalPossessionEnergy == 5 && actor.MaxPossessionEnergy == 6, "Spirit does not refill every tick");
        var npc = Actor("NPC Spirit isolation subject", new Vector2(10, 80));
        var npcArea = Area("NPC Spirit area", npc.transform.position, AlchemyPotionKind.Spirit);
        string growthBefore = JsonUtility.ToJson(growth.CaptureRuntimeState()); int playerBefore = actor.FinalPossessionEnergy;
        Step(npcArea); Step(npcArea, 10f);
        Check(JsonUtility.ToJson(growth.CaptureRuntimeState()) == growthBefore && actor.FinalPossessionEnergy == playerBefore,
            "NPC Spirit entry cannot refill or extend player-owned energy");
        Check(!CharacterPotionBuffs.Has(npc, AlchemyPotionKind.Spirit), "NPC cannot own player Spirit reserve");
        actor.GetComponent<ZeldaFourWayMover>().enabled = false; area.enabled = npcArea.enabled = false;
        growth.ApplyRuntimeState(default);
    }
    static void SnapshotCallbackChecks()
    {
        foreach (var kind in new[] { AlchemyPotionKind.Might, AlchemyPotionKind.Echo })
        {
            var point = new Vector2((int)kind * 4f, 120);
            var actor = Actor("Callback snapshot " + kind, point);
            var area = Area("Callback snapshot area " + kind, point, kind);
            ZeldaCharacterData.RuntimeState? first = null;
            Action capture = () => { if (!first.HasValue) first = actor.CaptureRuntimeState(); };
            actor.ValuesChanged += capture;
            Step(area); actor.ValuesChanged -= capture;
            Check(first.HasValue, "Entry exposes normal attribute-change callback: " + kind);
            var saved = first.Value.potionBuffs;
            Check(saved.hasAreaPotion && saved.areaKind == kind && saved.areaFrozen,
                "First ValuesChanged snapshot already has complete area ownership and freeze: " + kind);
            Near(saved.areaRemaining, CharacterPotionBuffs.Duration(kind) * 2f,
                "First ValuesChanged snapshot already has full doubled timer: " + kind);
            if (kind == AlchemyPotionKind.Echo)
                Check(Vector3.Distance(saved.areaEchoPosition, actor.transform.position) < .001f && saved.areaEchoHealth == 8 &&
                    !string.IsNullOrEmpty(saved.areaEchoScene), "First Echo ValuesChanged snapshot already has anchor, health and scene");
            area.enabled = false;
        }
    }
    static IEnumerator StreamingEdgeChecks()
    {
        var actor = Actor("Streamed edge body", new Vector2(1.1f, 140));
        var area = Area("Streaming edge area", new Vector2(0, 140), AlchemyPotionKind.Might);
        int entered = 0; Get<TemporaryCharacterEffectArea.CharacterEvent>(area, "onCharacterEntered").AddListener(_ => entered++);
        Step(area);
        Check(area.OccupantCount == 1 && !area.AreaCollider.OverlapPoint(actor.transform.position),
            "Streaming fixture overlaps by body edge while pivot is outside area");
        var root = new GameObject("Streaming membership fixture");
        var streaming = root.AddComponent<CameraVisionObjectStreaming>();
        Set(streaming, "restoreObjectsWhenDisabled", false); streaming.enabled = false;
        root.GetComponent<Camera>().enabled = false; root.GetComponent<CameraCircularVision>().enabled = false;
        // Register a streaming-owned suspension deterministically; no real camera scan or authored scene changes.
        var managedType = typeof(CameraVisionObjectStreaming).GetNestedType("ManagedObject", BindingFlags.NonPublic);
        var managed = Activator.CreateInstance(managedType, true);
        Set(managed, "gameObject", actor.gameObject); Set(managed, "suspendedByManager", true);
        Get<IDictionary>(streaming, "managedLookup")[actor.gameObject] = managed;
        actor.gameObject.SetActive(false); Step(area);
        var wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(!actor.gameObject.activeSelf && area.OccupantCount == 1 && entered == 1,
            "Streaming suspension keeps edge-contact membership without waking actor or replaying entry");
        Near(Remaining(actor, AlchemyPotionKind.Might), 60f, "Streamed edge-contact body keeps zone timer frozen");
        CameraVisionObjectStreaming.RestoreForGameplay(actor.gameObject); Step(area);
        Check(actor.gameObject.activeSelf && area.OccupantCount == 1 && entered == 1, "Streaming resume does not synthesize reentry");
        Set(managed, "suspendedByManager", true); actor.gameObject.SetActive(false);
        actor.transform.position += Vector3.right * 3f; Step(area);
        Check(area.OccupantCount == 0, "Moving suspended body out invalidates cached edge contact");
        wait = Advance(.2f); while (wait.MoveNext()) yield return wait.Current;
        Check(Remaining(actor, AlchemyPotionKind.Might) < 59.9f, "Moved streamed body counts down after actual exit");
        area.enabled = false; root.SetActive(false);
    }
    static void RestoreBoundaryChecks()
    {
        // Quick restart and scene travel restore without GameSaveSystem.loading.
        // Their manager flags must provide the same no-replay boundary as disk loading.
        var manager = SceneTravelStateManager.GetOrCreate();
        Check(!GameSaveSystem.IsLoading, "Manager-restore fixtures do not rely on disk-loading flag");
        int fixture = 0;
        foreach (string flag in new[] { "quickRestartPending", "sceneTravelRestoreInProgress" })
        foreach (var kind in new[] { AlchemyPotionKind.Vitality, AlchemyPotionKind.Veil, AlchemyPotionKind.Echo })
        {
            var point = new Vector2(fixture++ * 5f, 160f);
            var actor = Actor(flag + " consumed " + kind, point);
            if (kind == AlchemyPotionKind.Vitality) Reset(actor, 1);
            var area = Area(flag + " area " + kind, point, kind); Step(area);
            if (kind == AlchemyPotionKind.Veil) CharacterPotionBuffs.NotifyAction(actor);
            else if (kind == AlchemyPotionKind.Echo) actor.TakeDamage(100);
            var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(actor.CaptureRuntimeState()));
            var settings = area.CaptureEffectSettings(); bool previous = Get<bool>(manager, flag);
            Set(manager, flag, true);
            try
            {
                // Restore ordering intentionally matches scenes with the area preceding the body.
                area.ApplyEffectSettings(settings); actor.ApplyRuntimeState(state);
            }
            finally { Set(manager, flag, previous); }
            Step(area); Step(area, 3f);
            if (kind == AlchemyPotionKind.Vitality)
                Check(actor.CurrentHealth == 7 && actor.Health == 8, flag + " does not replay saved instant entry healing");
            else
                Check(!CharacterPotionBuffs.Has(actor, kind), flag + " does not rearm saved consumed " + kind);
            Leave(actor, area); actor.transform.position = area.transform.position; Step(area);
            if (kind == AlchemyPotionKind.Vitality)
                Check(actor.CurrentHealth == 8, flag + " permits instant healing on first real subsequent reentry");
            else
                Near(Remaining(actor, kind), CharacterPotionBuffs.Duration(kind) * 2f,
                    flag + " permits rearming on first real subsequent reentry: " + kind);
            area.enabled = false;
        }
    }
    static void SaveChecks(Scene scene)
    {
        var actor = Actor("Saved might subject", new Vector2(0, 100));
        var area = Area("Saved might area", actor.transform.position, AlchemyPotionKind.Might); Step(area);
        string actorJson = JsonUtility.ToJson(actor.CaptureRuntimeState());
        string areaJson = JsonUtility.ToJson(area.CaptureEffectSettings());
        var savedActor = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(actorJson);
        var savedArea = JsonUtility.FromJson<TemporaryCharacterEffectArea.EffectSettings>(areaJson);
        area.enabled = false; Reset(actor); actor.ApplyRuntimeState(savedActor);
        Near(Remaining(actor, AlchemyPotionKind.Might), 60f, "Body JSON restores doubled area duration without truncation");
        Check(savedArea.applyPotion && savedArea.potionKind == AlchemyPotionKind.Might && savedArea.bubbleColor == AlchemyRecipeCatalog.GetColor(AlchemyPotionKind.Might),
            "EffectSettings JSON preserves potion mode, kind and color");

        var healed = Actor("Loaded-inside vitality subject", new Vector2(10, 100)); Reset(healed, 1);
        var healArea = Area("Loaded-inside vitality area", healed.transform.position, AlchemyPotionKind.Vitality); Step(healArea);
        Check(healed.CurrentHealth == 7, "Save fixture has already consumed its instant entry heal");
        string healedJson = JsonUtility.ToJson(healed.CaptureRuntimeState());
        string healAreaJson = JsonUtility.ToJson(healArea.CaptureEffectSettings());
        var save = GameSaveSystem.Instance;
        Check(save != null, "Save system bootstrapped for load-boundary fixture");
        Set(save, "loading", true);
        try
        {
            healArea.ApplyEffectSettings(JsonUtility.FromJson<TemporaryCharacterEffectArea.EffectSettings>(healAreaJson));
            healed.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(healedJson));
        }
        finally { Set(save, "loading", false); }
        Step(healArea); Step(healArea, 3f);
        Check(healed.CurrentHealth == 7 && healed.Health == 8, "Loading inside resumes membership without replaying instant heal");
        Leave(healed, healArea); healed.transform.position = healArea.transform.position; Step(healArea);
        Check(healed.CurrentHealth == 8, "First real post-load reentry is not suppressed");

        foreach (var kind in new[] { AlchemyPotionKind.Veil, AlchemyPotionKind.Echo })
        {
            var point = new Vector2(20 + (int)kind * 3, 100);
            var consumed = Actor("Saved consumed " + kind, point);
            var consumedArea = Area("Saved consumed area " + kind, point, kind); Step(consumedArea);
            if (kind == AlchemyPotionKind.Veil) CharacterPotionBuffs.NotifyAction(consumed);
            else consumed.TakeDamage(100);
            Check(!CharacterPotionBuffs.Has(consumed, kind), "Save fixture consumed one-shot effect: " + kind);
            var consumedState = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(consumed.CaptureRuntimeState()));
            var consumedSettings = consumedArea.CaptureEffectSettings();
            Set(save, "loading", true);
            try { consumedArea.ApplyEffectSettings(consumedSettings); consumed.ApplyRuntimeState(consumedState); }
            finally { Set(save, "loading", false); }
            Step(consumedArea); Step(consumedArea, 3f);
            Check(!CharacterPotionBuffs.Has(consumed, kind), "Load inside does not rearm spent one-shot effect: " + kind);
            Leave(consumed, consumedArea); consumed.transform.position = consumedArea.transform.position; Step(consumedArea);
            Near(Remaining(consumed, kind), CharacterPotionBuffs.Duration(kind) * 2f, "Post-load real reentry rearms: " + kind);
            consumedArea.enabled = false;
        }

        // Exercise the actual scene save DTO, not only the component's local struct.
        var manager = SceneTravelStateManager.GetOrCreate(); string worldJson = manager.CaptureDiskWorld();
        Check(worldJson.Contains("\"hasTemporaryEffectArea\":true") && worldJson.Contains("\"applyPotion\":true"), "Disk world captures potion area settings");
        var worldType = typeof(SceneTravelStateManager).GetNestedType("DiskWorld", BindingFlags.NonPublic);
        var world = JsonUtility.FromJson(worldJson, worldType); object savedScene = null;
        foreach (var entry in Get<IList>(world, "scenes")) if (Get<string>(entry, "name") == scene.name) savedScene = entry;
        Check(savedScene != null, "Disk JSON contains current scene");
        var snapshotType = typeof(SceneTravelStateManager).GetNestedType("SceneState", BindingFlags.NonPublic);
        var snapshot = Activator.CreateInstance(snapshotType, true); Set(snapshot, "sceneName", scene.name);
        foreach (var entry in Get<IList>(savedScene, "objects")) Get<IDictionary>(snapshot, "objects").Add(Get<string>(entry, "path"), entry);
        healArea.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(AlchemyPotionKind.Clamor));
        Set(save, "loading", true);
        try { Call(manager, "RestoreSceneSnapshot", scene, snapshot, false); }
        finally { Set(save, "loading", false); }
        Check(healArea.CaptureEffectSettings().applyPotion && healArea.CaptureEffectSettings().potionKind == AlchemyPotionKind.Vitality,
            "Actual scene snapshot restore reinstates saved potion area, not pending injector configuration");
        healArea.enabled = false;
    }
}
