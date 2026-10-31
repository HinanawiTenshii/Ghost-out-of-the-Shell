using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ItemDescriptionLayoutSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static ItemDescriptionLayoutSmoke() { EditorApplication.update += Tick; }
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, F).GetValue(owner);
    static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, F).Invoke(owner, args);
    static PersistentInventory.Slot Slot(string id, string title = "", string description = "")
    {
        var slot = new PersistentInventory.Slot();
        typeof(PersistentInventory.Slot).GetMethod("Set", F).Invoke(slot, new object[] { id, 1, "", title, description, false, Color.white, false, 0f, "" });
        return slot;
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "item-description-layout-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(ItemDescriptionLayoutSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(ItemDescriptionLayoutSmoke), false))
            { SessionState.SetBool(nameof(ItemDescriptionLayoutSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Description test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("ITEM_DESCRIPTION_LAYOUT_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 1;
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
        Check(font != null, "project Chinese pixel font available");
        var bomb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PickupItems/Bombs/BombPickupItem.prefab").GetComponent<PickupItemBase>();
        var window = ItemDescriptionWindow.GetOrCreate();
        Check(window.OpenForSlot(Slot(bomb.ItemId)), "manual opening works");
        yield return null;
        var heading = Get<Text>(window, "headingText");
        var title = Get<Text>(window, "nameText");
        var text = Get<Text>(window, "descriptionText");
        var canvas = Get<Canvas>(window, "windowCanvas");
        var icons = Get<System.Collections.Generic.List<Image>>(window, "requirementIcons");
        Check(heading.text == "物品详情" && title.text == bomb.ItemName, "neutral heading and item title");
        Check(canvas.GetComponent<RuntimeUiGlowScope>() != null && title.color == ZeldaUiPalette.Primary, "existing CRT glow and palette");
        Check(canvas.GetComponentsInChildren<Text>(true).All(t => t.font == font), "all labels use actual game font");
        Check(Get<Image>(window, "iconImage").sprite != null, "actual inventory icon, not placeholder");
        Check(icons.Count(i => i.gameObject.activeSelf) == bomb.RequiredUseSkill && icons.All(i => i.color == ZeldaUiPalette.Primary), "prefab requirement represented by cyan HUD wrenches");
        Check(!text.text.Contains("使用需要技巧值：") && bomb.ItemDescription.Contains("使用需要技巧值："), "only detail window omits generated requirement footer");
        Check(Time.timeScale == 0 && DocumentReader.IsInputBlocked, "modal still pauses and blocks gameplay inventory scrolling");
        Check(Get<float>(window, "scrollMaximum") == 0 && !Get<Text>(window, "scrollHintText").gameObject.activeSelf, "short descriptions have no unnecessary scrollbar");
        Capture(window, "ItemDescription-default.png");
        window.CloseWindow(); Check(Time.timeScale == 1, "close restores time scale");
        yield return null;
        Check(!ItemDescriptionWindow.BlocksInput, "input block expires on next frame");

        string custom = string.Join("\n", Enumerable.Range(1, 50).Select(i => "第" + i + "段：这是保存在物品中的长篇说明，滚轮可继续阅读。")) + "\n说明结束。\n使用需要技巧值：99。";
        var oldSlot = Slot(bomb.ItemId, "带有较长说明的炸弹", custom);
        Check(window.OpenForSlot(oldSlot), "old save/custom description opens");
        Check(oldSlot.ItemDescription == custom && !text.text.Contains("技巧值：99") && text.text.Contains("说明结束"), "legacy footer stripped without mutating saved slot or truncating prose");
        Check(Get<float>(window, "scrollMaximum") > 500 && Get<RectTransform>(window, "descriptionViewport").GetComponent<RectMask2D>() != null, "long text measured and masked");
        var blocks = Get<System.Collections.Generic.List<Text>>(window, "descriptionBlocks");
        Check(!text.enabled && blocks.Count > 1 && blocks.All(b => b.text.Length <= 256 && b.GetComponent<TitleScreenGlow>() != null), "long descriptions retain glow within vertex limits");
        Check(string.Concat(blocks.Where(b => b.gameObject.activeSelf).Select(b => b.text)) == text.text, "wrapped-line chunks preserve every character");
        var titlePosition = title.rectTransform.anchoredPosition;
        var requirementPosition = icons[0].rectTransform.anchoredPosition;
        Call(window, "ScrollDescription", -1f);
        Check(Mathf.Approximately(Get<float>(window, "scrollOffset"), 48f), "wheel scroll works while timeScale is zero");
        Call(window, "ScrollDescription", -9999f);
        Check(Get<float>(window, "scrollOffset") == Get<float>(window, "scrollMaximum"), "bottom clamps to end without overscroll");
        Check(title.rectTransform.anchoredPosition == titlePosition && icons[0].rectTransform.anchoredPosition == requirementPosition, "title and requirements remain fixed while reading");
        yield return null;
        Capture(window, "ItemDescription-scrolled.png");
        Check(Mathf.Approximately(Get<float>(window, "scrollOffset"), Get<float>(window, "scrollMaximum")), "resolution/camera scale changes preserve bottom scroll position");
        var lastBlock = blocks.Last(b => b.gameObject.activeSelf);
        Check(lastBlock.text.EndsWith("说明结束。"), "last block includes final line");
        float lastBottom = -lastBlock.rectTransform.anchoredPosition.y + lastBlock.rectTransform.rect.height - Get<float>(window, "scrollOffset");
        Check(lastBottom <= Get<RectTransform>(window, "descriptionViewport").rect.height + 1f, "last line is inside the viewport at end");
        Call(window, "ScrollDescription", 9999f);
        Check(Get<float>(window, "scrollOffset") == 0, "top clamps correctly");
        window.CloseWindow();
        Check(window.OpenForSlot(oldSlot) && Get<float>(window, "scrollOffset") == 0, "reopening starts at top");
        window.CloseWindow();

        var templates = SaveGameCatalog.LoadPickupTemplates();
        var noRequirement = templates.First(t => t != null && t.RequiredUseSkill == 0);
        Check(window.OpenForSlot(Slot(noRequirement.ItemId)), "zero requirement item opens");
        Check(Get<Text>(window, "requirementCount").text == "×0" && icons.Count(i => i.gameObject.activeSelf) == 1 && icons[0].color == ZeldaUiPalette.Ghost, "zero requirement has unambiguous dark-blue icon and zero");
        window.CloseWindow();
        Call(window, "ShowRequirements", 999);
        Check(icons.Count(i => i.gameObject.activeSelf) == 1 && Get<Text>(window, "requirementCount").text == "×999", "large authorable requirements stay bounded");
        var puppet = templates.First(t => t is ClockworkPuppetPickupItem);
        Check(window.OpenForSlot(Slot(puppet.ItemId)), "puppet opens");
        Check(text.text.Contains(CharacterSkillRequirements.PryDescription) && !text.text.Contains("使用需要技巧值："), "additional lockpicking rules retained");
        window.CloseWindow();

        Check(window.OpenForSlot(Slot("test_missing_template", "旧版物品", "保留的原始内容。\n使用需要技巧值：4。")), "missing prefab still shows saved description");
        Check(text.text.Contains("技巧值：4") && Get<Text>(window, "requirementCount").text == "—", "unknown requirement is not misrepresented as zero");
        window.CloseWindow();
        ItemDescriptionWindow.ApplyDiscoveryHistory(new[] {bomb.DescriptionDiscoveryId});
        Check(window.OpenForSlot(Slot(bomb.ItemId)), "already-discovered item still manually opens");
        Check(ItemDescriptionWindow.CaptureDiscoveryHistory().Contains(bomb.DescriptionDiscoveryId), "UI change preserves discovery history");
        window.CloseWindow();

        Time.timeScale = .5f;
        Check(window.OpenForSlot(Slot(bomb.ItemId)), "opening during custom game speed");
        var texture = Get<Texture2D>(window, "wrenchTexture");
        Object.Destroy(window.gameObject); yield return null;
        Check(Mathf.Approximately(Time.timeScale, .5f) && texture == null, "destroy restores time and releases owned icon");
        Time.timeScale = 1;
    }
    static void Capture(ItemDescriptionWindow window, string filename)
    {
        var canvas = Get<Canvas>(window, "windowCanvas");
        var camera = new GameObject("UI screenshot camera").AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 360;
        camera.transform.position = new Vector3(0,0,-20); camera.backgroundColor = new Color(.025f,.035f,.055f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(1280,720,24); camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2;
        Canvas.ForceUpdateCanvases(); Call(window, "FitPanelToCanvas"); Call(window, "RebuildDescriptionLayout", false); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../"+filename),image.EncodeToPNG());
        RenderTexture.active = previous; canvas.worldCamera = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        camera.targetTexture = null; Object.Destroy(rt); Object.Destroy(image); Object.Destroy(camera.gameObject);
    }
}
