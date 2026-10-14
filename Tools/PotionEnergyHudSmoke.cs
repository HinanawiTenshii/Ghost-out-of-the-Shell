using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Copy to the isolated Unity test project's Assets/Editor; never saves scene changes.
[InitializeOnLoad]
public static class PotionEnergyHudSmoke
{
    const string Pending = "PotionEnergyHudSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static PotionEnergyHudSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("POTION_ENERGY_HUD_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    static SwordZeldaCharacterData Actor(string name, int energy, bool controlled)
    {
        var root = new GameObject(name); root.AddComponent<BoxCollider2D>().size = Vector2.one * .4f;
        var data = root.AddComponent<SwordZeldaCharacterData>();
        var mover = root.AddComponent<ZeldaFourWayMover>(); mover.enabled = controlled;
        if (controlled) ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var state = data.CaptureRuntimeState(); state.possessionEnergy = energy;
        state.spentBasePossessionEnergy = 0; state.spentGrowthPossessionEnergy = 0;
        state.crystalEnergyThirds = 0; data.ApplyRuntimeState(state);
        return data;
    }
    static List<Image> Bars(ZeldaHealthHeartsUI hud) => Get<List<Image>>(hud, "possessionEnergyBars");
    static void Count(ZeldaHealthHeartsUI hud, ZeldaCharacterData data, int expected)
    {
        int visible = 0;
        foreach (var image in Bars(hud)) if (image.gameObject.activeSelf) visible++;
        Check(data.PossessionEnergyDisplaySlotCount == expected, "Unified slot count equals " + expected);
        Check(visible == expected, "Actual event-driven HUD count equals " + expected + ", got " + visible);
    }
    static void Solid(Image image, bool expected, string label)
    {
        Check((image.sprite != ZeldaHealthHeartsUI.GetSpentPossessionEnergyIcon()) == expected, label);
        Check(image.type == Image.Type.Simple, label + " uses a normal energy icon, not a crystal fraction");
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Potion Energy HUD Regression"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 0f;
        if (PlayerGrowthAttributes.Instance != null)
        {
            UnityEngine.Object.Destroy(PlayerGrowthAttributes.Instance.gameObject);
            yield return null;
        }
        Check(PlayerGrowthAttributes.Instance == null, "Direct level starts without a player growth bootstrap");
        var actor = Actor("Potion HUD controlled body", 3, true);
        var mover = actor.GetComponent<ZeldaFourWayMover>();
        var source = ActiveZeldaCharacterDataSource.Instance;
        if (source == null) source = new GameObject("Potion HUD source").AddComponent<ActiveZeldaCharacterDataSource>();
        source.Refresh();
        var hud = ZeldaHealthHeartsUI.Instance;
        if (hud == null)
            hud = new GameObject("Potion HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<ZeldaHealthHeartsUI>();
        yield return null;
        Count(hud, actor, 3);
        for (int i = 0; i < 3; i++) Solid(Bars(hud)[i], true, "Original full base energy " + i);
        int changes = 0; source.DataChanged += () => changes++;
        Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit), "Spirit works without initial growth component");
        var growth = PlayerGrowthAttributes.Instance;
        Check(growth != null && changes > 0, "Late growth instance connects and notifies the existing HUD");
        Count(hud, actor, 5);
        Check(actor.FinalPossessionEnergy == 3 && actor.MaxPossessionEnergy == 5, "Capacity is added without filling it");
        Solid(Bars(hud)[3], false, "First new potion slot is empty");
        Solid(Bars(hud)[4], false, "Second new potion slot is empty");
        Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit), "Next drink restores reserve");
        Count(hud, actor, 5);
        Check(actor.FinalPossessionEnergy == 5, "Second drink fills both temporary points");
        Solid(Bars(hud)[3], true, "First potion slot filled");
        Solid(Bars(hud)[4], true, "Second potion slot filled");

        // Crystal thirds occupy their own slot even though partial thirds do not
        // count toward integer MaxPossessionEnergy. Potion points stay separate.
        for (int thirds = 1; thirds <= 3; thirds++)
        {
            var body = actor.CaptureRuntimeState(); body.possessionEnergy = 3;
            body.spentBasePossessionEnergy = 0; body.crystalEnergyThirds = thirds; actor.ApplyRuntimeState(body);
            var reserve = growth.CaptureRuntimeState(); reserve.potionEnergyCurrent = 1; growth.ApplyRuntimeState(reserve);
            Count(hud, actor, 6);
            var bars = Bars(hud);
            Check(actor.IsCrystalPossessionEnergyDisplaySlot(3) && !actor.IsCrystalPossessionEnergyDisplaySlot(4), "Only the crystal slot is fractional");
            Check(bars[3].type == Image.Type.Filled && Mathf.Abs(bars[3].fillAmount - thirds / 3f) < .001f, "Preserve crystal fraction " + thirds);
            Check(bars[3].GetComponent<TemporaryEnergyParticles>() != null, "Preserve original crystal particles");
            Solid(bars[4], true, "Potion reserve begins after the crystal");
            Solid(bars[5], false, "Potion reserve empty second slot");
            Check(actor.TrySpendPossessionEnergy(2), "Spend base energy independently of temporary pools");
            Solid(bars[0], true, "One base point remains");
            Solid(bars[1], false, "Potion point cannot fill a spent base icon");
            Solid(bars[2], false, "Crystal cannot fill a spent base icon");
            Solid(bars[4], true, "Temporary point remains after base spending");
        }
        Check(actor.GetPossessionEnergyDisplayFill(-1) == 0f && actor.GetPossessionEnergyDisplayFill(99) == 0f, "Out-of-range display slots are safely empty");
        var stats = actor.GetComponent<ZeldaCharacterStatsWindow>();
        if (stats == null) stats = actor.gameObject.AddComponent<ZeldaCharacterStatsWindow>();
        stats.Configure(actor, Vector2.zero, 2.2f);
        int statsCount = 0;
        foreach (var image in Get<Image[]>(stats, "costIcons")) if (image.enabled) statsCount++;
        Check(statsCount == 6, "Character attributes use the same six-slot layout");

        var other = Actor("Potion HUD next body", 5, false);
        mover.enabled = false; var otherMover = other.GetComponent<ZeldaFourWayMover>(); otherMover.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(otherMover);
        Count(hud, other, 7);
        Check(actor.PossessionEnergyDisplaySlotCount == 4, "Previous body retains its crystal but no player reserve");
        Solid(Bars(hud)[5], true, "Temporary reserve follows player to next body");
        Solid(Bars(hud)[6], false, "Empty reserve point also follows player");
        Call(stats, "Refresh", false);
        statsCount = 0; foreach (var image in Get<Image[]>(stats, "costIcons")) if (image.enabled) statsCount++;
        Check(statsCount == 4, "Previous body's attribute window no longer shows player-owned potion slots");

        int beforeExpiry = changes;
        var expiring = growth.CaptureRuntimeState(); expiring.potionEnergyRemaining = .03f; growth.ApplyRuntimeState(expiring);
        Time.timeScale = 1f; float until = Time.time + .12f; while (Time.time < until) yield return null;
        Time.timeScale = 0f;
        Count(hud, other, 5);
        Check(changes > beforeExpiry && other.FinalPossessionEnergy == 5, "Expiry notifies HUD and removes only the temporary reserve");
        Check(other.TrySpendPossessionEnergy(2), "Base spending works after reserve expiry");
        Solid(Bars(hud)[3], false, "Legacy spent base icon still appears");
        Check(CharacterPotionBuffs.TryApply(other, AlchemyPotionKind.Spirit), "Spirit restores spent ordinary energy");
        Count(hud, other, 5);
        Solid(Bars(hud)[3], true, "Restored base icon is solid");
        Solid(Bars(hud)[4], true, "Restored last base icon is solid");
        Time.timeScale = 1f;
    }
}
