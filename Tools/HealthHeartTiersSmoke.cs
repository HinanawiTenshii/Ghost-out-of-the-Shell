using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Run in the isolated Unity validation project, without saving any scenes.
[InitializeOnLoad]
public static class HealthHeartTiersSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static HealthHeartTiersSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("HealthHeartTiersSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("HealthHeartTiersSmoke", false))
            { SessionState.SetBool("HealthHeartTiersSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Debug.Log("HEALTH_HEART_TIERS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Flags).GetValue(obj);
    static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
    static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
    static ZeldaCharacterData Actor(bool ghost)
    {
        var go = new GameObject(ghost ? "Ghost HUD fixture" : "Body HUD fixture");
        ZeldaCharacterData data = ghost ? (ZeldaCharacterData)go.AddComponent<GhostZeldaCharacterData>() : go.AddComponent<SwordZeldaCharacterData>();
        go.AddComponent<ZeldaFourWayMover>().enabled = false;
        return data;
    }
    static void Control(ZeldaCharacterData data)
    {
        data.GetComponent<ZeldaFourWayMover>().enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(data.GetComponent<ZeldaFourWayMover>());
    }
    static void Health(ZeldaCharacterData data, int value)
    {
        var state = data.CaptureRuntimeState(); state.health = 60; state.currentHealth = value;
        data.ApplyRuntimeState(state);
    }
    static void Validate(ZeldaHealthHeartsUI hud, int value, bool ghost = false)
    {
        var hearts = Get<Image[]>(hud, "hearts");
        Check(hearts.Length == 10, "exactly ten slots, no second row");
        Color red = Get<Color>(hud, "normalHeartColor"), yellow = Get<Color>(hud, "overHealthColor"), blue = Get<Color>(hud, "ghostHeartColor");
        int visible = Mathf.Clamp(value, 0, 10), extra = Mathf.Clamp(value - 10, 0, 10);
        for (int i = 0; i < hearts.Length; i++)
        {
            Check(hearts[i].gameObject.activeSelf == (i < visible), "visibility at health " + value + " slot " + i);
            Check(hearts[i].color == (ghost ? blue : i < extra ? yellow : red), "base tint at health " + value + " slot " + i);
            if (i > 0) Check(hearts[i].rectTransform.anchoredPosition.x > hearts[i - 1].rectTransform.anchoredPosition.x, "array runs left to right");
        }
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var actor = Actor(false); Health(actor, 9); Control(actor);
        var source = ActiveZeldaCharacterDataSource.Instance;
        if (!source) source = new GameObject("HUD data source").AddComponent<ActiveZeldaCharacterDataSource>();
        source.Refresh();
        var hud = ZeldaHealthHeartsUI.Instance;
        if (!hud) hud = new GameObject("Heart tier HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<ZeldaHealthHeartsUI>();
        yield return null;
        foreach (int hp in new[] { 1, 9, 10, 11, 12, 15, 19, 20, 21, 60, 20, 15, 11, 10, 9 })
        {
            Health(actor, hp); source.Refresh();
            Check(source.CurrentHealth == hp && actor.CurrentHealth == hp, "display does not cap actual health");
            Validate(hud, hp);
            Call(hud, "SetHeartColors", true);
            foreach (var heart in Get<Image[]>(hud, "hearts")) Check(heart.color == Get<Color>(hud, "healthFlashColor"), "shared highlight still works");
            Call(hud, "UpdateHealthFlash", 10f);
            Validate(hud, hp);
            Check(Get<float>(hud, "healthFlashTimer") == 0f, "flash completes at zero");
            Call(hud, "UpdateHealthFlash", 0f); Validate(hud, hp);
        }
        // Switch between ghost and ordinary bodies; preserve the existing blue ghost palette.
        Health(actor, 15); source.Refresh();
        var ghost = Actor(true); Health(ghost, 21); Control(ghost); source.Refresh();
        Call(hud, "UpdateHealthFlash", 10f); Validate(hud, 21, true);
        ghost.GetComponent<ZeldaFourWayMover>().enabled = false;
        Health(actor, 10); Control(actor); source.Refresh();
        Call(hud, "UpdateHealthFlash", 10f); Validate(hud, 10);
        Health(actor, 0); source.Refresh(); Call(hud, "UpdateHealthFlash", 10f); Validate(hud, 0);
    }
}
