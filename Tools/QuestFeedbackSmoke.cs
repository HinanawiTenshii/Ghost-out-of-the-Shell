using System;
using System.Collections;
using System.Collections.Generic;
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
public static class QuestFeedbackSmoke
{
    const string Key = nameof(QuestFeedbackSmoke);
    const string Parent = QuestJournalManager.FortressProofQuestId;
    const string Child = QuestJournalManager.KnightAmbushQuestId;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static QuestFeedbackSmoke() { EditorApplication.update += Tick; }
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Flags).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks++; Debug.Log("QUEST_FEEDBACK_CHECK " + label); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated only");
        PlayerSettings.productName = "quest-feedback-smoke";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SaveScene(scene, "Assets/QuestFeedbackFixture.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            { SessionState.SetBool(Key, false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Feedback test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("QUEST_FEEDBACK_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Border(Transform row, float thickness)
    {
        foreach (string side in new[] { "Top", "Bottom", "Left", "Right" })
        {
            var edge = row.Find(side).GetComponent<Image>();
            float size = side == "Top" || side == "Bottom" ? edge.rectTransform.rect.height : edge.rectTransform.rect.width;
            Check(Mathf.Abs(size - thickness) < .01f && !edge.raycastTarget, "border " + side + " thickness " + thickness);
        }
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        journal.AddOrUpdateEntry(Parent, QuestJournalManager.FortressProofQuestTitle, QuestJournalManager.FortressProofDescription);
        journal.AddOrUpdateEntry(Child, QuestJournalManager.KnightAmbushQuestTitle, QuestJournalManager.KnightAmbushDescription);
        journal.AddOrUpdateEntry("feedback.other", "其他任务", "普通任务");
        journal.TrackEntry(Parent);
        var go = new GameObject("Feedback menu", typeof(RectTransform)); go.SetActive(false);
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        go.AddComponent<GraphicRaycaster>();
        var menu = go.AddComponent<TabJournalMenuController>();
        Set(menu, "menuFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf"));
        go.SetActive(true); menu.OpenMenu(); Call(menu,"SetSelectedTab",1); Call(menu,"SelectQuestEntry",Child);
        yield return null; Canvas.ForceUpdateCanvases();
        var content = Get<RectTransform>(menu,"questListContent");
        Func<string,Transform> row = id => content.Cast<Transform>().Single(t => t.gameObject.activeSelf && t.name == "Quest " + id);
        var selected = row(Child); var tracked = row(Parent); var other = row("feedback.other");
        Border(selected,5); Border(tracked,2); Border(other,2);
        var ordinaryColor = other.GetComponent<Button>().colors.normalColor;
        var trackedColor = tracked.GetComponent<Button>().colors.normalColor;
        Check(trackedColor.grayscale > ordinaryColor.grayscale * 2, "tracked background is clearly brighter");
        Check(selected.GetComponent<Button>().colors.normalColor == ordinaryColor, "selection does not brighten untracked fill");
        Check(tracked.Find("Title").GetComponent<Text>().color.grayscale > selected.Find("Title").GetComponent<Text>().color.grayscale,
            "tracked label is brighter");
        Check(tracked.Find("Top").GetComponent<Image>().color.grayscale > selected.Find("Top").GetComponent<Image>().color.grayscale,
            "tracked border is brighter");
        Check(selected.GetComponent<Image>().color == Color.white, "row tint applied once");
        var rect = (RectTransform)selected;
        var parentRect = (RectTransform)tracked;
        Check(Mathf.Abs(-rect.anchoredPosition.y + parentRect.anchoredPosition.y - parentRect.rect.height - 4) < .01f,
            "child spacing unchanged");
        var button = other.GetComponent<Button>(); var colors = button.colors; colors.fadeDuration = 0; button.colors = colors;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        button.OnPointerExit(pointer); var normal = button.targetGraphic.canvasRenderer.GetColor();
        button.OnPointerEnter(pointer); var hover = button.targetGraphic.canvasRenderer.GetColor();
        button.OnPointerDown(pointer); var pressed = button.targetGraphic.canvasRenderer.GetColor();
        Check(normal != hover && hover != pressed, "real hover and press tint transitions");
        button.OnPointerUp(pointer); button.OnPointerExit(pointer);
        Capture(menu,canvas);

        int parentInstance=tracked.GetInstanceID(), childInstance=selected.GetInstanceID(), otherInstance=other.GetInstanceID();
        for(int i=0;i<12;i++)
        {
            Call(menu,"SelectQuestEntry",Parent);
            journal.SetEntryCollapsed(Parent,true);
            Check(row(Parent).GetInstanceID()==parentInstance && row("feedback.other").GetInstanceID()==otherInstance,
                "fold does not replace visible rows " + i);
            journal.SetEntryCollapsed(Parent,false);
            Call(menu,"SelectQuestEntry",Child);
            Check(row(Child).GetInstanceID()==childInstance && content.childCount==3,
                "selection and expand reuse child without duplicate rows " + i);
        }
        yield return null;
        Check(row(Parent).GetInstanceID()==parentInstance && row(Child).GetInstanceID()==childInstance,
            "no deferred row destruction after selection");

        Get<Button>(menu,"questTrackButton").onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
        Check(journal.TrackedEntryId == Child, "track action updates journal");
        Check(row(Child).GetComponent<Button>().colors.normalColor == trackedColor && row(Parent).GetComponent<Button>().colors.normalColor == ordinaryColor,
            "tracking transfers brightness immediately");
        Border(row(Child),5);
        row("feedback.other").GetComponent<Button>().onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
        Check(journal.TrackedEntryId == Child && row(Child).GetComponent<Button>().colors.normalColor == trackedColor,
            "selecting another entry leaves tracked highlight");
        Border(row(Child),2); Border(row("feedback.other"),5);
        var fold = row(Parent).GetComponentInChildren<QuestFoldoutGraphic>();
        pointer.position = RectTransformUtility.WorldToScreenPoint(null,fold.rectTransform.TransformPoint(fold.rectTransform.rect.center));
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count > 0 && hits[0].gameObject == fold.gameObject, "foldout remains independently clickable");
        fold.GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(journal.IsEntryCollapsed(Parent) && journal.TrackedEntryId == Child, "folding preserves tracking");
        journal.SetEntryCollapsed(Parent,false); yield return null;
        Check(row(Child).GetComponent<Button>().colors.normalColor == trackedColor, "expanded child restores tracking highlight");
        journal.CompleteEntry(Child); yield return null;
        Check(row(Child).GetComponent<Button>().colors.normalColor == ordinaryColor, "completed task no longer highlighted as tracked");
        menu.CloseMenu(); go.SetActive(false); yield return null;

        // The world marker follows the live actor without modifying the 0.7 s map sample.
        journal.ResetAllEntries(); journal.AddOrUpdateEntry(Parent,"失窃的证明","parent");
        journal.AddOrUpdateEntry(Child,QuestJournalManager.KnightAmbushQuestTitle,"child"); journal.TrackEntry(Child);
        var captainGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Troops/AdvancedPaladin-Squad.prefab"));
        var captain = captainGo.GetComponent<SquadPatrolCharacterAi>(); captain.enabled = false;
        captainGo.GetComponent<ZeldaFourWayMover>().enabled = false; captainGo.transform.position = Vector3.zero;
        var director = new GameObject("Knight marker director").AddComponent<KnightCaptainAmbush>();
        director.captain = captain; director.guards = new SquadPatrolCharacterAi[0]; director.safeHouses = new KnightCaptainAmbush.SafeHouse[0];
        var cam = new GameObject("World marker camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        cam.orthographic = true; cam.orthographicSize = 12; cam.transform.position = new Vector3(0,0,-10);
        yield return null; yield return null;
        Set(director,"nextMarkerTime",float.MaxValue);
        var hudGo = new GameObject("Marker HUD"); hudGo.SetActive(false);
        var hud = hudGo.AddComponent<ZeldaHealthHeartsUI>();
        var map = new GameObject("Marker map",typeof(RectTransform),typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        Set(map,"geometrySceneName",SceneManager.GetActiveScene().name);
        Set(hud,"questJournal",journal); Set(hud,"taskMiniMapGraphic",map);
        Call(hud,"ApplyTrackedQuestTarget",SceneManager.GetActiveScene(),Vector2.zero);
        Vector2 sample = director.CachedMarker;
        for (int i=1;i<=10;i++)
        {
            captainGo.transform.position = new Vector3(i * .2f, i * .1f,0);
            Call(hud,"LateUpdate");
            var marker = Get<GameObject>(hud,"trackedQuestWorldMarker");
            Check(marker.activeSelf && Vector2.Distance(marker.transform.position,captainGo.transform.position) < .001f,
                "world marker follows each frame " + i);
            Check(map.TryGetQuestTarget(Child,out var cached) && cached == sample && director.CachedMarker == sample,
                "world follow leaves map sample unchanged " + i);
        }
        Check(Mathf.Approximately(director.markerInterval,.7f),"map cadence remains 0.7 seconds");
        Set(director,"nextMarkerTime",0f); Call(director,"Update"); Call(hud,"LateUpdate");
        Check(Vector2.Distance(Get<Vector2>(map,"trackedQuestTargetPosition"),captainGo.transform.position) < .001f,
            "map refreshes at sample boundary even when player stationary");
        captainGo.SetActive(false); Call(hud,"LateUpdate");
        Check(!Get<GameObject>(hud,"trackedQuestWorldMarker").activeSelf,"unloaded actor has no stale world marker");
        captainGo.SetActive(true); Call(hud,"LateUpdate");
        Check(Get<GameObject>(hud,"trackedQuestWorldMarker").activeSelf,"world marker returns when actor loaded");
        Check(!KnightCaptainAmbush.TryGetWorldMarker("other-scene",out _),"world target cannot leak across scenes");
    }
    static void Capture(TabJournalMenuController menu, Canvas canvas)
    {
        var camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.enabled=false;
        camera.orthographic=true; camera.orthographicSize=360; camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color32(12,20,35,255); camera.cullingMask=1<<30;
        foreach(Transform t in menu.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=30;
        var rt=new RenderTexture(1280,720,24); camera.targetTexture=rt;
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
        Canvas.ForceUpdateCanvases(); camera.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../QuestFeedback-preview.png"),image.EncodeToPNG());
        RenderTexture.active=old; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
        Object.Destroy(camera.gameObject); Object.Destroy(rt); Object.Destroy(image);
    }
}
