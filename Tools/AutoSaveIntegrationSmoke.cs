using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Copy to Assets/Editor of the disposable .codex-temp project, execute Run.
// Captures/restores the actual TutorialLevel world in a unique test product.
// No player save is read, migrated, removed, or used as a test fixture.
[InitializeOnLoad]
public static class AutoSaveIntegrationSmoke
{
    private const string ProductPrefix = "autosave-integration-smoke-";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static int frame = -1, checks;
    private static double deadline;
    static AutoSaveIntegrationSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = ProductPrefix + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/TutorialLevel.unity");
        SessionState.SetBool(nameof(AutoSaveIntegrationSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(AutoSaveIntegrationSmoke), false))
            {
                SessionState.SetBool(nameof(AutoSaveIntegrationSmoke), false);
                routines.Push(Verify());
                deadline = EditorApplication.timeSinceStartup + 240;
            }
            if (routines.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Automatic save integration timed out");
            var current = routines.Peek();
            if (current.MoveNext())
            {
                if (current.Current is IEnumerator child) routines.Push(child);
                return;
            }
            routines.Pop();
            if (routines.Count == 0)
            {
                Debug.Log("AUTO_SAVE_INTEGRATION_PASS checks=" + checks);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception ex)
        {
            routines.Clear(); Debug.LogException(ex); EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Ready()
    {
        int stable = 0;
        while (stable < 3)
        {
            bool ready = SceneManager.GetActiveScene().name == "TutorialLevel" && !GameSaveSystem.IsLoading &&
                !GameSaveSystem.IsPreparingSceneSave && !RetroSceneLoadReveal.IsPreparingGameplay &&
                !RetroSceneLoadReveal.IsBlockingInput && !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress &&
                !SceneTravelStateManager.GetOrCreate().IsQuickRestartPending;
            stable = ready ? stable + 1 : 0;
            yield return null;
        }
    }

    private static IEnumerator Verify()
    {
        AssertDisposablePaths();
        yield return Ready();
        var saves = GameSaveSystem.Instance;
        Check(saves != null, "actual save service bootstrapped");
        saves.enabled = false; // This fixture advances the scheduler explicitly.
        Time.timeScale = 0f;
        if (saves.Read(0) == null || saves.ReplayEntries.Count == 0)
        {
            Invoke("BeginNewGame", 0);
            Check((bool)Invoke("SaveCore", 0, false, true), "capture authored pristine tutorial entrance");
        }
        var initial = saves.Read(0);
        var entry = saves.ReplayEntries.Single(e => e.id == GameSaveSystem.LevelIdForScene("TutorialLevel"));
        string checkpoint = Path.Combine(GameSaveSystem.CheckpointsDirectory, entry.checkpointId + ".json");
        Check(initial != null && File.Exists(checkpoint), "initial autosave and immutable entry exist");
        byte[] checkpointBytes = File.ReadAllBytes(checkpoint);
        string originalEntry = JsonUtility.ToJson(entry);
        string profileId = saves.ActiveProfileId;

        // Use an authored body already represented by the Tutorial save catalog,
        // so the real restore can resolve its normal character prefab and path.
        var body = Object.FindObjectsOfType<ZeldaFourWayMover>(true).FirstOrDefault(m =>
            m.gameObject.scene == SceneManager.GetActiveScene() && m.CharacterData != null &&
            !m.CharacterData.IsGhostLike && !m.CharacterData.IsDead);
        Check(body != null, "tutorial contains an authored living body");
        foreach (var mover in Object.FindObjectsOfType<ZeldaFourWayMover>(true)) mover.enabled = false;
        foreach (var ai in Object.FindObjectsOfType<ZeldaCharacterAiBase>(true)) ai.enabled = false;
        for (Transform node = body.transform; node != null; node = node.parent) node.gameObject.SetActive(true);
        var character = body.CharacterData.CaptureRuntimeState();
        character.health = character.currentHealth = 8;
        body.CharacterData.ApplyRuntimeState(character);
        body.enabled = true;
        ZeldaRuntimeRegistry.ClaimControlledMover(body);
        Check(ZeldaRuntimeRegistry.GetControlledMover() == body, "fixture controls the catalogued body");

        Growth(101); Check(saves.Save(1, false), "write actual quick slot fixture");
        Growth(202); Check(saves.Save(2, false), "write actual first manual slot fixture");
        string profileFolder = Path.Combine(GameSaveSystem.ProfilesDirectory, profileId);
        var protectedFiles = CaptureFiles(profileFolder, "quick.json", "manual-1.json");
        string[] automaticNames = { "auto.json", "auto-previous-1.json", "auto-previous-2.json" };

        int[] moments = { 11, 22, 33 };
        for (int index = 0; index < moments.Length; index++)
        {
            Growth(moments[index]);
            Time.timeScale = 1f;
            float start = 100f + index * 40f;
            PrimeClock(saves, body, start);
            for (int request = 0; request < 20; request++)
                GameSaveSystem.RequestAutoSave("integration complete transaction " + request, true);
            Check(Get<bool>(saves, "autoSaveRequested"), "coalesced request queued for moment " + moments[index]);
            byte[] before = File.ReadAllBytes(Path.Combine(profileFolder, "auto.json"));
            Invoke("TickAutoSave");
            Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(profileFolder, "auto.json"))),
                "new queue does not write before safe/settle windows");
            Set(saves, "autoPlayClock", start + 10f);
            Set(saves, "nextSafetyPoll", 0f);
            Check((bool)Invoke("CanCommitAutomaticSave"), "safe mature queue can commit actual world");
            Invoke("TickAutoSave");
            Time.timeScale = 0f;
            var saved = saves.Read(0);
            Check(saved != null && saved.collectibleCount == moments[index] &&
                saved.player.growth.collectibleCount == moments[index], "scheduler committed distinct actual player state " + moments[index]);
            Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "autoProgressDirty"),
                "successful real commit consumes coalesced queue once");
            Check(Get<bool>(saves, "autoSaveCompletionPending"), "successful event save queues completion notice");
            Invoke("TickAutoSaveCompletionNotification");
            Check(Get<bool>(saves, "autoSaveCompletionPending"), "pause defers completion notice");
            Time.timeScale = 1f;
            var blocking = typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", Flags);
            bool oldBlocking = (bool)blocking.GetValue(null);
            try
            {
                blocking.SetValue(null, true);
                Invoke("TickAutoSaveCompletionNotification");
                Check(Get<bool>(saves, "autoSaveCompletionPending"), "black loading curtain defers completion notice");
            }
            finally { blocking.SetValue(null, oldBlocking); }
            var hud = ZeldaHealthHeartsUI.Instance;
            Check(hud != null, "notification HUD exists");
            hud.ShowJournalStyleNotificationPopup("任务日志已更新");
            hud.ShowJournalStyleNotificationPopup("fixture general notice");
            Invoke("TickAutoSaveCompletionNotification");
            Check(!Get<bool>(saves, "autoSaveCompletionPending") && saves.LastMessage == "自动存档已完成", "actual saved snapshot announces success exactly once");
            var notice = hud.transform.Find("Journal Style Notification AutomaticSave");
            Check(notice != null && notice.gameObject.activeSelf &&
                notice.GetComponentInChildren<Text>().text == "自动存档已完成", "completion uses the existing HUD toast style");
            Check(hud.transform.Find("Journal Style Notification General").GetComponentInChildren<Text>().text == "fixture general notice",
                "completion has its own notification kind and does not replace general feedback");
            int messages = 0;
            Action changed = () => messages++;
            saves.Changed += changed;
            try { Invoke("TickAutoSaveCompletionNotification"); }
            finally { saves.Changed -= changed; }
            Check(messages == 0, "completion is not announced again on the next frame");
            Time.timeScale = 0f;
        }
        Check(!(bool)Invoke("SaveCore", 99, false, false) && !Get<bool>(saves, "autoSaveCompletionPending"),
            "rejected save does not queue a success notice");
        foreach (var expected in new[] { new { slot = 0, growth = 33 }, new { slot = 7, growth = 22 }, new { slot = 8, growth = 11 } })
        {
            var data = saves.Read(expected.slot);
            Check(data != null && data.collectibleCount == expected.growth && data.player.growth.collectibleCount == expected.growth,
                "read slot " + expected.slot + " restores snapshot metadata/player growth " + expected.growth);
        }
        Check(Directory.GetFiles(profileFolder, "auto*.json").Length == 3, "exactly three actual automatic save files");
        Unchanged(protectedFiles, "automatic queue leaves quick and manual bytes unchanged");
        Check(checkpointBytes.SequenceEqual(File.ReadAllBytes(checkpoint)) &&
            JsonUtility.ToJson(saves.ReplayEntries.Single(e => e.id == entry.id)) == originalEntry,
            "mid-level autos never rewrite pristine entry/checkpoint bytes");

        // Full production restore of CURRENT auto. Merely reading it must not
        // manufacture a fourth moment or rotate rollback history.
        var historyBeforeLoad = CaptureFiles(profileFolder, automaticNames);
        Time.timeScale = 1f;
        Check(saves.Load(0), "normal current-slot load accepted");
        yield return Ready();
        for (int i = 0; i < 4; i++) yield return null;
        saves = GameSaveSystem.Instance;
        saves.enabled = false;
        Check(saves.ActiveProfileId == profileId && PlayerGrowthAttributes.Instance.CollectibleCount == 33,
            "actual restore returns selected profile and latest growth");
        Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "autoProgressDirty"),
            "ordinary current load creates no new-progress request");
        Unchanged(historyBeforeLoad, "normal load preserves all three automatic files byte-for-byte");
        Unchanged(protectedFiles, "normal load preserves quick/manual files");
        Check(checkpointBytes.SequenceEqual(File.ReadAllBytes(checkpoint)), "normal load preserves original entrance checkpoint");

        // Loading an older automatic moment changes the Continue reference,
        // not the payloads or timestamps of any rollback snapshot.
        Time.timeScale = 1f;
        Check(saves.Load(7), "older automatic slot load accepted");
        yield return Ready();
        for (int i = 0; i < 4; i++) yield return null;
        saves = GameSaveSystem.Instance;
        saves.enabled = false;
        Check(saves.ActiveProfileId == profileId && PlayerGrowthAttributes.Instance.CollectibleCount == 22,
            "actual older-slot restore returns growth 22");
        var resumedProfile = saves.ListProfiles().Single(p => p.id == profileId);
        object[] olderContinue = { resumedProfile, -1, null, 0L };
        var olderTarget = (GameSaveSystem.SaveData)typeof(GameSaveSystem).GetMethod("ResolveContinueTarget", Flags)
            .Invoke(null, olderContinue);
        Check(olderTarget != null && (int)olderContinue[1] == 7 && olderContinue[2] == null &&
            olderTarget.collectibleCount == 22 && olderTarget.player.growth.collectibleCount == 22,
            "persisted Continue selection follows loaded slot 7 instead of newer auto 0");
        Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "autoProgressDirty"),
            "older-slot restore creates no new-progress request");
        Unchanged(historyBeforeLoad, "older-slot load preserves all three automatic files byte-for-byte");
        Unchanged(protectedFiles, "older-slot load preserves quick/manual files");

        Time.timeScale = 1f;
        Check(saves.CanRestartCurrentLevel && saves.RestartCurrentLevel(), "real restart accepts original tutorial entrance");
        yield return Ready();
        for (int i = 0; i < 4; i++) yield return null;
        saves = GameSaveSystem.Instance;
        saves.enabled = false;
        Check(PlayerGrowthAttributes.Instance.CollectibleCount == entry.initialCollectibleCount,
            "real level restart restores pristine entrance growth");
        var restartedProfile = saves.ListProfiles().Single(p => p.id == profileId);
        Check(restartedProfile.resumeCheckpointId == entry.checkpointId && restartedProfile.resumeSlot == -1,
            "restart persists the same immutable checkpoint identity as Continue destination");
        object[] entryContinue = { restartedProfile, -1, null, 0L };
        var entryTarget = (GameSaveSystem.SaveData)typeof(GameSaveSystem).GetMethod("ResolveContinueTarget", Flags)
            .Invoke(null, entryContinue);
        Check(entryTarget != null && (int)entryContinue[1] == -1 &&
            (string)entryContinue[2] == entry.checkpointId && entryTarget.collectibleCount == entry.initialCollectibleCount,
            "Continue resolves restarted entrance without writing a replacement automatic snapshot");
        Check(checkpointBytes.SequenceEqual(File.ReadAllBytes(checkpoint)) &&
            JsonUtility.ToJson(saves.ReplayEntries.Single(e => e.id == entry.id)) == originalEntry,
            "real restart preserves original entrance metadata and checkpoint bytes");
        Unchanged(historyBeforeLoad, "real restart preserves all three rollback payloads");
        Check(saves.Read(0).collectibleCount == 33 && !Get<bool>(saves, "autoSaveRequested"),
            "restart resumes entrance through metadata while auto 0 retains growth 33");
        Unchanged(protectedFiles, "real restart preserves quick/manual files");

        var pause = Object.FindObjectOfType<PauseMenuController>();
        Check(pause != null, "authored pause UI exists after restore");
        Time.timeScale = 1f;
        pause.OpenMenu();
        typeof(PauseMenuController).GetMethod("OpenSaveSlots", Flags).Invoke(pause, new object[] { false });
        yield return null;
        var panel = Object.FindObjectOfType<SaveSlotPanel>();
        Check(panel != null && SaveSlotPanel.IsOpen, "real load slot panel opens");
        var scroll = panel.GetComponentInChildren<ScrollRect>();
        Check(scroll != null && scroll.vertical && !scroll.horizontal && scroll.verticalScrollbar != null &&
            scroll.viewport.GetComponent<RectMask2D>() != null, "slot panel supplies a masked vertical scrollbar");
        var rows = scroll.content.Cast<Transform>().Where(t => t.name.StartsWith("Slot ", StringComparison.Ordinal)).ToArray();
        Check(rows.Select(t => t.name).SequenceEqual(new[] { "Slot 0", "Slot 7", "Slot 8", "Slot 1", "Slot 2", "Slot 3", "Slot 4", "Slot 5", "Slot 6" }),
            "all nine rows retain automatic/quick/manual grouping");
        Check(scroll.content.rect.height > scroll.viewport.rect.height, "overflow scrolls instead of clipping inaccessible rows");
        foreach (int slot in new[] { 0, 7, 8, 1 })
        {
            var row = rows.Single(t => t.name == "Slot " + slot);
            Check(row.GetComponent<Button>() != null && row.GetComponent<Button>().interactable && row.Find("Delete Save") == null,
                "read-only automatic/quick slot " + slot + " is loadable without delete control");
        }
        scroll.verticalNormalizedPosition = 1f;
        Canvas.ForceUpdateCanvases();
        yield return null;
        RenderPreview(pause.GetComponent<Canvas>(), "AutoSaveSlots-preview.png");
        scroll.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Check(Mathf.Abs(scroll.verticalNormalizedPosition) < .01f, "scroll reaches fifth manual slot at bottom");
        RenderPreview(pause.GetComponent<Canvas>(), "AutoSaveSlots-bottom-preview.png");
        typeof(SaveSlotPanel).GetMethod("Close", Flags).Invoke(panel, null);
        pause.CloseMenu();
        yield return null;
        Unchanged(historyBeforeLoad, "opening and scrolling load UI never rotates saves");
    }

    private static void PrimeClock(GameSaveSystem saves, ZeldaFourWayMover body, float value)
    {
        Set(saves, "autoPlayClock", value); Set(saves, "safeSince", value);
        Set(saves, "nextSafetyPoll", 0f); Set(saves, "nextAutoRetry", 0f); Set(saves, "damageHoldUntil", 0f);
        Set(saves, "autoObservedMover", body); Set(saves, "autoObservedPosition", (Vector2)body.transform.position);
        Set(saves, "autoObservedHealth", body.CharacterData.CurrentHealth);
        Set(saves, "autoObservedShieldVersion", body.CharacterData.HolyShieldBlockVersion);
    }
    private static void Growth(int value)
    {
        var growth = PlayerGrowthAttributes.GetOrCreate();
        var state = growth.CaptureRuntimeState(); state.collectibleCount = value;
        growth.ApplyRuntimeState(state);
    }
    private static Dictionary<string, byte[]> CaptureFiles(string folder, params string[] names) =>
        names.Select(name => Path.Combine(folder, name)).ToDictionary(path => path, File.ReadAllBytes);
    private static void Unchanged(Dictionary<string, byte[]> files, string label) =>
        Check(files.All(pair => File.Exists(pair.Key) && pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key))), label);
    private static void AssertDisposablePaths()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/") &&
            Application.productName.StartsWith(ProductPrefix, StringComparison.Ordinal) &&
            Application.persistentDataPath.Replace('\\', '/').Contains(Application.productName),
            "fixture can only read/write a fresh isolated product directory");
        string persistent = Path.GetFullPath(Application.persistentDataPath) + Path.DirectorySeparatorChar;
        Check(Path.GetFullPath(GameSaveSystem.SaveDirectory).StartsWith(persistent, StringComparison.OrdinalIgnoreCase),
            "production save root remains inside disposable product");
    }
    private static void RenderPreview(Canvas canvas, string filename)
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", filename));
        Check(output.Replace('\\', '/').Contains("/.codex-temp/"), "preview stays in isolated project");
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
        var cameraObject = new GameObject("AutoSave Slots Preview Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(-10000f, -10000f, -10f);
        camera.orthographic = true; camera.orthographicSize = 360f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(output, texture.EncodeToPNG());
            Check(File.Exists(output), "saved " + filename);
        }
        finally
        {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); Object.Destroy(cameraObject);
            Canvas.ForceUpdateCanvases();
        }
    }
    private static void Check(bool condition, string label)
    { checks++; if (!condition) throw new Exception("AutoSaveIntegration: " + label); Debug.Log("AUTO_SAVE_INTEGRATION_CHECK " + label); }
    private static object Invoke(string name, params object[] args) => typeof(GameSaveSystem).GetMethod(name, Flags).Invoke(GameSaveSystem.Instance, args);
    private static T Get<T>(GameSaveSystem saves, string field) => (T)typeof(GameSaveSystem).GetField(field, Flags).GetValue(saves);
    private static void Set(GameSaveSystem saves, string field, object value) => typeof(GameSaveSystem).GetField(field, Flags).SetValue(saves, value);
}
