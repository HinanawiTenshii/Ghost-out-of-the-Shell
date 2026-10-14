using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run only in the isolated validation project. No scene/prefab saves.
[InitializeOnLoad]
public static class CharacterStatsWindowSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int frame = -1, checks;
    static CharacterStatsWindowSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "character-stats-skill-discovery-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("CharacterStatsWindowSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("CharacterStatsWindowSmoke", false))
            { SessionState.SetBool("CharacterStatsWindowSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Debug.Log("CHARACTER_STATS_WINDOW_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Step(ZeldaCharacterStatsWindow w, bool visible, float dt) => Call(w, "UpdateWindow", visible, dt);
    static void ValidateRows(ZeldaCharacterStatsWindow w, ZeldaCharacterData d)
    {
        var attack = Get<Image[]>(w, "attackIcons"); var skill = Get<Image[]>(w, "skillIcons");
        var energy = Get<Image[]>(w, "costIcons"); var requirement = Get<Image[]>(w, "requirementIcons");
        for (int i = 0; i < 10; i++)
        {
            Check(attack[i].enabled == (i < Mathf.Clamp(d.FinalAttackPower, 0, 10)), "attack count");
            Check(skill[i].enabled == (i < Mathf.Clamp(d.SkillValue, 0, 10)), "skill count");
            Check(requirement[i].enabled == (i < Mathf.Clamp(d.PossessionCost, 0, 10)), "requirement count");
            Check(energy[i].enabled == (i < Mathf.Clamp(d.PossessionEnergyDisplaySlotCount, 0, 10)), "energy slot count");
            bool crystal = d.IsCrystalPossessionEnergyDisplaySlot(i);
            Check(energy[i].type == (crystal ? Image.Type.Filled : Image.Type.Simple), "temporary energy type");
            Check(Mathf.Approximately(energy[i].fillAmount, crystal ? d.GetPossessionEnergyDisplayFill(i) : 1f), "energy fill");
        }
        Check(Get<Image>(w, "permissionImage").sprite.name.EndsWith(" " + d.PermissionLevel), "permission digit");
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var cameraGo = new GameObject("Stats test camera", typeof(Camera)); cameraGo.tag = "MainCamera";
        var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 7;
        cameraGo.transform.position = new Vector3(0, 0, -10);
        var actor = new GameObject("Stats test actor");
        var data = actor.AddComponent<SwordZeldaCharacterData>();
        var mover = actor.AddComponent<ZeldaFourWayMover>(); mover.enabled = false;
        var w = actor.GetComponentInChildren<ZeldaCharacterStatsWindow>();
        w.Configure(data, new Vector2(0, 1), 2.2f);
        var overlay = Get<GameObject>(w, "overlayObject"); var panel = Get<RectTransform>(w, "panelRect");
        var alpha = Get<CanvasGroup>(w, "canvasGroup"); var canvas = Get<Canvas>(w, "overlayCanvas");
        var skill = Get<Image[]>(w, "skillIcons");
        Check(!overlay.activeSelf && alpha.alpha == 0, "initially hidden");
        Check(canvas.sortingOrder == 31100, "unchanged sorting order: " + canvas.sortingOrder);
        Check((bool)Call(w, "IsCharacterInsideCameraVision"), "in-camera eligibility");
        actor.transform.position = new Vector3(100, 100, 0);
        Check(!(bool)Call(w, "IsCharacterInsideCameraVision"), "offscreen eligibility");
        actor.transform.position = new Vector3(1, 2, 0);

        // Sentinel writes prove hidden ticks do not touch images or transforms.
        panel.anchoredPosition = new Vector2(-901, -902); skill[0].color = Color.magenta;
        var state = data.CaptureRuntimeState(); state.skillValue = 7; state.permissionLevel = -1;
        state.possessionCost = 4; state.possessionEnergy = 3; state.spentBasePossessionEnergy = 1; state.crystalEnergyThirds = 2;
        data.ApplyRuntimeState(state);
        int dirty = 0; skill[0].RegisterDirtyVerticesCallback(() => dirty++);
        for (int i = 0; i < 120; i++) { Step(w, false, .016f); Call(w, "LateUpdate"); }
        Check(dirty == 0, "hidden ticks do not dirty UI");
        Check(panel.anchoredPosition == new Vector2(-901, -902), "hidden position skipped");
        Check(skill[0].color == Color.magenta, "hidden icons skipped");
        Check(!overlay.activeSelf, "hidden canvas remains disabled");

        Step(w, true, .03f);
        Check(overlay.activeSelf && Mathf.Approximately(alpha.alpha, .25f), "first quarter fade-in, unscaled");
        Check(panel.anchoredPosition != new Vector2(-901, -902), "position refreshed before first display");
        Check(skill[0].color != Color.magenta, "palette restored on opening"); ValidateRows(w, data);
        Step(w, true, .09f); Check(alpha.alpha == 1f, "original .12 second fade-in");
        Vector2 previous = panel.anchoredPosition;
        actor.transform.position += Vector3.right;
        state.skillValue = 2; state.permissionLevel = 5; state.possessionEnergy = 1; state.spentBasePossessionEnergy = 3;
        state.crystalEnergyThirds = 1; data.ApplyRuntimeState(state);
        Step(w, true, 0);
        Check(panel.anchoredPosition != previous, "visible window follows movement"); ValidateRows(w, data);

        Step(w, false, .03f); Check(overlay.activeSelf && Mathf.Approximately(alpha.alpha, .75f), "fade-out is not skipped");
        previous = panel.anchoredPosition; actor.transform.position += Vector3.left;
        Step(w, false, .03f); Check(panel.anchoredPosition != previous && Mathf.Approximately(alpha.alpha, .5f), "fading window still follows");
        Step(w, true, .03f); Check(Mathf.Approximately(alpha.alpha, .75f), "reopen reverses fade smoothly");
        Step(w, false, .12f); Check(!overlay.activeSelf && alpha.alpha == 0, "fade completion disables canvas");
        previous = panel.anchoredPosition; actor.transform.position += Vector3.up;
        Step(w, false, 1); Check(panel.anchoredPosition == previous, "completed fade returns to idle fast path");

        // Streaming disables the owner, not its independently rooted overlay.
        Step(w, true, .12f); actor.SetActive(false);
        Check(!overlay.activeSelf && alpha.alpha == 0 && !Get<bool>(w, "isVisible"), "streaming hides independent overlay");
        actor.SetActive(true); Check(!overlay.activeSelf, "reload starts hidden");
        Step(w, true, .12f); ValidateRows(w, data);
        w.enabled = false; Check(!overlay.activeSelf, "component disable hides panel");
        w.enabled = true; Step(w, true, .12f);
        Call(w, "LateUpdate"); Check(!Get<bool>(w, "isVisible"), "released real input hides window");

        Check(Get<GameObject>(w, "skillDiscoveryBadge") == null, "ordinary NPC creates no discovery graphics");
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        var nativeSkill = actor.AddComponent<CombatExpertiseCharacterSkillData>();
        Step(w, true, .12f);
        var badge = Get<GameObject>(w, "skillDiscoveryBadge");
        var badgeIcon = Get<Image>(w, "skillDiscoveryIcon");
        Check(badge != null && badge.activeInHierarchy, "undiscovered combat skill has indicator");
        Check(badgeIcon.sprite == data.CharacterSkillIcon && badgeIcon.color == ZeldaUiPalette.Primary, "uses native skill glyph and cyan palette");
        Check(badge.GetComponentsInChildren<Text>(true).Length == 0, "indicator is entirely graphical, no added text");
        Check(badge.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "indicator cannot intercept possession/mouse input");
        Check(badge.transform.Find("New Skill Mark/Plus Glyph") != null, "new-skill plus symbol present");
        Check(badge.transform.parent == panel && panel.sizeDelta == new Vector2(276, 156), "original panel layout preserved");
        int badgeDirty = 0;
        foreach (var graphic in badge.GetComponentsInChildren<Graphic>()) graphic.RegisterDirtyVerticesCallback(() => badgeDirty++);
        for (int i = 0; i < 120; i++) Step(w, true, .016f);
        Check(badgeDirty == 0, "unchanged visible badge does not dirty canvas each frame");
        Preview(w, "Combat");

        growth.RecordPossessedCharacter(data); Step(w, true, .016f);
        Check(!badge.activeSelf && !growth.HasSkill("combat_expertise"), "discovery hides indicator before skill-point purchase");
        var discovered = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(default); Step(w, true, .016f);
        Check(badge.activeSelf && Get<GameObject>(w, "skillDiscoveryBadge") == badge, "reset reuses indicator without recreating it");
        growth.ApplyRuntimeState(discovered); Step(w, true, .016f);
        Check(!badge.activeSelf, "loaded discovery progress suppresses stale new-skill mark");
        growth.ApplyRuntimeState(new PlayerGrowthAttributes.RuntimeState { unlockedSkills = new[] { "combat_expertise" } });
        Step(w, true, .016f); Check(!badge.activeSelf, "legacy learned skill without discovery history also suppresses mark");
        Step(w, false, .12f); growth.ApplyRuntimeState(default);
        for (int i = 0; i < 30; i++) Step(w, false, .016f);
        Check(!badge.activeSelf, "hidden windows do not update badge graphics");
        Step(w, true, .12f); Check(badge.activeSelf, "opening refreshes progress changed while hidden");
        actor.SetActive(false); Check(!badge.activeInHierarchy, "streaming hides the indicator with the window");
        actor.SetActive(true); Step(w, true, .12f);
        Check(badge.activeInHierarchy, "streaming reload preserves discovery indicator");
        var alive = data.CaptureRuntimeState(); var dead = alive; dead.currentHealth = 0;
        data.ApplyRuntimeState(dead); Step(w, true, .016f); Check(!badge.activeSelf, "dead character never advertises learnable skill");
        data.ApplyRuntimeState(alive);
        Object.Destroy(nativeSkill); yield return null;
        Step(w, true, .12f); Check(!badge.activeSelf, "removing native skill removes discovery mark");
        actor.AddComponent<RoyalCommandCharacterSkillData>(); Step(w, true, .016f);
        Check(badge.activeSelf && badgeIcon.sprite == ZeldaCharacterData.CommandIcon, "royal command uses its own glyph");
        Preview(w, "Command");
        growth.RecordPossessedCharacter(data); Step(w, true, .016f);
        Check(!badge.activeSelf, "royal command discovery hides mark");

        var beast = new GameObject("Native roar owner").AddComponent<BehemothZeldaCharacterData>();
        var beastWindow = beast.GetComponentInChildren<ZeldaCharacterStatsWindow>();
        beastWindow.Configure(beast, Vector2.zero, 2.2f); Step(beastWindow, true, .12f);
        Check(Get<GameObject>(beastWindow, "skillDiscoveryBadge").activeSelf && Get<Image>(beastWindow, "skillDiscoveryIcon").sprite == beast.CharacterSkillIcon, "virtual native roar works without optional skill component");
        Preview(beastWindow, "Roar");
        growth.RecordPossessedCharacter(beast); Step(beastWindow, true, .016f);
        Check(!Get<GameObject>(beastWindow, "skillDiscoveryBadge").activeSelf, "roar discovery hides mark");
        Object.Destroy(beast.gameObject);

        var empty = new GameObject("Unconfigured window").AddComponent<ZeldaCharacterStatsWindow>();
        empty.enabled = false; Check(!empty.enabled, "unconfigured disable is safe");
        Object.Destroy(empty.gameObject); Object.Destroy(actor);
        yield return null;
        Check(overlay == null, "destroy removes overlay");
    }
    static void Preview(ZeldaCharacterStatsWindow window, string suffix)
    {
        var copy = Object.Instantiate(Get<GameObject>(window, "overlayObject"));
        copy.GetComponent<CanvasScaler>().enabled = false;
        var canvas = copy.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)copy.transform; rect.sizeDelta = new Vector2(360, 280);
        rect.position = Vector3.zero; rect.rotation = Quaternion.identity; rect.localScale = Vector3.one * .01f;
        var panel = (RectTransform)copy.transform.Find("Character Attribute Panel");
        panel.anchoredPosition = new Vector2(0, -24); panel.localScale = Vector3.one;
        copy.GetComponent<CanvasGroup>().alpha = 1;
        foreach (var child in copy.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
        foreach (var label in copy.GetComponentsInChildren<Text>()) label.font = font;
        var cam = new GameObject("Skill Badge Preview").AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = 1.45f;
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(13, 20, 38, 255);
        var rt = new RenderTexture(640, 480, 24); cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
        var image = new Texture2D(640, 480, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath + "/../SkillDiscovery-" + suffix + ".png", image.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.Destroy(image); Object.Destroy(rt); Object.Destroy(cam.gameObject); Object.Destroy(copy);
    }
}
