#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TerminalSceneLoadSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Key = "TerminalSceneLoadSmoke";
    static IEnumerator suite;
    static int frame = -1, checks;
    static string runtimeException;
    static double deadline;
    static TerminalSceneLoadSmoke() { EditorApplication.update += Tick; }
    static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, F).Invoke(o, a);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated clone only");
        PlayerSettings.productName = "terminal-scene-refresh-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (suite == null)
            {
                Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception) runtimeException = message; };
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 180;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Test timeout");
            if (suite.MoveNext()) return;
            Check(runtimeException == null, "no runtime exceptions: " + runtimeException);
            SessionState.SetBool(Key, false); Debug.Log("TERMINAL_SCENE_LOAD_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { SessionState.SetBool(Key, false); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        Object.FindObjectOfType<LevelSceneVisualProfile>().gameObject.AddComponent<DevlogDemoSceneSettings>();
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        var reveal = Object.FindObjectOfType<RetroSceneLoadReveal>();
        var style = Resources.Load<TerminalSceneLoadStyle>("TerminalSceneLoadStyle");
        Check(style != null && style.font.name == "ZLabsPixel_12px_M_CN", "same pixel font as gameplay/title");
        Check(style.AreaName("Level2-Floor-1") == "城镇地下" && style.AreaName("Level1-Floor1") == "城堡一楼", "localized destination names");
        Check(style.AreaName("TitleScreen") == "" && style.AreaName("LastScene") == "", "no redundant labels over title/ending");
        foreach (var clip in new[] { style.beginSound, style.scanSound, style.readySound })
        {
            Check(clip != null && clip.channels == 1 && clip.length <= .35f, "small mono audio clip");
            clip.LoadAudioData(); var samples = new float[clip.samples]; clip.GetData(samples, 0);
            Check(samples.Max(x => Mathf.Abs(x)) < .15f, "quiet original samples");
        }
        var audio = (AudioSource)Get(reveal, "transitionAudio");
        Check(audio.spatialBlend == 0f && audio.ignoreListenerPause && audio.volume <= .25f && !audio.loop, "single quiet UI voice independent of pause");
        var bands = (Image[])Get(reveal, "bandImages");
        var scan = (RectTransform)Get(reveal, "scanRoot");
        var overlay = (GameObject)Get(reveal, "canvasObject");
        var status = (GameObject)Get(reveal, "statusRoot");
        var phase = typeof(RetroSceneLoadReveal).GetNestedType("LoadingPhase", F);
        overlay.SetActive(true);
        foreach (bool unveiling in new[] { false, true })
        {
            Set(reveal, "lastScanStep", -1);
            for (int step = 0; step <= bands.Length + 2; step++)
            {
                Call(reveal, "RenderScanStep", unveiling, step);
                for (int row = 0; row < bands.Length; row++)
                {
                    var b = bands[bands.Length - 1 - row];
                    int age = step - row;
                    float expected = unveiling ? (age < 0 ? 1f : age == 0 ? .78f : age == 1 ? .42f : 0f)
                                               : (age < 0 ? 0f : age == 0 ? .42f : age == 1 ? .78f : 1f);
                    Check(b.gameObject.activeSelf == (expected > 0) && (expected == 0 || b.color.a == expected), "top-down quantized phosphor level");
                }
                if (step < bands.Length)
                    Check(Mathf.Abs(scan.anchorMin.y - (1f - (step + 1f) / bands.Length)) < .001f, "scan edge follows row boundary");
            }
        }
        Call(reveal, "ConfigurePresentation", "Level2-Floor-1", false);
        Call(reveal, "ShowFullBlack"); Call(reveal, "BeginLoadingWait");
        Call(reveal, "UpdateLoadingStatus"); Check(!status.activeSelf, "fast load skips status");
        Set(reveal, "waitingStarted", Time.unscaledTime - .7f);
        foreach (string state in new[] { "Reading", "Restoring", "Preparing" })
        {
            Call(reveal, "SetLoadingPhase", Enum.Parse(phase, state)); Call(reveal, "UpdateLoadingStatus");
            Check(status.activeSelf && bands.All(b => b.gameObject.activeSelf && b.color == Color.black), "all waiting phases remain fully opaque");
            Check(!((Text)Get(reveal, "statusLabel")).text.Contains("%"), "truthful status, no fabricated percentage");
            Capture(reveal, 1280, 720, "wait-" + state);
        }
        Capture(reveal, 1024, 768, "wait-4x3"); Capture(reveal, 1920, 810, "wait-wide");
        Call(reveal, "ConfigurePresentation", null, true); Call(reveal, "BeginLoadingWait");
        Set(reveal, "waitingStarted", Time.unscaledTime - 10f); Call(reveal, "UpdateLoadingStatus");
        Check(!status.activeSelf, "local transitions suppress region text even if delayed");
        foreach (bool unveiling in new[] { false, true })
        {
            Set(reveal, "lastScanStep", -1); Call(reveal, "RenderScanStep", unveiling, 8);
            Capture(reveal, 1280, 720, unveiling ? "reveal-half" : "cover-half");
        }
        Call(reveal, "FinishPresentation"); overlay.SetActive(false);
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", F).SetValue(null, false);

        // Time-scale zero is intentional: pause-menu/settlement travel must still finish.
        Time.timeScale = 0f;
        bool called = false; float started = Time.unscaledTime;
        Check(RetroSceneLoadReveal.BeginLocalTransition(() => {
            Check(bands.All(b => b.gameObject.activeSelf && b.color == Color.black), "local callback only after full cover");
            Check(RetroSceneLoadReveal.IsBlockingInput && !EventSystem.current.sendNavigationEvents, "input shield active during callback");
            called = true;
        }), "local transition accepted");
        Check(!RetroSceneLoadReveal.BeginLocalTransition(() => { }), "duplicate transition rejected");
        while (RetroSceneLoadReveal.IsBlockingInput)
        { Check(!status.activeSelf, "local animation never displays region loading text"); yield return null; }
        Check(called && Time.timeScale == 0f && EventSystem.current.sendNavigationEvents, "unscaled transition restores navigation and preserves pause state");
        Check(Time.unscaledTime - started < 1.4f, "no per-band frame waits prolong animation");
        Check(!overlay.activeSelf && !scan.gameObject.activeSelf && !status.activeSelf, "all transient graphics removed");
        Time.timeScale = 1f;
        // Real larger destination: starts hidden, ends only after state + streamer preparation.
        Check(RetroSceneLoadReveal.BeginTransition("Level2-Floor2", () => {
            Check(bands.All(b => b.gameObject.activeSelf && b.color == Color.black), "engine load only after opaque frame");
            RetroSceneLoadReveal.LoadSceneAsync("Level2-Floor2");
        }), "real cross-scene load accepted");
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        Check(SceneManager.GetActiveScene().name == "Level2-Floor2" && Time.timeScale == 1f, "real scene activation and time scale restored");
        Check(!RetroSceneLoadReveal.IsPreparingGameplay && !GameSaveSystem.IsLoading, "normal completion gates preserved");
        Check(Object.FindObjectsOfType<RetroSceneLoadReveal>().Length == 1, "persistent overlay not duplicated");
        Check((string)Get(reveal, "destinationLabel") == "城镇二层", "destination refreshed after travel");
        Check(!overlay.activeSelf && !status.activeSelf, "no status left over in new scene");

        // Current save schema, not the legacy pre-profile smoke fixture (v2).
        yield return null;
        var journal = QuestJournalManager.GetOrCreate();
        journal.AddOrUpdateEntry("test.terminal-save", "过场读档检查", "仅测试使用", "Level2-Floor2");
        var saves = GameSaveSystem.Instance;
        Check(saves.Save(1, false), "current profile save succeeds");
        var data = saves.Read(1);
        Check(data != null && data.version == new GameSaveSystem.SaveData().version && data.scene == "Level2-Floor2", "current schema and destination read back");
        Check(saves.Load(1), "normal covered save load accepted");
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsLoading) yield return null;
        Check(SceneManager.GetActiveScene().name == "Level2-Floor2" && !RetroSceneLoadReveal.IsPreparingGameplay && Time.timeScale == 1f, "disk restore finishes before reveal unlocks");
        Check(JsonUtility.ToJson(QuestJournalManager.GetOrCreate().CaptureSaveState()).Contains("test.terminal-save"), "journal restored into live game");

        // Same-scene arena layout presentation uses the real loader, not teleport semantics.
        Check(RetroSceneLoadReveal.BeginLayoutTransition("Level2-Floor2", () => RetroSceneLoadReveal.LoadSceneAsync("Level2-Floor2")), "layout reload accepted");
        while (RetroSceneLoadReveal.IsBlockingInput)
        { Check(!status.activeSelf, "layout reload suppresses area announcement throughout activation"); yield return null; }
        Check(!RetroSceneLoadReveal.IsPreparingGameplay && Time.timeScale == 1f, "layout reload still completes all loading gates");
    }

    static void Capture(RetroSceneLoadReveal reveal, int width, int height, string name)
    {
        var rt = new RenderTexture(width, height, 24);
        var cameras = Camera.allCameras.Where(c => c.enabled && c.targetTexture == null).OrderBy(c => c.depth).ToArray();
        foreach (var c in cameras) c.targetTexture = rt;
        foreach (var scaler in Object.FindObjectsOfType<CanvasScaler>()) Call(scaler, "Handle");
        Canvas.ForceUpdateCanvases(); foreach (var c in cameras) c.Render();
        var canvas = (Canvas)Get(reveal, "overlayCanvas");
        var transforms = canvas.GetComponentsInChildren<Transform>(true);
        var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        foreach (var t in transforms) t.gameObject.layer = 31;
        var camera = new GameObject("Overlay capture camera", typeof(Camera)).GetComponent<Camera>();
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.Depth; camera.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Call(canvas.GetComponent<CanvasScaler>(), "Handle"); Canvas.ForceUpdateCanvases(); camera.Render();
        var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        const string folder = "../../.codex-temp/terminal-scene-previews";
        Directory.CreateDirectory(folder); File.WriteAllBytes(folder + "/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = old; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
        foreach (var c in cameras) c.targetTexture = null;
        Object.DestroyImmediate(camera.gameObject); Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
    }
}
#endif
