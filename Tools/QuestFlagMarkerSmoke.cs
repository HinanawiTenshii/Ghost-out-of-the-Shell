using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real uGUI meshes, clipping and world rendering, in the disposable test project.
public static class QuestFlagMarkerSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Call(object o, string name, params object[] args)
        => o.GetType().GetMethod(name, F, null, Array.ConvertAll(args, a => a.GetType()), null).Invoke(o, args);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);

    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VerifySharedGlyph();
            VerifyMapEdges();
            VerifyIncidentBorder();
            RenderPreview();
            Debug.Log("QUEST_AMETHYST_MARKER_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static RuntimeMiniMapGraphic CreateMap(Transform parent, Vector2 position, Vector2 size, float iconScale)
    {
        var viewport = new GameObject("Map viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(parent, false); viewport.anchoredPosition = position; viewport.sizeDelta = size;
        viewport.GetComponent<Image>().color = new Color32(6, 15, 29, 255);
        var map = new GameObject("Map", typeof(RectTransform), typeof(RuntimeMiniMapGraphic)).GetComponent<RuntimeMiniMapGraphic>();
        map.transform.SetParent(viewport, false); map.rectTransform.sizeDelta = size;
        map.SetIconScale(iconScale); map.SetCameraMarkerVisible(false);
        return map;
    }

    static void VerifySharedGlyph()
    {
        using (var vh = new VertexHelper())
        {
            QuestFlagGlyph.AddTo(vh, Vector2.zero, 1);
            Check(vh.currentVertCount == 22, "bounded 22-vertex amethyst glyph");
            var first = QuestFlagGlyph.CreateWorldVisual(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var second = QuestFlagGlyph.CreateWorldVisual(first.scene);
            var mesh = first.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh == second.GetComponent<MeshFilter>().sharedMesh, "world markers share mesh");
            Check(first.GetComponent<Renderer>().sharedMaterial == second.GetComponent<Renderer>().sharedMaterial, "world markers share material");
            Check(first.GetComponent<Renderer>().sortingOrder == 29999, "world badge remains below vision mask");
            Check(first.transform.rotation == Quaternion.identity, "badge upright");
            Check(first.GetComponentsInChildren<Renderer>().Length == 1 && first.GetComponent<Collider2D>() == null, "single renderer without physics");
            Check(Mathf.Abs(first.GetComponent<Renderer>().bounds.size.y - .65f) < .001f, "world footprint preserved");
            var vertices = mesh.vertices; var colors = mesh.colors32;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                var v = new UIVertex(); vh.PopulateUIVertex(ref v, i);
                Check(v.position == vertices[i] && v.color.Equals(colors[i]), "map and world identical vertex " + i);
            }
            Check(vertices[0].x == 0 && vertices[3].x == 0 && vertices[0].y == -vertices[3].y,
                "hexagon vertical tips centred");
            Check(vertices[1].x == -vertices[5].x && vertices[2].x == -vertices[4].x &&
                vertices[1].y == vertices[5].y && vertices[2].y == vertices[4].y, "hexagon symmetrical");
            Check(vertices[18].x == 0 && vertices[20].x == 0 && vertices[19].y == 0 && vertices[21].y == 0,
                "solid diamond core centred");
            Check(colors[6].Equals(new Color32(234,212,255,255)) && colors[12].Equals(new Color32(41,33,68,255)) &&
                colors[18].Equals(new Color32(188,131,255,255)), "approved amethyst palette");
            var badge = new GameObject("Region badge", typeof(RectTransform), typeof(MapSceneQuestMarkerGraphic)).GetComponent<MapSceneQuestMarkerGraphic>();
            badge.rectTransform.sizeDelta = Vector2.one * 28;
            using (var badgeVertices = new VertexHelper())
            {
                Call(badge, "OnPopulateMesh", badgeVertices);
                Check(badgeVertices.currentVertCount == vh.currentVertCount, "region badge uses same amethyst glyph");
            }
            Object.DestroyImmediate(badge.gameObject);
            Object.DestroyImmediate(first); Object.DestroyImmediate(second);
        }
    }

    static void VerifyMapEdges()
    {
        var map = CreateMap(null, Vector2.zero, Vector2.one * 96, .45f);
        Set(map, "mapBounds", new Bounds(Vector3.zero, new Vector3(100, 100, 0)));
        Set(map, "hasBounds", true);
        var directions = new[] {Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1,1), new Vector2(1,-1), new Vector2(-1,1), new Vector2(-1,-1), new Vector2(3,1)};
        foreach (var size in new[] {Vector2.one*96, new Vector2(180,96), new Vector2(96,180), Vector2.one*16, new Vector2(740,520)})
        foreach (var pivot in new[] {Vector2.one*.5f, new Vector2(.2f,.8f)})
        foreach (float icons in new[] {.2f,.45f,1f,2f})
        {
            map.rectTransform.sizeDelta = size; map.rectTransform.pivot = pivot; map.SetIconScale(icons);
            var rect = map.GetPixelAdjustedRect();
            foreach (float distance in new[] {0.5f, 17f, 22f, 100f, 10000f})
            foreach (var direction in directions)
            {
                Vector2 target = direction * distance;
                map.SetTrackedQuestTarget(true, target, true);
                using (var actual = new VertexHelper())
                {
                    Call(map, "PopulateMarkers", actual);
                    Check(actual.currentVertCount == 22, "full badge retained on every border");
                    var bounds = BoundsOf(actual);
                    const float epsilon=.002f;
                    Check(bounds.xMin >= rect.xMin-epsilon && bounds.xMax <= rect.xMax+epsilon &&
                        bounds.yMin >= rect.yMin-epsilon && bounds.yMax <= rect.yMax+epsilon,
                        "no clipped geometry " + size + " / " + direction + " / " + icons);
                    var tip = new UIVertex(); var top = new UIVertex();
                    actual.PopulateUIVertex(ref tip,3); actual.PopulateUIVertex(ref top,0);
                    Check(top.position.y > tip.position.y, "off-map badges stay upright");
                    Vector2 center = bounds.center;
                    Vector2 bearing = center-rect.center;
                    if (direction.sqrMagnitude > 0)
                        Check(Mathf.Abs(bearing.x*direction.y-bearing.y*direction.x) < .01f, "bearing preserved");
                    if(distance>=100 && direction.sqrMagnitude>0)
                    {
                        float gap=Mathf.Min(Mathf.Min(bounds.xMin-rect.xMin,rect.xMax-bounds.xMax),
                            Mathf.Min(bounds.yMin-rect.yMin,rect.yMax-bounds.yMax));
                        Check(Mathf.Abs(gap-1f)<.003f,"off-screen marker touches inner edge without dead band: "+gap);
                    }
                }
            }
        }
        map.rectTransform.sizeDelta=Vector2.one*96; map.rectTransform.pivot=Vector2.one*.5f; map.SetIconScale(.45f);
        map.SetTrackedQuestTarget(true, Vector2.one*100, true);
        using (var actual=new VertexHelper())
        {
            Call(map,"PopulateMarkers",actual);
            var b=BoundsOf(actual);
            Check(Mathf.Abs(b.yMax-47f)<.003f,"full-menu map clamps to visible edge too");
        }
        map.SetTrackedQuestTargets(new[]{Vector2.left*100,Vector2.right*100},true);
        using (var actual=new VertexHelper())
        {
            Call(map,"PopulateMarkers",actual);
            Check(actual.currentVertCount==44,"two injector destinations retained");
            var bounds=BoundsOf(actual);
            Check(Mathf.Abs(bounds.xMin+47f)<.003f && Mathf.Abs(bounds.xMax-47f)<.003f,"both injector targets touch their respective edges");
        }
        // Zoom, pan and resize must recompute the viewport intersection, not
        // leave a cached edge position behind or clamp inside the map data bounds.
        foreach(float zoomDelta in new[]{-100f,1f,2f,100f})
        foreach(float pan in new[]{0f,.5f,1f})
        {
            map.ZoomBy(zoomDelta);map.SetHorizontalPan01(pan);map.SetVerticalPan01(1-pan);
            map.SetTrackedQuestTarget(true,new Vector2(10000,2000),true);
            using(var actual=new VertexHelper())
            {
                Call(map,"PopulateMarkers",actual);var b=BoundsOf(actual);
                Check(Mathf.Abs(b.xMax-47f)<.003f,"zoom/pan keeps the right-edge target attached");
            }
            Vector2 viewCenter=(Vector2)typeof(RuntimeMiniMapGraphic).GetField("viewCenterNormalized",F).GetValue(map);
            map.SetTrackedQuestTarget(true,new Vector2(-50+100*viewCenter.x,-50+100*viewCenter.y),true);
            using(var actual=new VertexHelper())
            {
                Call(map,"PopulateMarkers",actual);Check(BoundsOf(actual).center.sqrMagnitude<.001f,"visible target returns to its real position");
            }
        }
        map.SetTrackedQuestTarget(false,Vector2.zero,true);
        using(var actual=new VertexHelper()){Call(map,"PopulateMarkers",actual);Check(actual.currentVertCount==0,"untracking removes badge");}
        Object.DestroyImmediate(map.transform.parent.gameObject);
    }

    static Rect BoundsOf(VertexHelper vh)
    {
        Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
        for(int i=0;i<vh.currentVertCount;i++){
            var v=new UIVertex();vh.PopulateUIVertex(ref v,i);
            Check(!float.IsNaN(v.position.x)&&!float.IsInfinity(v.position.y),"finite mesh vertex");
            min=Vector2.Min(min,v.position);max=Vector2.Max(max,v.position);
        }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    static void VerifyIncidentBorder()
    {
        var map=CreateMap(null,Vector2.zero,Vector2.one*96,.45f);
        Set(map,"mapBounds",new Bounds(Vector3.zero,Vector3.one*100));Set(map,"hasBounds",true);
        var timer=new GameObject("Timer",typeof(RectTransform),typeof(MiniMapIncidentTimerGraphic)).GetComponent<MiniMapIncidentTimerGraphic>();
        timer.transform.SetParent(map.transform.parent,false);timer.rectTransform.sizeDelta=Vector2.one*92;timer.Bind(map);
        map.SetTrackedQuestTarget(true,Vector2.right*10000,true);
        foreach(var phase in new[]{SharedPlayerHostilityTimer.IncidentPhase.Hostile,SharedPlayerHostilityTimer.IncidentPhase.Search,SharedPlayerHostilityTimer.IncidentPhase.Idle})
        {
            timer.SetDisplay(phase,.7f);
            using(var mesh=new VertexHelper())
            {
                Call(map,"PopulateMarkers",mesh);
                float expected=phase==SharedPlayerHostilityTimer.IncidentPhase.Idle?47f:42f;
                Check(Mathf.Abs(BoundsOf(mesh).xMax-expected)<.003f,"marker touches actual visible border without incident ring covering it");
            }
        }
        timer.SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Hostile,.5f);timer.enabled=false;
        using(var mesh=new VertexHelper()){Call(map,"PopulateMarkers",mesh);Check(Mathf.Abs(BoundsOf(mesh).xMax-47)<.003f,"removing timer releases reserved strip");}
        Object.DestroyImmediate(map.transform.parent.gameObject);
    }

    static void RenderPreview()
    {
        var canvas=new GameObject("Preview",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.WorldSpace; canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,720);
        Label(canvas.transform,new Vector2(0,325),"紫晶六角任务标点 · 小地图边界",24);
        var snapshot=Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor1");
        Check(snapshot!=null,"real Level2 map snapshot available");
        var overview=CreateMap(canvas.transform,new Vector2(-220,15),new Vector2(500,540),1f);
        overview.RebuildFromSnapshot(snapshot);overview.ZoomBy(-100);
        // Use an authored objective when available, otherwise the map's centre.
        var mapBounds=(Bounds)typeof(RuntimeMiniMapGraphic).GetField("mapBounds",F).GetValue(overview);
        overview.SetTrackedQuestTargets(new[]{(Vector2)mapBounds.center+Vector2.right*10000,(Vector2)mapBounds.center+Vector2.up*10000},true);
        Label(canvas.transform,new Vector2(-220,-285),"菜单地图 · 右侧与上方范围外",18);
        var directions=new[]{Vector2.right,new Vector2(-1,1),Vector2.down,Vector2.zero};
        var labels=new[]{"右侧范围外","左上范围外","下方范围外","范围内"};
        for(int i=0;i<4;i++)
        {
            Vector2 position=new Vector2(125+(i%2)*220,170-(i/2)*240);
            var map=CreateMap(canvas.transform,position,Vector2.one*96,.45f);
            map.transform.parent.localScale=Vector3.one*1.8f;
            map.RebuildFromSnapshot(snapshot);map.SetZoomToMaximum();map.CenterOnWorldPosition(mapBounds.center);
            map.SetTrackedQuestTarget(true,(Vector2)mapBounds.center+directions[i]*10000,true);
            var timer=new GameObject("Timer",typeof(RectTransform),typeof(MiniMapIncidentTimerGraphic)).GetComponent<MiniMapIncidentTimerGraphic>();
            timer.transform.SetParent(map.transform.parent,false);timer.rectTransform.sizeDelta=Vector2.one*92;
            timer.Bind(map);
            timer.SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Hostile,.7f);
            Label(canvas.transform,position+Vector2.down*110,labels[i],18);
        }
        Label(canvas.transform,new Vector2(0,-335),"同一晶标同步用于场景目标、任务触发提示与区域列表",18);
        var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=360;camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(7,17,31,255);canvas.worldCamera=camera;
        var rt=new RenderTexture(1000,720,24);camera.targetTexture=rt;
        Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var pixels=new Texture2D(1000,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1000,720),0,0);pixels.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(),"QuestAmethystMarker-preview.png"),pixels.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);
    }

    static void Label(Transform parent,Vector2 position,string text,int size)
    {
        var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent,false);label.rectTransform.anchoredPosition=position;label.rectTransform.sizeDelta=new Vector2(980,38);
        label.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf")??Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize=size;label.alignment=TextAnchor.MiddleCenter;label.text=text;label.color=ZeldaUiPalette.Primary;
    }
}
