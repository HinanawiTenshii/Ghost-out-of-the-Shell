using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroProofHudTravelSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1;
    static double deadline;
    static int checks;
    static HeroProofHudTravelSmoke() { EditorApplication.update += Tick; }
    static object Field(object target, string name) => target.GetType().GetField(name, F).GetValue(target);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "hero-proof-hud-travel-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Desert.unity");
        SessionState.SetBool(nameof(HeroProofHudTravelSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HeroProofHudTravelSmoke), false))
            {
                SessionState.SetBool(nameof(HeroProofHudTravelSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 180; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERO_PROOF_HUD_TRAVEL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void VerifyHud(string label, int stage, bool expectedTarget)
    {
        Canvas.ForceUpdateCanvases();
        Dump(label);
        var hud = Object.FindObjectOfType<ZeldaHealthHeartsUI>();
        var map = (RuntimeMiniMapGraphic)Field(hud, "taskMiniMapGraphic");
        var journal = QuestJournalManager.GetOrCreate();
        Check(journal.Level2HeroProofStage == stage, label + ": correct quest stage");
        Check(journal.TrackedEntryId == QuestJournalManager.Level2HeroProofId, label + ": main quest still tracked");
        bool found = map.TryGetQuestTarget(journal.TrackedEntryId, out var cached);
        bool live = RuntimeMiniMapGraphic.TryGetSceneQuestTarget(SceneManager.GetActiveScene(), journal.TrackedEntryId, out var position);
        Check(found == expectedTarget && live == expectedTarget && cached == position, label + ": HUD cache matches live scene target");
        Check((bool)Field(map, "showTrackedQuestTarget") == expectedTarget, label + ": HUD marker visibility");
        Check(map.isActiveAndEnabled && ((RuntimeMiniMapLayerGraphic)Field(map, "markerLayer")).isActiveAndEnabled,
            label + ": HUD marker layer enabled");
        if (expectedTarget)
        {
            Check((Vector2)Field(map, "trackedQuestTargetPosition") == position, label + ": correct world position");
            Check((bool)Field(map, "clampQuestTargetToBorder"), label + ": offscreen marker clamped to minimap frame");
            using (var vh = new VertexHelper())
            {
                Call(map, "PopulateMarkers", vh); int visibleVertices = vh.currentVertCount;
                map.SetTrackedQuestTarget(false, position, true); Call(map, "PopulateMarkers", vh);
                Check(visibleVertices > vh.currentVertCount, label + ": marker included in rendered mesh");
                map.SetTrackedQuestTarget(true, position, true);
            }
        }
    }
    static void Dump(string label)
    {
        var hud = Object.FindObjectOfType<ZeldaHealthHeartsUI>();
        var map = (RuntimeMiniMapGraphic)Field(hud, "taskMiniMapGraphic");
        var journal = QuestJournalManager.GetOrCreate();
        var scene = SceneManager.GetActiveScene();
        bool found = map.TryGetQuestTarget(journal.TrackedEntryId, out var target);
        bool liveFound = RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, journal.TrackedEntryId, out var live);
        var markers = (RuntimeMiniMapLayerGraphic)Field(map, "markerLayer");
        Debug.Log("HUD_TRAVEL " + label + " scene=" + scene.name + " handle=" + scene.handle +
            " mapScene=" + Field(map, "geometrySceneName") + " hudScene=" + ((Scene)Field(hud, "taskMiniMapScene")).name +
            " stage=" + journal.Level2HeroProofStage + " tracked=" + journal.TrackedEntryId + " found=" + found + " pos=" + target +
            " live=" + liveFound + " livePos=" + live + " visible=" + Field(map, "showTrackedQuestTarget") +
            " mapActive=" + map.isActiveAndEnabled + " markerActive=" + markers.isActiveAndEnabled + " cull=" + markers.canvasRenderer.cull +
            " rect=" + markers.rectTransform.rect + " char=" + ActiveZeldaCharacterDataSource.Instance.ActiveCharacterData);
        using (var vh = new VertexHelper())
        {
            Call(map, "PopulateMarkers", vh);
            Debug.Log("HUD_TRAVEL mesh verts=" + vh.currentVertCount);
        }
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        var journal = QuestJournalManager.GetOrCreate();
        journal.ResetAllEntries(); Call(journal, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        VerifyHud("desert", 1, true);
        SceneManager.sceneLoaded += (scene, mode) => Dump("sceneLoaded");
        var transition = Object.FindObjectsOfType<PersistentSceneTransitionPoint>(true).Single(t => t.TargetSceneName == "Level2-Floor1");
        Call(transition, "BeginTransition");
        while (SceneManager.GetActiveScene().name != "Level2-Floor1" || DocumentReader.IsInputBlocked || SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        VerifyHud("first city entry without extra movement", 2, true);
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Desert", new Vector2(22.31f, 68f));
        while (SceneManager.GetActiveScene().name != "Level2-Desert" || DocumentReader.IsInputBlocked || SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress) yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        VerifyHud("desert revisit", 2, false);
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1", new Vector2(-97f, -45f));
        while (SceneManager.GetActiveScene().name != "Level2-Floor1" || DocumentReader.IsInputBlocked || SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress) yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        VerifyHud("second city entry", 2, true);
        var save = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(save);
        VerifyHud("quest save restored", 2, true);
        var reload = SceneManager.LoadSceneAsync("Level2-Floor1");
        while (!reload.isDone || DocumentReader.IsInputBlocked) yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        VerifyHud("same-name scene reload", 2, true);
    }
}
