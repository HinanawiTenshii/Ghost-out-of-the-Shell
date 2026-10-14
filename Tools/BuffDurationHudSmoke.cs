using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Copy into the isolated Unity project's Assets/Editor. Does not edit authored assets.
[InitializeOnLoad]
public static class BuffDurationHudSmoke
{
    const string Pending = "BuffDurationHudSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static BuffDurationHudSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("BUFF_DURATION_HUD_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }
        }
        throw new Exception("Missing field " + name);
    }
    static SwordZeldaCharacterData Actor(string name, bool controlled = false)
    {
        var go = new GameObject(name);
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .35f;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>();
        mover.enabled = controlled;
        if (controlled) ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Reset(data);
        return data;
    }
    static void Reset(ZeldaCharacterData actor, int hp = 8)
    {
        var state = actor.CaptureRuntimeState();
        state.health = 8; state.currentHealth = hp; state.possessionEnergy = 4;
        state.spentBasePossessionEnergy = 0; state.spentGrowthPossessionEnergy = 0;
        state.crystalEnergyThirds = 0; state.potionBuffs = default;
        actor.ApplyRuntimeState(state);
    }
    static void Control(ZeldaCharacterData oldBody, ZeldaCharacterData newBody)
    {
        oldBody.GetComponent<ZeldaFourWayMover>().enabled = false;
        var mover = newBody.GetComponent<ZeldaFourWayMover>();
        mover.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(mover);
    }
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    static void Has(RuntimeBuffDurationBar bar, BuffDurationIcon kind, bool visible, float ratio = -1)
    {
        var graphic = bar.GetIcon(kind);
        Check(graphic != null && graphic.gameObject.activeSelf == visible, kind + " visibility=" + visible);
        if (visible)
        {
            Check(graphic.Icon == kind && !graphic.raycastTarget, kind + " identity and input transparency");
            if (ratio >= 0) Check(Near(graphic.Remaining01, ratio), kind + " remaining ratio=" + ratio);
        }
    }
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Buff Duration HUD Test"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 0;
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var actor = Actor("HUD controlled body", true);
        var next = Actor("HUD alternate body"); next.transform.position = Vector3.right * 20;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        var canvas = Rect("HUD isolated canvas", null, Vector2.zero, new Vector2(1280, 720));
        canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var properties = Rect("Character property row", canvas, new Vector2(12, -92), new Vector2(200, 20));
        var skills = Rect("Skills", canvas, Vector2.zero, Vector2.one).gameObject.AddComponent<RuntimeSkillSlots>();
        skills.Build(font, 12);
        var bar = Rect("Buff Durations", canvas, Vector2.zero, Vector2.zero).gameObject.AddComponent<RuntimeBuffDurationBar>();
        bar.Build(properties, skills);
        Check(bar.DisplayedCharacter == actor && bar.VisibleCount == 0, "Initial controlled body has no timed icons");
        Check(bar.GetIcon((BuffDurationIcon)AlchemyPotionKind.Death) == null, "Instant death has no countdown cell");
        Check(bar.GetIcon((BuffDurationIcon)(-1)) == null && bar.GetIcon((BuffDurationIcon)99) == null, "Unknown icon access is safe");
        Check(bar.transform.childCount == 15, "Fifteen reusable graphics allocated once (ten potion effects plus five skill effects)");
        var same = bar.GetIcon(BuffDurationIcon.Might); bar.Build(properties, skills);
        Check(bar.transform.childCount == 15 && same == bar.GetIcon(BuffDurationIcon.Might), "Repeated Build reuses graphic instances");

        // Independent square-perimeter math: erasure starts upper-left and advances clockwise.
        for (int step = 0; step <= 100; step++)
        {
            float remaining = step / 100f, sum = 0;
            for (int edge = 0; edge < 4; edge++)
            {
                float edgeActual = BuffDurationIconGraphic.GetEdgeRemaining(edge, remaining);
                float expected = Mathf.Clamp01(edge + 1 - (1 - remaining) * 4);
                Check(Near(edgeActual, expected), "Perimeter edge=" + edge + ", remaining=" + remaining);
                sum += edgeActual;
            }
            Check(Near(sum, remaining * 4), "Perimeter total scales linearly with time");
        }
        Check(Near(BuffDurationIconGraphic.GetEdgeRemaining(0, .75f), 0) && Near(BuffDurationIconGraphic.GetEdgeRemaining(1, .75f), 1), "25 percent elapsed removes precisely the top edge");
        Check(Near(BuffDurationIconGraphic.GetEdgeRemaining(1, .5f), 0) && Near(BuffDurationIconGraphic.GetEdgeRemaining(2, .5f), 1), "50 percent elapsed removes top and right");
        Check(Near(BuffDurationIconGraphic.GetEdgeRemaining(2, .25f), 0) && Near(BuffDurationIconGraphic.GetEdgeRemaining(3, .25f), 1), "75 percent elapsed leaves only the left edge");

        Reset(actor, 2); Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Vitality), "Drink healing potion");
        bar.RefreshNow(); Check(bar.VisibleCount == 0, "Instant HP restoration does not invent a lasting icon");
        Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Vitality), "Drink at maximum health");
        bar.RefreshNow(); Has(bar, BuffDurationIcon.Vitality, true, 1);
        Reset(actor); actor.TrySpendPossessionEnergy(2);
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit); bar.RefreshNow();
        Check(bar.VisibleCount == 0, "Instant energy restoration does not invent a lasting icon");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Spirit); bar.RefreshNow();
        Has(bar, BuffDurationIcon.Spirit, true, 1);
        Check(Near(growth.PotionEnergyRemaining, 30), "Temporary energy denominator is exactly thirty seconds");

        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            if (kind == AlchemyPotionKind.Death || kind == AlchemyPotionKind.Spirit) continue;
            Check(CharacterPotionBuffs.TryApply(actor, kind), "Apply timed effect " + kind);
        }
        bar.RefreshNow(); Check(bar.VisibleCount == 9, "All nine timed potion effects shown together");
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
            if (kind != AlchemyPotionKind.Death) Has(bar, (BuffDurationIcon)kind, true, 1);
        Check(Near(actor.GetComponent<CharacterPotionBuffs>().Remaining(AlchemyPotionKind.Echo), 120), "Echo preserves one hundred twenty second countdown");
        Check(Near(((RectTransform)bar.transform).anchoredPosition.y, -120), "Bar sits below the final character property row");
        Check(Near(((RectTransform)bar.transform).sizeDelta.y, 40), "Nine buffs fit in one row at reference width");

        var bodyState = actor.GetComponent<CharacterPotionBuffs>().CaptureRuntimeState();
        for (int i = 0; i < bodyState.remaining.Length; i++) bodyState.remaining[i] *= .5f;
        actor.GetComponent<CharacterPotionBuffs>().ApplyRuntimeState(JsonUtility.FromJson<CharacterPotionBuffs.RuntimeState>(JsonUtility.ToJson(bodyState)));
        var playerState = growth.CaptureRuntimeState(); playerState.potionEnergyRemaining = 15;
        growth.ApplyRuntimeState(JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(playerState)));
        bar.RefreshNow(); Has(bar, BuffDurationIcon.Might, true, .5f); Has(bar, BuffDurationIcon.Echo, true, .5f); Has(bar, BuffDurationIcon.Spirit, true, .5f);
        float before = same.Remaining01;
        yield return null; yield return null;
        Check(Near(same.Remaining01, before), "Paused game freezes the automatic visual countdown");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might); bar.RefreshNow(); Has(bar, BuffDurationIcon.Might, true, 1);
        Check(bar.VisibleCount == 9 && same == bar.GetIcon(BuffDurationIcon.Might), "Reapplication refills existing cell, not a duplicate");

        Control(actor, next); bar.RefreshNow();
        Check(bar.DisplayedCharacter == next && bar.VisibleCount == 1, "Body switch hides prior body's modifiers");
        Has(bar, BuffDurationIcon.Might, false); Has(bar, BuffDurationIcon.Spirit, true, .5f);
        Control(next, actor); bar.RefreshNow(); Check(bar.VisibleCount == 9, "Returning to prior body restores its active effect icons");
        bodyState = actor.GetComponent<CharacterPotionBuffs>().CaptureRuntimeState(); bodyState.immunityRemaining = .5f;
        actor.GetComponent<CharacterPotionBuffs>().ApplyRuntimeState(bodyState); bar.RefreshNow();
        Check(bar.VisibleCount == 10, "Brief Echo protection can coexist without overflowing allocated cells");
        Has(bar, BuffDurationIcon.EchoImmunity, true, .5f);
        canvas.sizeDelta = new Vector2(320, 360); bar.RefreshNow();
        Check(Near(((RectTransform)bar.transform).sizeDelta.x, 86) && Near(((RectTransform)bar.transform).sizeDelta.y, 224), "Narrow canvas wraps ten cells into five rows");
        float barBottom = -((RectTransform)bar.transform).anchoredPosition.y + ((RectTransform)bar.transform).sizeDelta.y;
        float skillTop = canvas.rect.height * .5f - ((RectTransform)skills.transform).anchoredPosition.y - 64f - 3.6f;
        Check(skillTop >= barBottom + 7.9f, "Existing skill shortcuts move clear of wrapped effect rows");
        canvas.sizeDelta = new Vector2(1280, 720); bar.RefreshNow();
        Check(Near(((RectTransform)bar.transform).sizeDelta.y, 40), "Canvas widening restores one-row arrangement");
        for (int i = 0; i < 50; i++) bar.RefreshNow();
        Check(bar.transform.childCount == 15, "Repeated frame refreshes do not allocate additional GameObjects");

        Reset(actor); growth.ApplyRuntimeState(default);
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Echo); actor.TakeDamage(100); bar.RefreshNow();
        Check(!actor.IsDead && bar.VisibleCount == 1, "Lethal Echo consumption replaces anchor with brief protection");
        Has(bar, BuffDurationIcon.Echo, false); Has(bar, BuffDurationIcon.EchoImmunity, true, 1);
        Reset(actor); CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        bodyState = actor.GetComponent<CharacterPotionBuffs>().CaptureRuntimeState(); bodyState.remaining[(int)AlchemyPotionKind.Might] = .12f;
        actor.GetComponent<CharacterPotionBuffs>().ApplyRuntimeState(bodyState); bar.RefreshNow();
        Has(bar, BuffDurationIcon.Might, true, .004f);
        Time.timeScale = 1;
        float deadline = Time.time + .3f; while (Time.time < deadline) yield return null;
        Check(bar.VisibleCount == 0 && !same.gameObject.activeSelf, "Expired effect automatically disappears and leaves no dead frame");
        Time.timeScale = 0;
        Check(((RectTransform)bar.transform).sizeDelta == Vector2.zero, "No effects collapses occupied layout space");
        Check(Near(((RectTransform)skills.transform).anchoredPosition.y, 116), "No effects restores original skill column placement");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        var deadState = actor.CaptureRuntimeState(); deadState.currentHealth = 0; actor.ApplyRuntimeState(deadState);
        bar.RefreshNow(); Check(bar.VisibleCount == 0, "Dead body's saved timers never render");
        Reset(actor); actor.GetComponent<ZeldaFourWayMover>().enabled = false; bar.RefreshNow();
        Check(bar.VisibleCount == 0 && bar.DisplayedCharacter == null, "No active controlled mover means no body HUD");
        actor.GetComponent<ZeldaFourWayMover>().enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(actor.GetComponent<ZeldaFourWayMover>());

        // Exercise the production Awake wiring, not just the isolated bar fixture.
        var hud = ZeldaHealthHeartsUI.Instance;
        if (hud == null)
        {
            var go = new GameObject("Production HUD wiring", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.SetActive(false); hud = go.AddComponent<ZeldaHealthHeartsUI>();
            Set(hud, "persistBetweenScenes", false); Set(hud, "permissionLabelFont", font); go.SetActive(true);
        }
        var actual = hud.transform.Find("Buff Durations").GetComponent<RuntimeBuffDurationBar>();
        Check(actual != null && actual.isActiveAndEnabled, "Production HUD automatically creates independent duration component");
        hud.enabled = false; CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        yield return null; yield return null;
        Check(actual.VisibleCount == 1 && !hud.enabled, "Countdown updates even when event-driven health HUD script sleeps");
        var healthRow = (RectTransform)hud.transform.Find("Buff Durations");
        Check(healthRow.anchoredPosition.y < -40, "Production duration strip is beneath attributes");
        hud.gameObject.SetActive(false); canvas.gameObject.SetActive(false);
        var preview = Preview(font);
        while (preview.MoveNext()) yield return preview.Current;
        Time.timeScale = 1;
    }

    static Text Label(Transform parent, Font font, string text, Vector2 position, Vector2 size, int fontSize = 24)
    {
        var label = Rect(text, parent, position, size).gameObject.AddComponent<Text>();
        label.font = font; label.fontSize = fontSize; label.text = text;
        label.color = new Color32(95, 248, 233, 255); label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft; return label;
    }
    static BuffDurationIconGraphic PreviewIcon(Transform parent, BuffDurationIcon icon, Vector2 position, float ratio, float size = 88)
    {
        var graphic = Rect(icon.ToString(), parent, position, Vector2.one * size).gameObject.AddComponent<BuffDurationIconGraphic>();
        graphic.SetDisplay(icon, ratio); return graphic;
    }
    static IEnumerator Preview(Font font)
    {
        const int width = 1600, height = 850;
        var canvasRect = Rect("Buff Duration Visual Preview", null, Vector2.zero, new Vector2(width, height));
        canvasRect.pivot = Vector2.zero;
        canvasRect.position = Vector3.zero;
        var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 32760;
        var background = Rect("Preview backdrop", canvasRect, Vector2.zero, new Vector2(width, height)).gameObject.AddComponent<Image>();
        background.color = new Color32(7, 17, 42, 255); background.raycastTarget = false;
        Label(canvasRect, font, "BUFF DURATION / LIVE HUD GRAPHICS", new Vector2(40, -28), new Vector2(1500, 48), 32);
        Label(canvasRect, font, "Navy panels + cyan pixel symbols | clockwise perimeter depletion", new Vector2(40, -83), new Vector2(1500, 40), 22);
        BuffDurationIcon[] kinds = { BuffDurationIcon.Vitality, BuffDurationIcon.Spirit, BuffDurationIcon.Might,
            BuffDurationIcon.Veil, BuffDurationIcon.Berserk, BuffDurationIcon.Sight,
            BuffDurationIcon.Weakness, BuffDurationIcon.Clamor, BuffDurationIcon.Echo, BuffDurationIcon.EchoImmunity };
        for (int i = 0; i < kinds.Length; i++)
        {
            float x = 55 + (i % 5) * 306, y = 155 + (i / 5) * 160;
            var icon = PreviewIcon(canvasRect, kinds[i], new Vector2(x, -y), 1);
            Label(canvasRect, font, kinds[i].ToString(), new Vector2(x + 108, -y - 21), new Vector2(200, 45), 24);
            using (var vh = new VertexHelper())
            {
                typeof(BuffDurationIconGraphic).GetMethod("OnPopulateMesh", Flags, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(icon, new object[] { vh });
                Check(vh.currentVertCount > 16 && vh.currentVertCount < 4096, kinds[i] + " renders one bounded UI mesh");
            }
        }
        Label(canvasRect, font, "TIMER: elapsed time erases from top-left, then right / bottom / left", new Vector2(40, -503), new Vector2(1500, 40), 25);
        float[] ratios = { 1, .875f, .75f, .5f, .25f, 0 };
        for (int i = 0; i < ratios.Length; i++)
        {
            float x = 60 + i * 255;
            PreviewIcon(canvasRect, BuffDurationIcon.Echo, new Vector2(x, -570), ratios[i], 112);
            Label(canvasRect, font, (ratios[i] * 100).ToString("0.#") + "% remaining", new Vector2(x, -693), new Vector2(235, 36), 22);
        }
        Label(canvasRect, font, "IN-GAME SCALE", new Vector2(40, -775), new Vector2(230, 40), 22);
        for (int i = 0; i < kinds.Length; i++) PreviewIcon(canvasRect, kinds[i], new Vector2(310 + i * 46, -776), 1 - i * .08f, 40);
        Label(canvasRect, font, "40 px / pooled graphics / no glow", new Vector2(820, -775), new Vector2(650, 40), 23);
        var camera = new GameObject("Buff preview camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(width * .5f, height * .5f, -10);
        camera.orthographic = true; camera.orthographicSize = height * .5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(7, 17, 42, 255);
        canvas.worldCamera = camera;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "BuffDurationHud-preview.png"), image.EncodeToPNG());
        Color[] colors = image.GetPixels(); int cyan = 0;
        foreach (var color in colors) if (color.g > .6f && color.b > .5f && color.r < .8f) cyan++;
        Check(cyan > 5000, "GPU preview contains visible cyan icon and countdown geometry");
        RenderTexture.active = old; camera.targetTexture = null; target.Release();
        UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(canvasRect.gameObject); UnityEngine.Object.Destroy(camera.gameObject);
    }
}
