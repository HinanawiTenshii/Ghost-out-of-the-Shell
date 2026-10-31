using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TitleTerminalSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator routine;
    static int frame = -1, checks;
    static double deadline;
    static TitleTerminalSmoke() { EditorApplication.update += Tick; }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "terminal-title-smoke-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool(nameof(TitleTerminalSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TitleTerminalSmoke), false))
            {
                SessionState.SetBool(nameof(TitleTerminalSmoke), false);
                routine = Verify(); deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (routine == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("terminal test timeout");
            if (!routine.MoveNext()) { routine = null; Debug.Log("TITLE_TERMINAL_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception ex) { routine = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        var title = Object.FindObjectOfType<TitleScreenController>();
        Check(title != null, "authored real title exists");
        var input = Field<CanvasGroup>(title, "terminalInput");
        var chrome = Field<CanvasGroup>(title, "terminalFrame");
        var rows = Field<CanvasGroup[]>(title, "terminalRows");
        var lines = Field<CanvasGroup[]>(title, "terminalTitleLines");
        var menu = Field<RectTransform>(title, "mainOptionsRoot");
        var buttons = menu.GetComponentsInChildren<Button>();
        Check(!input.interactable && rows.All(r => r.alpha == 0), "boot hides and locks all menu rows immediately");
        buttons[0].onClick.Invoke();
        Check(Field<RectTransform>(title, "progressOptionsRoot") == null, "early synthetic click cannot open a submenu");
        while (RetroSceneLoadReveal.IsBlockingInput) { Check(chrome.alpha == 0, "boot waits for scene curtain"); yield return null; }
        Time.timeScale = 0f;
        float start = Time.realtimeSinceStartup;
        bool sawFrame = false, sawPartialRows = false, sawUnfold = false, sawTitleBand = false, sawWriting = false;
        while (!Field<bool>(title, "terminalReady"))
        {
            sawFrame |= chrome.alpha == 1 && lines[0].alpha == 0;
            sawPartialRows |= rows[0].alpha == 1 && rows[4].alpha == 0;
            sawUnfold |= chrome.alpha == 1 && ((RectTransform)chrome.transform).rect.height > 8 && ((RectTransform)chrome.transform).rect.height < 628;
            sawTitleBand |= Field<RectMask2D[]>(title, "titleRevealMasks")[0].enabled && lines[0].alpha == 1;
            string partial = buttons[0].GetComponentInChildren<Text>().text;
            sawWriting |= partial.Length > 0 && partial.Length < "开始新游戏".Length;
            Check(!input.interactable && !input.blocksRaycasts, "input disabled throughout staged reveal");
            yield return null;
        }
        Check(Time.realtimeSinceStartup - start >= 2.15f && Time.realtimeSinceStartup - start < 4f, "extended unscaled boot remains about 2.3 seconds");
        Check(sawFrame && sawPartialRows, "frame/title/menu appear as discrete batches");
        Check(sawUnfold && sawTitleBand && sawWriting, "window unfolds, title bands reveal, menu text writes in batches");
        Check(Field<RectMask2D[]>(title, "titleRevealMasks").All(m => !m.enabled), "ready screen removes reveal clipping so halos are not cut off");
        Check(buttons[0].GetComponent<RectTransform>().sizeDelta == new Vector2(400,48) && lines[0].GetComponent<Text>().fontSize == 168, "more balanced title and menu proportions");
        Check(Field<float>(lines[0].GetComponent<TitleScreenGlow>(), "haloIntensityScale") == 1.75f, "stronger local title halo");
        Check(Field<float>(buttons[0].GetComponentInChildren<Text>().GetComponent<TitleScreenGlow>(), "haloRadiusScale") == 1.1f, "small text gets a tighter halo for readability");
        Check(lines.All(l => l.alpha == 1) && rows.All(r => r.alpha == 1), "all elements visible when ready");
        Check(EventSystem.current.currentSelectedGameObject == buttons[0].gameObject, "first usable command initially selected");
        Check(buttons[0].GetComponent<Image>().canvasRenderer.GetColor() == Color.white, "startup group does not leave stale disabled renderer tint");
        Check(buttons.Length == 5 && !buttons[1].interactable && !buttons[3].interactable, "original actions and save availability retained");
        Check(title.GetComponentsInChildren<Text>().All(t => t.text != "主界面" && !t.text.Contains("ENTER") && !t.text.Contains("READY")), "no redundant header/footer/status words");
        Check(title.GetComponent<CanvasScaler>().screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, "aspect-safe layout");
        foreach (var label in title.GetComponentsInChildren<Text>().Where(t => !string.IsNullOrEmpty(t.text)))
            Debug.Log("TERMINAL_LABEL " + label.text + " preferred=" + label.preferredWidth + " rect=" + label.rectTransform.rect.width);
        var source = Field<AudioSource>(title, "terminalAudio");
        foreach (string name in new[] { "terminalBootSound", "terminalMoveSound", "terminalConfirmSound" })
        {
            var clip = Field<AudioClip>(title, name);
            Check(clip != null && clip.channels == 1 && clip.length <= 2.01f, "bounded original audio: " + name);
            clip.LoadAudioData(); var samples = new float[clip.samples];
            Check(clip.GetData(samples, 0) && samples.Max(s => Mathf.Abs(s)) < .19f, "quiet source amplitude, normalization disabled");
        }
        Check(source.spatialBlend == 0 && source.volume <= .3f && !source.loop, "quiet single UI voice");
        buttons[2].GetComponent<TitleTerminalOption>().OnPointerEnter(new PointerEventData(EventSystem.current));
        Check(EventSystem.current.currentSelectedGameObject == buttons[2].gameObject, "mouse selects command");
        Check(buttons[2].GetComponent<Image>().color.a == 1 && buttons[0].GetComponent<Image>().color.a == 0, "exactly one inverted selection bar");
        Check(buttons[2].GetComponentInChildren<Text>().color.maxColorComponent < .2f, "inverted label stays dark");
        Check(source.clip == Field<AudioClip>(title, "terminalMoveSound"), "selection plays navigation cue");
        buttons[3].GetComponent<TitleTerminalOption>().OnPointerEnter(new PointerEventData(EventSystem.current));
        Check(EventSystem.current.currentSelectedGameObject == buttons[2].gameObject, "disabled command cannot steal focus");
        var move = new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down, moveVector = Vector2.down };
        ExecuteEvents.Execute(buttons[2].gameObject, move, ExecuteEvents.moveHandler);
        Check(EventSystem.current.currentSelectedGameObject == buttons[4].gameObject, "keyboard skips unavailable settings");
        buttons[0].Select();
        Capture(title, 1280, 720, "main-1280.png");
        Capture(title, 1920, 1080, "main-1920.png");
        Capture(title, 1024, 768, "main-4x3.png");
        Capture(title, 1920, 810, "main-wide.png");
        buttons[0].onClick.Invoke();
        yield return null;
        Check(!menu.gameObject.activeSelf && Field<RectTransform>(title, "progressOptionsRoot").gameObject.activeSelf, "new game still opens profile choice");
        Check(source.clip == Field<AudioClip>(title, "terminalConfirmSound"), "confirmation cue overrides move cue");
        Capture(title, 1280, 720, "new-game-slots.png");
        title.ShowMainMenu();
        Check(menu.gameObject.activeSelf && Field<bool>(title, "terminalReady") && rows.All(r => r.alpha == 1), "return does not replay boot");
        buttons[2].onClick.Invoke(); yield return null;
        Capture(title, 1280, 720, "profiles.png");
        Check(Field<RectTransform>(title, "progressOptionsRoot").GetComponentsInChildren<Button>().Count(b => b.interactable) == 1, "empty profiles remain disabled, back works");
        title.ShowMainMenu();
        // Deterministic startup snapshots, no scene/prefab changes.
        foreach (float time in new[] { .0f, .20f, .50f, .85f, 1.10f, 1.43f, 1.68f, 2.0f })
        {
            Invoke(title, "RenderTerminalBoot", time);
            Capture(title, 1280, 720, "boot-" + Mathf.RoundToInt(time * 100) + ".png");
        }
        Invoke(title, "RenderTerminalBoot", 2.3f);
        Time.timeScale = 1f;
    }
    static void Capture(TitleScreenController title, int width, int height, string file)
    {
        var canvas = title.GetComponent<Canvas>();
        var camera = canvas.worldCamera;
        Check(camera != null, "CRT composite camera assigned");
        var previousTarget = camera.targetTexture;
        var previousClear = camera.clearFlags;
        var rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        Invoke(title.GetComponent<CanvasScaler>(), "Handle");
        Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); camera.Render();
        var corners = new Vector3[4]; Field<CanvasGroup>(title, "terminalInput").GetComponent<RectTransform>().GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var v = camera.WorldToViewportPoint(corner);
            Check(v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1, "frame inside viewport " + width + "x" + height);
        }
        var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        const string folder = "../../.codex-temp/title-terminal-previews";
        Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = previousTarget; camera.clearFlags = previousClear;
        rt.Release(); Object.Destroy(rt); Object.Destroy(texture);
    }
}
