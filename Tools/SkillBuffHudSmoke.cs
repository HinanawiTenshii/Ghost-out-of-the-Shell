using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Stage in an isolated Unity project's Assets/Editor; never edits authored assets.
[InitializeOnLoad]
public static class SkillBuffHudSmoke
{
    const string Pending = "SkillBuffHudSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, frame = -1;
    static readonly BuffDurationIcon[] Skills = { BuffDurationIcon.CombatExpertise, BuffDurationIcon.GhostForm,
        BuffDurationIcon.RoyalCommand, BuffDurationIcon.SoulMark, BuffDurationIcon.Domino };
    static readonly string[] SkillIds = { "combat_expertise_2", "ghost_form_3", "royal_command_2", "soul_mark_3", "domino_3" };

    static SkillBuffHudSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("SKILL_BUFF_HUD_PASS checks=" + checks); EditorApplication.Exit(0); }
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
    static SwordZeldaCharacterData Actor(string name, Vector3 position, bool controlled = false)
    {
        var go = new GameObject(name); go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .35f;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = controlled;
        if (controlled) ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Reset(data); return data;
    }
    static void Reset(ZeldaCharacterData actor)
    {
        var state = actor.CaptureRuntimeState();
        state.health = state.currentHealth = 8; state.possessionEnergy = 10; state.possessionCost = 1;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        state.combatExpertiseRemaining = state.ghostFormRemaining = state.ghostFormDuration = 0;
        state.combatExpertiseRegeneration = false; state.potionBuffs = default;
        actor.ApplyRuntimeState(state);
    }
    static void Control(ZeldaCharacterData oldBody, ZeldaCharacterData newBody)
    {
        oldBody.GetComponent<ZeldaFourWayMover>().enabled = false;
        var mover = newBody.GetComponent<ZeldaFourWayMover>(); mover.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
    }
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static void Has(RuntimeBuffDurationBar bar, BuffDurationIcon kind, Sprite expected, float ratio, bool timed = true)
    {
        var graphic = bar.GetIcon(kind);
        Check(graphic != null && graphic.gameObject.activeSelf, kind + " is visible");
        Check(graphic.Icon == kind && !graphic.raycastTarget, kind + " identity and input transparency");
        Check(graphic.SkillSprite == expected && expected != null, kind + " reuses exact existing skill sprite");
        Check(graphic.IsTimed == timed, kind + " timed state");
        if (timed) Check(Near(graphic.Remaining01, ratio), kind + " real runtime ratio=" + ratio);
        var images = graphic.GetComponentsInChildren<Image>(true);
        Check(images.Length == 1 && images[0].sprite == expected && images[0].enabled && !images[0].raycastTarget,
            kind + " renders the original sprite with one input-transparent child");
        Check(CharacterSkillBuffs.TryGet(bar.DisplayedCharacter, kind, out var state) &&
            state.Icon == expected && state.IsTimed == timed, kind + " adapter matches displayed state");
        if (timed) Check(state.Duration > 0 && Near(state.Remaining / state.Duration, ratio), kind + " adapter uses actual remaining and duration");
        else Check(state.Duration == 0, kind + " never invents an expiry duration");
    }
    static void Hidden(RuntimeBuffDurationBar bar, BuffDurationIcon kind)
    { Check(!bar.GetIcon(kind).gameObject.activeSelf, kind + " disappears with effect"); }

    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Skill Buff HUD Test"); SceneManager.SetActiveScene(scene);
        Time.timeScale = 0;
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var actor = Actor("Skill HUD controlled body", Vector3.zero, true);
        actor.gameObject.AddComponent<CombatExpertiseCharacterSkillData>();
        var next = Actor("Skill HUD alternate body", Vector3.right * 20);
        var mover = actor.GetComponent<ZeldaFourWayMover>();
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        var canvas = Rect("Skill HUD isolated canvas", null, Vector2.zero, new Vector2(1280, 720));
        canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var properties = Rect("Character property row", canvas, new Vector2(12, -92), new Vector2(200, 20));
        var slots = Rect("Skills", canvas, Vector2.zero, Vector2.one).gameObject.AddComponent<RuntimeSkillSlots>(); slots.Build(font, 12);
        var bar = Rect("Buff Durations", canvas, Vector2.zero, Vector2.zero).gameObject.AddComponent<RuntimeBuffDurationBar>();
        bar.Build(properties, slots);
        Check(bar.DisplayedCharacter == actor && bar.VisibleCount == 0, "No fabricated effects before a cast");
        Check(bar.transform.childCount == 16, "Ten potion and six skill cells are pooled");
        Check(!Enum.IsDefined(typeof(BuffDurationIcon), "Stun") && !Enum.IsDefined(typeof(BuffDurationIcon), "FearRoar"), "Stun and its causing skill do not receive buff tiles");
        Check(!CharacterSkillBuffs.TryGet(actor, BuffDurationIcon.Might, out _) &&
            !CharacterSkillBuffs.TryGet(actor, (BuffDurationIcon)99, out _) &&
            !CharacterSkillBuffs.TryGet(null, BuffDurationIcon.GhostForm, out _), "Adapter rejects potion kinds, unknown kinds and missing body");

        Check(actor.TryUseCombatExpertise(), "Cast real native combat expertise"); bar.RefreshNow();
        Has(bar, BuffDurationIcon.CombatExpertise, actor.CharacterSkillIcon, 1);
        Check(bar.VisibleCount == 1, "Native combat is one independent tile");
        Check(!actor.TryUseCombatExpertise(), "Active combat skill cannot duplicate itself");
        var state = actor.CaptureRuntimeState(); state.combatExpertiseRemaining = 1.5f;
        actor.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(state)));
        bar.RefreshNow(); Has(bar, BuffDurationIcon.CombatExpertise, actor.CharacterSkillIcon, .5f);

        var learned = growth.CaptureRuntimeState();
        learned.unlockedSkills = new[] { "combat_expertise", "combat_expertise_2", "ghost_form", "ghost_form_2", "ghost_form_3",
            "royal_command", "royal_command_2", "soul_mark", "soul_mark_2", "soul_mark_3", "domino", "domino_2", "domino_3" };
        growth.ApplyRuntimeState(learned); Reset(actor);
        Check(actor.TryUseCombatExpertise(), "Cast upgraded combat expertise");
        Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might), "Drink potion before entering ghost form");
        GhostFormRuntime.Use(mover); bar.RefreshNow();
        Has(bar, BuffDurationIcon.CombatExpertise, SkillPageArt.GetAbilitySprite("combat_expertise_2"), 1);
        Has(bar, BuffDurationIcon.GhostForm, SkillPageArt.GetAbilitySprite("ghost_form_3"), 1);
        Check(actor.IsGhostForm && Near(actor.GhostForm.TotalDuration, 7), "Real upgraded ghost cast keeps seven-second denominator");
        state = actor.CaptureRuntimeState(); state.combatExpertiseRemaining = 1.5f; state.ghostFormRemaining = 3.5f;
        string json = JsonUtility.ToJson(state); Reset(actor);
        actor.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(json)); bar.RefreshNow();
        Has(bar, BuffDurationIcon.CombatExpertise, SkillPageArt.GetAbilitySprite("combat_expertise_2"), .5f);
        Has(bar, BuffDurationIcon.GhostForm, SkillPageArt.GetAbilitySprite("ghost_form_3"), .5f);
        yield return null; yield return null;
        Has(bar, BuffDurationIcon.CombatExpertise, SkillPageArt.GetAbilitySprite("combat_expertise_2"), .5f);
        Has(bar, BuffDurationIcon.GhostForm, SkillPageArt.GetAbilitySprite("ghost_form_3"), .5f);
        Check(Near(actor.CaptureRuntimeState().ghostFormRemaining, 3.5f), "Pause freezes authoritative and displayed skill timers");
        bar.RefreshNow();
        Check(bar.VisibleCount == 3 && bar.GetIcon(BuffDurationIcon.Might).SkillSprite == null && bar.GetIcon(BuffDurationIcon.Might).IsTimed,
            "Potion glyph/countdown remains independent beside skill sprites");
        Control(actor, next); bar.RefreshNow(); Check(bar.VisibleCount == 0 && bar.DisplayedCharacter == next, "Possession switches away from body-local skill and potion effects");
        Control(next, actor); bar.RefreshNow(); Check(bar.VisibleCount == 3, "Returning to affected body restores skill HUD");
        GhostFormRuntime.Restore(actor, 0); bar.RefreshNow(); Hidden(bar, BuffDurationIcon.GhostForm);
        Reset(actor); bar.RefreshNow(); Check(bar.VisibleCount == 0, "Ended and reset effects leave no orphan tiles");
        var weakGrowth = growth.CaptureRuntimeState(); weakGrowth.unlockedSkills = Array.Empty<string>(); growth.ApplyRuntimeState(weakGrowth);
        GhostFormRuntime.Restore(actor, 3.5f, 7); bar.RefreshNow();
        Has(bar, BuffDurationIcon.GhostForm, SkillPageArt.GetAbilitySprite("ghost_form"), .5f);
        Check(Near(actor.GhostForm.TotalDuration, 7), "Restored seven-second cast is not rescaled by current skill level");
        Reset(actor); growth.ApplyRuntimeState(learned);

        var linkTarget = Actor("Domino and soul target", Vector3.right);
        DominoSkillRuntime.Use(mover); bar.RefreshNow();
        Check(DominoSkillRuntime.HasActiveMark(actor) && DominoSkillRuntime.HasActiveMark(linkTarget), "Real Domino cast established a damage link");
        Has(bar, BuffDurationIcon.Domino, SkillPageArt.GetAbilitySprite("domino_3"), 1, false);
        Check(CharacterSkillBuffs.TryGet(linkTarget, BuffDurationIcon.Domino, out var linked) && !linked.IsTimed, "Any genuinely linked body reports untimed Domino");
        Control(actor, next); bar.RefreshNow(); Hidden(bar, BuffDurationIcon.Domino);
        Control(next, actor); bar.RefreshNow();
        DominoSkillRuntime.RemoveMark(linkTarget); bar.RefreshNow(); Hidden(bar, BuffDurationIcon.Domino);
        Check(!DominoSkillRuntime.HasActiveMark(actor), "Removing final partner dissolves caster link and HUD state");

        linkTarget.transform.position = new Vector3(0, -1.1f); Physics2D.SyncTransforms(); Reset(actor);
        Check(mover.GetSoulMarkCandidate() == linkTarget.GetComponent<ZeldaFourWayMover>(), "Soul-mark fixture uses actual possession target detection");
        SoulMarkRuntime.Use(mover); bar.RefreshNow();
        Has(bar, BuffDurationIcon.SoulMark, SkillPageArt.GetAbilitySprite("soul_mark_3"), 1, false);
        Control(actor, next); bar.RefreshNow();
        Has(bar, BuffDurationIcon.SoulMark, SkillPageArt.GetAbilitySprite("soul_mark_3"), 1, false);
        Check(bar.VisibleCount == 1, "Pending soul mark remains usable and shown after changing controlled body");
        Control(next, actor);
        SoulMarkRuntime.ClearAfterDirectPossession(linkTarget.GetComponent<ZeldaFourWayMover>()); bar.RefreshNow();
        Hidden(bar, BuffDurationIcon.SoulMark);

        var recipient = Actor("Royal command AI recipient", Vector3.right * 40);
        var ai = recipient.gameObject.AddComponent<ZeldaCharacterAiBase>();
        ai.StunForDuration(3); bar.RefreshNow();
        Check(ai.IsStunned && bar.VisibleCount == 0, "Real stun is explicitly absent from buff HUD");
        Set(ai, "currentState", ZeldaAiState.Hostile); Set(ai, "targetMover", mover);
        Check(actor.TryUseRoyalCommand(), "Cast real royal command against an alerted AI"); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), 1);
        Check(Near(ai.RoyalCommandBuffRemaining, 5), "Command HUD duration comes from affected AI");
        var savedAi = ai.CaptureSaveState(); savedAi.royalCommandRemaining = 2.5f;
        ai.ApplySaveState(JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(savedAi))); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .5f);
        var secondRecipient = Actor("Second commanded AI", Vector3.right * 45);
        var secondAi = secondRecipient.gameObject.AddComponent<ZeldaCharacterAiBase>(); secondAi.ReceiveRoyalCommand();
        Set(secondAi, "royalCommandRemaining", 4f); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .8f);
        Check(bar.VisibleCount == 1, "Multiple commanded NPCs share one tile showing the longest live timer");
        Set(secondAi, "royalCommandRemaining", 0f); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .5f);
        var otherScene = SceneManager.CreateScene("Unrelated commanded NPC scene");
        SceneManager.MoveGameObjectToScene(secondRecipient.gameObject, otherScene);
        Set(secondAi, "royalCommandRemaining", 5f); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .5f);
        Check(bar.VisibleCount == 1, "Loaded NPCs in another gameplay scene do not extend the command buff");
        Control(actor, next); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .5f);
        Control(next, actor); ai.enabled = false; bar.RefreshNow(); Hidden(bar, BuffDurationIcon.RoyalCommand);
        ai.enabled = true; Set(ai, "royalCommandRemaining", 2.5f); recipient.gameObject.SetActive(false); bar.RefreshNow();
        Has(bar, BuffDurationIcon.RoyalCommand, SkillPageArt.GetAbilitySprite("royal_command_2"), .5f);
        Check(Near(ai.RoyalCommandBuffRemaining, 2.5f), "Streaming-inactive recipient retains authoritative effect");
        recipient.gameObject.SetActive(true); Set(ai, "royalCommandRemaining", 0f); bar.RefreshNow(); Hidden(bar, BuffDurationIcon.RoyalCommand);
        Set(ai, "royalCommandRemaining", 5f); var dead = recipient.CaptureRuntimeState(); dead.currentHealth = 0; recipient.ApplyRuntimeState(dead);
        bar.RefreshNow(); Hidden(bar, BuffDurationIcon.RoyalCommand);
        Check(Near(ai.RoyalCommandBuffRemaining, 0), "Dead recipient cannot leave command buff visible");
        Reset(recipient); Set(ai, "royalCommandRemaining", 0f);

        // Every skill has now allocated its single sprite child. Warm refreshes must be allocation-stable.
        int objects = bar.GetComponentsInChildren<Transform>(true).Length;
        var stable = bar.GetIcon(BuffDurationIcon.CombatExpertise);
        for (int i = 0; i < 50; i++) bar.RefreshNow(); bar.Build(properties, slots);
        Check(bar.transform.childCount == 16 && objects == bar.GetComponentsInChildren<Transform>(true).Length &&
            stable == bar.GetIcon(BuffDurationIcon.CombatExpertise), "Repeated refresh and Build reuse tiles and existing sprite children");

        Reset(actor); state = actor.CaptureRuntimeState(); state.combatExpertiseRemaining = .12f;
        state.combatExpertiseRegeneration = true; state.ghostFormRemaining = .12f; state.ghostFormDuration = 7;
        actor.ApplyRuntimeState(state); bar.RefreshNow(); Check(bar.VisibleCount == 2, "Restored near-expiry effects both appear");
        Time.timeScale = 1; float deadline = Time.time + .3f; while (Time.time < deadline) yield return null;
        Check(bar.VisibleCount == 0 && !actor.IsCombatExpertiseActive && !actor.IsGhostForm, "Both real timers expire and disappear without manual HUD refresh");
        Time.timeScale = 0;

        // Exercise actual main-HUD construction and independent per-frame duration updates.
        var hud = ZeldaHealthHeartsUI.Instance;
        if (hud == null)
        {
            var go = new GameObject("Production skill HUD wiring", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.SetActive(false); hud = go.AddComponent<ZeldaHealthHeartsUI>();
            Set(hud, "persistBetweenScenes", false); Set(hud, "permissionLabelFont", font); go.SetActive(true);
        }
        var actual = hud.transform.Find("Buff Durations").GetComponent<RuntimeBuffDurationBar>();
        Check(actual != null && actual.isActiveAndEnabled, "Main HUD owns live independent buff strip");
        hud.enabled = false; Reset(actor); Check(actor.TryUseCombatExpertise(), "Native/learned skill casts with main HUD sleeping");
        CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Might);
        yield return null; yield return null;
        Has(actual, BuffDurationIcon.CombatExpertise, SkillPageArt.GetAbilitySprite("combat_expertise_2"), 1);
        Check(actual.VisibleCount == 2 && !hud.enabled && actual.GetIcon(BuffDurationIcon.Might).gameObject.activeSelf,
            "Skill plus potion update while event-driven health HUD component sleeps");
        state = actor.CaptureRuntimeState(); state.currentHealth = 0; actor.ApplyRuntimeState(state); actual.RefreshNow();
        Check(actual.VisibleCount == 0, "Dead controlled body's skill and potion effects are hidden");
        Reset(actor); mover.enabled = false; actual.RefreshNow();
        Check(actual.VisibleCount == 0 && actual.DisplayedCharacter == null, "Missing controlled body clears all skill tiles");
        hud.gameObject.SetActive(false); canvas.gameObject.SetActive(false);
        var preview = Preview(font); while (preview.MoveNext()) yield return preview.Current;
        Time.timeScale = 1;
    }

    static void Label(Transform parent, Font font, string text, Vector2 position, Vector2 size, int fontSize = 23)
    {
        var label = Rect(text, parent, position, size).gameObject.AddComponent<Text>();
        label.font = font; label.fontSize = fontSize; label.text = text; label.raycastTarget = false;
        label.color = new Color32(95, 248, 233, 255); label.alignment = TextAnchor.MiddleLeft;
    }
    static BuffDurationIconGraphic PreviewIcon(Transform parent, int skill, Vector2 position, float ratio, float size = 88)
    {
        var graphic = Rect(Skills[skill].ToString(), parent, position, Vector2.one * size).gameObject.AddComponent<BuffDurationIconGraphic>();
        graphic.SetSkillDisplay(Skills[skill], SkillPageArt.GetAbilitySprite(SkillIds[skill]), ratio, skill < 3); return graphic;
    }
    static IEnumerator Preview(Font font)
    {
        const int width = 1600, height = 790;
        var canvasRect = Rect("Skill Buff Visual Preview", null, Vector2.zero, new Vector2(width, height));
        canvasRect.pivot = Vector2.zero; canvasRect.position = Vector3.zero;
        var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 32760;
        var backdrop = Rect("Preview backdrop", canvasRect, Vector2.zero, new Vector2(width, height)).gameObject.AddComponent<Image>();
        backdrop.color = new Color32(7, 17, 42, 255); backdrop.raycastTarget = false;
        Label(canvasRect, font, "SKILL BUFFS / ORIGINAL SKILL SPRITES", new Vector2(40, -25), new Vector2(1500, 48), 32);
        Label(canvasRect, font, "Exact rank-aware skill art | timed effects deplete clockwise | marks remain until removed", new Vector2(40, -83), new Vector2(1500, 42));
        for (int i = 0; i < Skills.Length; i++)
        {
            float x = 60 + i * 307;
            var icon = PreviewIcon(canvasRect, i, new Vector2(x, -157), .75f, 112);
            Label(canvasRect, font, Skills[i].ToString(), new Vector2(x, -282), new Vector2(290, 36));
            Label(canvasRect, font, i < 3 ? "75% remaining" : "UNTIMED", new Vector2(x, -321), new Vector2(285, 36), 20);
            Check(icon.SkillSprite == SkillPageArt.GetAbilitySprite(SkillIds[i]) && icon.GetComponentsInChildren<Image>().Length == 1,
                Skills[i] + " GPU preview uses production sprite child");
            using (var vh = new VertexHelper())
            {
                typeof(BuffDurationIconGraphic).GetMethod("OnPopulateMesh", Flags, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(icon, new object[] { vh });
                Check(vh.currentVertCount > 0 && vh.currentVertCount < 4096, Skills[i] + " renders bounded backing/timer mesh");
                if (i >= 3) Check(vh.currentVertCount == 20, Skills[i] + " untimed mesh has only backdrop and outer frame, no fake countdown band");
            }
        }
        Label(canvasRect, font, "TIMER: top-left start, then clockwise along the square perimeter", new Vector2(40, -396), new Vector2(1500, 42), 25);
        float[] ratios = { 1, .875f, .75f, .5f, .25f, 0 };
        for (int i = 0; i < ratios.Length; i++)
        {
            float x = 60 + i * 255;
            PreviewIcon(canvasRect, 1, new Vector2(x, -464), ratios[i], 112);
            Label(canvasRect, font, (ratios[i] * 100).ToString("0.#") + "% remaining", new Vector2(x, -589), new Vector2(235, 36), 22);
        }
        Label(canvasRect, font, "IN-GAME SCALE", new Vector2(40, -698), new Vector2(250, 40));
        for (int i = 0; i < Skills.Length; i++) PreviewIcon(canvasRect, i, new Vector2(310 + i * 46, -699), .75f, 40);
        var potion = Rect("Existing potion", canvasRect, new Vector2(310 + 5 * 46, -699), Vector2.one * 40).gameObject.AddComponent<BuffDurationIconGraphic>();
        potion.SetDisplay(BuffDurationIcon.Might, .5f);
        Label(canvasRect, font, "40 px / shared strip / original icons / stun excluded", new Vector2(630, -698), new Vector2(910, 40), 22);
        var camera = new GameObject("Skill buff preview camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(width * .5f, height * .5f, -10);
        camera.orthographic = true; camera.orthographicSize = height * .5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(7, 17, 42, 255); canvas.worldCamera = camera;
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "SkillBuffHud-preview.png"), image.EncodeToPNG());
        int cyan = 0; foreach (var color in image.GetPixels()) if (color.g > .6f && color.b > .5f && color.r < .8f) cyan++;
        Check(cyan > 5000, "GPU preview contains visible original skill art and perimeter timers");
        for (int i = 0; i < Skills.Length; i++)
        {
            int centerCyan = 0;
            // Restrict sampling to the sprite interior, excluding text, outer frame and timer band.
            for (int y = 187; y < 239; y++) for (int x = 90 + i * 307; x < 142 + i * 307; x++)
            {
                Color pixel = image.GetPixel(x, height - 1 - y);
                if (pixel.g > .6f && pixel.b > .5f && pixel.r < .8f) centerCyan++;
            }
            Check(centerCyan > 25, Skills[i] + " original sprite is GPU-visible inside its tile");
        }
        RenderTexture.active = old; camera.targetTexture = null; target.Release();
        UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(canvasRect.gameObject); UnityEngine.Object.Destroy(camera.gameObject);
    }
}
