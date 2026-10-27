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

// Run only in the disposable build copy. Its unique product cannot touch user progress.
[InitializeOnLoad]
public static class ExhibitionSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static ExhibitionSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project");
        PlayerSettings.productName = "exhibition-smoke-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool(nameof(ExhibitionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(ExhibitionSmoke), false))
            {
                SessionState.SetBool(nameof(ExhibitionSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 420;
                stack.Push(Verify());
            }
            if (stack.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("exhibition suite timed out in " + SceneManager.GetActiveScene().name);
            if (stack.Peek().MoveNext())
            {
                if (stack.Peek().Current is IEnumerator child) stack.Push(child);
                return;
            }
            stack.Pop();
            if (stack.Count == 0)
            {
                Debug.Log("EXHIBITION_SMOKE_PASS edition=" + ExhibitionEdition.IsEnabled + " checks=" + checks);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { stack.Clear(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string scene)
    {
        int stable = 0;
        while (stable < 4)
        {
            bool ready = SceneManager.GetActiveScene().name == scene && !GameSaveSystem.IsLoading &&
                !GameSaveSystem.IsPreparingSceneSave && !RetroSceneLoadReveal.IsBlockingInput &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress;
            stable = ready ? stable + 1 : 0;
            yield return null;
        }
    }
    static IEnumerator Verify()
    {
        yield return Ready("TitleScreen");
        Check(Application.persistentDataPath.Contains("exhibition-smoke-"), "independent disposable save root");
        Check(!GameSaveSystem.Instance.HasContinuableProgress, "fresh product cannot see formal saves");
        var title = Object.FindObjectOfType<TitleScreenController>();
        var caption = title.transform.Find("Exhibition Edition");
        Check((caption != null) == ExhibitionEdition.IsEnabled, "title caption edition gate");
        if (caption != null)
        {
            Check(caption.GetComponent<Text>().text == "核聚变特供版" && !caption.GetComponent<Text>().raycastTarget, "caption text and nonblocking input");
            Preview(title.GetComponent<Canvas>(), "ExhibitionTitle-preview.png");
        }
        Check(ExhibitionEdition.ResolveDestination("TutorialLevel", "Level0") == (ExhibitionEdition.IsEnabled ? "Level2-Desert" : "Level0"), "tutorial route gate");
        Check(ExhibitionEdition.ResolveDestination("Level2-Floor1", "Level2-Floor2") == "Level2-Floor2", "ordinary travel unchanged");
        Check(GameSaveSystem.Instance.StartNewGame(0), "new game starts");
        yield return Ready("TutorialLevel");
        Check(!PlayerGrowthAttributes.Instance.CaptureRuntimeState().exhibitionLoadoutGranted, "no premature tutorial loadout");
        var movers = Object.FindObjectsOfType<ZeldaFourWayMover>(true);
        Check(movers.Any(m => m.CharacterData != null && m.CharacterData.IsGhostLike) &&
              movers.Any(m => m.CharacterData != null && !m.CharacterData.IsGhostLike), "authored ghost and body timing fixtures");
        foreach (var mover in movers.Where(m => m.CharacterData != null))
        {
            float authored = Get<float>(mover, "maximumStamina");
            Check(Mathf.Approximately(mover.MaximumStamina, authored * (ExhibitionEdition.IsEnabled ? 2 : 1)), "effective stamina multiplier");
            mover.RestoreSavedMovement(Vector2.down, mover.MaximumStamina);
            Check(Mathf.Approximately(mover.CurrentStamina, mover.MaximumStamina) && Mathf.Approximately(mover.StaminaNormalized, 1f), "restore retains full doubled stamina");
            float expected = Mathf.Max(.05f, Get<float>(mover, "controlSwitchHoldDuration"));
            if (!mover.CharacterData.IsGhostLike) expected = Mathf.Max(.05f, expected + Get<float>(mover, "nonGhostPossessionDurationBonus") - (ExhibitionEdition.IsEnabled ? 2f : 0f));
            Check(Mathf.Approximately((float)Call(mover, "GetActivePossessionDuration"), expected), "ghost/body possession timing");
        }
        var pause = Object.FindObjectOfType<PauseMenuController>();
        Check(pause.GetComponentsInChildren<Text>(true).Any(t => t.text == ExhibitionEdition.Caption) == ExhibitionEdition.IsEnabled, "pause caption gate");
        if (!ExhibitionEdition.IsEnabled)
        {
            ExhibitionEdition.PrepareLevelEntry("Level2-Desert");
            Check(!PlayerGrowthAttributes.Instance.CaptureRuntimeState().exhibitionLoadoutGranted &&
                !PlayerGrowthAttributes.Instance.HasSkill("soul_mark_2"), "formal version never grants exhibition skills");
            yield break;
        }
        Check(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(ExhibitionBuild.Scenes), "release contains only intended scenes");
        var transition = Object.FindObjectsOfType<SceneTransitionPoint>(true).First(t => t.TargetSceneName == "Level2-Desert");
        Call(transition, "LoadTargetScene");
        yield return Ready("Level2-Desert");
        var growth = PlayerGrowthAttributes.Instance;
        Check(growth.CaptureRuntimeState().exhibitionLoadoutGranted && growth.HealthBonus == 2 && growth.SoulMarkLevel == 2 && growth.DominoLevel == 1 && growth.CollectibleCount == 3, "actual tutorial exit grants requested loadout");
        Check(Enumerable.Range(0, 5).Select(growth.GetEquippedSkill).Contains("soul_mark_2") &&
            Enumerable.Range(0, 5).Select(growth.GetEquippedSkill).Contains("domino"), "active skills equipped in empty slots");
        var saves = GameSaveSystem.Instance;
        var entry = saves.ReplayEntries.Single(e => e.entryScene == "Level2-Desert");
        var entrance = (GameSaveSystem.SaveData)typeof(GameSaveSystem).GetMethod("ReadData", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { Path.Combine(GameSaveSystem.CheckpointsDirectory, entry.checkpointId + ".json") });
        Check(entrance.player.growth.exhibitionLoadoutGranted && entrance.player.growth.collectibleCount == 3, "immutable entrance already contains grant");
        var state = growth.CaptureRuntimeState(); state.collectibleCount = 1;
        growth.ApplyRuntimeState(JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(state)));
        ExhibitionEdition.PrepareLevelEntry("Level2-Floor1");
        Check(growth.CollectibleCount == 1 && growth.HealthBonus == 2, "reentry does not replenish DATA or duplicate health");
        Check(saves.Save(2, false), "actual manual save writes exhibition loadout");
        Check(saves.Load(2), "actual manual load accepted");
        yield return Ready("Level2-Desert");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount == 1 && PlayerGrowthAttributes.Instance.SoulMarkLevel == 2 &&
            PlayerGrowthAttributes.Instance.CaptureRuntimeState().exhibitionLoadoutGranted, "real restore preserves spent DATA and grant marker");
        Check(GameSaveSystem.Instance.RestartCurrentLevel(), "restart uses Level2 entrance");
        yield return Ready("Level2-Desert");
        Check(PlayerGrowthAttributes.Instance.CollectibleCount == 3 && PlayerGrowthAttributes.Instance.HealthBonus == 2, "restart restores original granted loadout");
        SceneManager.LoadScene("TitleScreen");
        yield return Ready("TitleScreen");
        Check(GameSaveSystem.Instance.StartNewGame(1), "second fresh profile starts");
        yield return Ready("TutorialLevel");
        Check(!PlayerGrowthAttributes.Instance.CaptureRuntimeState().exhibitionLoadoutGranted && !PlayerGrowthAttributes.Instance.HasSkill("health_1"), "new campaign resets grant cleanly");
    }
    static T Get<T>(object value, string name) => (T)value.GetType().GetField(name, Flags).GetValue(value);
    static object Call(object value, string name) => value.GetType().GetMethod(name, Flags).Invoke(value, null);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); Debug.Log("EXHIBITION_CHECK " + message); }
    static void Preview(Canvas canvas, string name)
    {
        var mode = canvas.renderMode; var previousCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var cameraObject = new GameObject("Edition preview camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(-10000, -10000, -10); camera.orthographic = true; camera.orthographicSize = 360;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = RenderTexture.GetTemporary(1280, 720, 24); var old = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Application.dataPath + "/../" + name, texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = distance;
            camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(target);
            Object.Destroy(texture); Object.Destroy(cameraObject); Canvas.ForceUpdateCanvases();
        }
    }
}
