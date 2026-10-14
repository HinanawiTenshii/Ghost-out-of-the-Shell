using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SceneScaleOptimizationSmoke
{
    static int checks;
    static IEnumerator test;
    const BindingFlags F = BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static SceneScaleOptimizationSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool("SceneScaleOptimization",true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (SessionState.GetBool("SceneScaleOptimization",false))
            { SessionState.SetBool("SceneScaleOptimization",false);test=Verify(); }
            if(test==null||test.MoveNext())return;
            test=null;Debug.Log("SCENE_SCALE_OPTIMIZATION_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){test=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Get(object obj,string key)=>SceneScaleBaselineSmoke.Field(obj,key).GetValue(obj);
    static void Set(object obj,string key,object value)=>SceneScaleBaselineSmoke.Field(obj,key).SetValue(obj,value);
    static object Call(object obj,string key,params object[] args)
    {
        for(Type t=obj.GetType();t!=null;t=t.BaseType)
        { var m=t.GetMethod(key,F|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(obj,args); }
        throw new Exception(key);
    }
    static IEnumerator Verify()
    {
        var before=JsonUtility.FromJson<SceneScaleBaselineSmoke.Result>(File.ReadAllText(SceneScaleBaselineSmoke.Output));
        var after=SceneScaleBaselineSmoke.CapturePaths();
        Check(before.paths.Length==after.paths.Length,"path fixture length");
        for(int i=0;i<before.paths.Length;i++)
        {
            Check(before.paths[i].success==after.paths[i].success,"route success unchanged "+i);
            Check(before.paths[i].points.Length==after.paths[i].points.Length,"route length unchanged "+i);
            for(int j=0;j<before.paths[i].points.Length;j++)
                Check((before.paths[i].points[j]-after.paths[i].points[j]).sqrMagnitude<.000001f,"route point unchanged");
        }
        Debug.Log("PERFORMANCE_ROUTES before_ms="+before.milliseconds+" after_ms="+after.milliseconds+" (single-run diagnostic, not FPS)");
        VerifyIndex(); VerifyMap(); VerifyStreaming(); VerifyRegistryAndScratch();
        yield return null;
        VerifyVisualBudget();
    }
    static void VerifyIndex()
    {
        var index=new SpatialBoundsIndex2D<object>(8);var all=new Dictionary<object,Bounds>();var result=new List<object>();
        var random=new System.Random(47);
        for(int i=0;i<4000;i++)
        {
            var item=new object();var b=new Bounds(new Vector3(random.Next(-1500,1500),random.Next(-1500,1500),0),new Vector3(random.Next(1,25),random.Next(1,25),1));
            all[item]=b;index.Set(item,b);
        }
        var big=new object();all[big]=new Bounds(Vector3.zero,new Vector3(5000,5000,1));index.Set(big,all[big]);
        for(int i=0;i<120;i++)
        {
            var q=new Bounds(new Vector3(random.Next(-1500,1500),random.Next(-1500,1500),0),new Vector3(i==119?8000:30,40,1));
            index.Query(q,result);var unique=new HashSet<object>(result);
            Check(unique.Count==result.Count,"no duplicate candidates");
            foreach(var pair in all)
                Check(unique.Contains(pair.Key)==pair.Value.Intersects(q),"grid matches brute force including large entries");
        }
        index.Remove(big);index.Query(new Bounds(Vector3.zero,Vector3.one),result);Check(!result.Contains(big),"remove oversized entry");
        index.Clear();index.Query(new Bounds(Vector3.zero,Vector3.one*10000),result);Check(result.Count==0,"index reset");
    }
    static void ConfigureMap(object map)
    {
        var paths=(List<Vector2[]>)Get(map,"blockPaths");
        for(int x=0;x<20;x++)for(int y=0;y<20;y++)
            paths.Add(new[]{new Vector2(x*3,y*3),new Vector2(x*3+2,y*3),new Vector2(x*3+2,y*3+2),new Vector2(x*3,y*3+2),new Vector2(x*3,y*3)});
        paths.Add(new[]{new Vector2(-1,-1),new Vector2(60,61)});
        Set(map,"mapBounds",new Bounds(new Vector3(30,30,0),new Vector3(62,64,0)));Set(map,"hasBounds",true);
        Call(map,"RebuildFilledWallTriangles");Call(map,"NotifyViewChanged");
    }
    static void VerifyMap()
    {
        var canvas=new GameObject("Map test canvas",typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var oldGo=new GameObject("Old",typeof(RectTransform),typeof(CanvasRenderer),typeof(BaselineMiniMapGraphic));oldGo.transform.SetParent(canvas.transform,false);
        var newGo=new GameObject("New",typeof(RectTransform),typeof(CanvasRenderer),typeof(RuntimeMiniMapGraphic));newGo.transform.SetParent(canvas.transform,false);
        var old=oldGo.GetComponent<BaselineMiniMapGraphic>();var current=newGo.GetComponent<RuntimeMiniMapGraphic>();
        old.rectTransform.sizeDelta=current.rectTransform.sizeDelta=new Vector2(480,260);
        old.rectTransform.pivot=current.rectTransform.pivot=new Vector2(.3f,.7f);
        ConfigureMap(old);ConfigureMap(current);
        var oldStream=new List<UIVertex>();var actual=new List<UIVertex>();var temp=new List<UIVertex>();
        var walls=newGo.transform.Find("Cached Map Walls").GetComponent<RuntimeMiniMapLayerGraphic>();
        var markers=newGo.transform.Find("Dynamic Map Markers").GetComponent<RuntimeMiniMapLayerGraphic>();
        using(var vh=new VertexHelper())
        {
            for(int i=0;i<16;i++)
            {
                var p=new Vector2(i*3,i*2);
                old.SetCameraPosition(p);current.SetCameraPosition(p);
                old.CenterOnWorldPosition(p);current.CenterOnWorldPosition(p);
                if(i%4==0){old.ZoomBy(.1f);current.ZoomBy(.1f);}
                old.SetTrackedQuestTarget(true,new Vector2(500,500),true);current.SetTrackedQuestTarget(true,new Vector2(500,500),true);
                Call(old,"OnPopulateMesh",vh);oldStream.Clear();vh.GetUIVertexStream(oldStream);
                actual.Clear();Call(current,"PopulateWalls",vh);temp.Clear();vh.GetUIVertexStream(temp);
                foreach(var vertex in temp){var v=vertex;v.position=current.transform.InverseTransformPoint(walls.transform.TransformPoint(v.position));actual.Add(v);}
                Call(current,"PopulateMarkers",vh);temp.Clear();vh.GetUIVertexStream(temp);
                foreach(var vertex in temp){var v=vertex;v.position=current.transform.InverseTransformPoint(markers.transform.TransformPoint(v.position));actual.Add(v);}
                Check(oldStream.Count==actual.Count,"map triangle count identical");
                for(int n=0;n<actual.Count;n++)
                {
                    Check((oldStream[n].position-actual[n].position).sqrMagnitude<.00001f,"map vertex identical after pan/zoom/non-centred pivot");
                    Check(oldStream[n].color.Equals(actual[n].color),"map colour unchanged");
                }
            }
            Canvas.ForceUpdateCanvases();int count=walls.MeshBuildCount;
            for(int i=0;i<12;i++){current.SetCameraPosition(new Vector2(i,i));current.CenterOnWorldPosition(new Vector2(i,i));Canvas.ForceUpdateCanvases();}
            Check(walls.MeshBuildCount==count,"pan and marker updates do not rebuild wall mesh");
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<200;i++)Call(old,"OnPopulateMesh",vh);timer.Stop();double before=timer.Elapsed.TotalMilliseconds;
            timer.Restart();for(int i=0;i<200;i++)Call(current,"PopulateMarkers",vh);timer.Stop();
            Debug.Log("PERFORMANCE_MAP_200_UPDATES before_ms="+before+" after_ms="+timer.Elapsed.TotalMilliseconds+" static_wall_builds_during_pan="+(walls.MeshBuildCount-count));
            current.enabled=false;Check(!walls.enabled&&!markers.enabled,"disabling owner hides both layers");current.enabled=true;
        }
        Object.DestroyImmediate(canvas);
    }
    static void VerifyStreaming()
    {
        var cameraGo=new GameObject("Streaming fixture",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=5;camera.aspect=1.4f;camera.transform.position=new Vector3(0,0,-10);
        var streamer=cameraGo.AddComponent<CameraVisionObjectStreaming>();streamer.enabled=false;
        var objects=new List<GameObject>();var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,2);
        for(int i=0;i<500;i++)
        { var go=new GameObject("stream "+i);go.AddComponent<SpriteRenderer>().sprite=sprite;go.transform.position=new Vector3(i%25*5,i/25*5,0);objects.Add(go); }
        Set(streamer,"discoveryTransformsPerFrame",100000);Call(streamer,"RebuildObjectRegistry");Call(streamer,"ProcessDiscoveryScan");
        var lookup=(IDictionary)Get(streamer,"managedLookup");
        for(int iteration=0;iteration<6;iteration++)
        {
            camera.transform.position=new Vector3(iteration*14,iteration*9,-10);camera.transform.rotation=Quaternion.Euler(0,0,iteration*11);camera.orthographicSize=5+iteration*2;
            foreach(var go in objects){var entry=lookup[go];Check(entry!=null,"object registered");go.SetActive(false);Set(entry,"suspendedByManager",true);}
            if(iteration==3)objects[490].transform.position=camera.transform.position+Vector3.forward*10; // disabled teleport
            Call(streamer,"RefreshStreamingViewport");Call(streamer,"UpdateSpatialIndex");
            var planes=(Plane[])Get(streamer,"streamingPlanes");float pad=(float)Get(streamer,"currentPreloadPadding")*.5f;
            var expected=new HashSet<GameObject>();
            foreach(var go in objects)
            {
                Bounds b=(Bounds)Call(streamer,"GetWorldBounds",lookup[go]);
                bool inside=(bool)typeof(CameraVisionObjectStreaming).GetMethod("IntersectsStreamingViewport",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{b,planes,pad});
                if(inside)expected.Add(go);
            }
            Call(streamer,"RestoreObjectsNearViewport");
            foreach(var go in objects)Check(go.activeSelf==expected.Contains(go),"indexed restoration matches original frustum test");
        }
        var hidden=objects[400];hidden.SetActive(false);Set(lookup[hidden],"suspendedByManager",false);
        var regionGo=new GameObject("retention fixture");regionGo.transform.position=hidden.transform.position;
        regionGo.AddComponent<CameraVisionStreamingRegion>().Configure(8);
        Call(streamer,"UpdateSpatialIndex");Call(streamer,"RestoreObjectsInRetentionRegions");
        Check(!hidden.activeSelf,"puzzle-disabled object is never restored");
        var nearby=objects[401];Check(nearby.activeSelf,"bomb region restores nearby streamed objects");
        Object.DestroyImmediate(regionGo);foreach(var go in objects)Object.DestroyImmediate(go);
        Object.DestroyImmediate(cameraGo);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);
    }
    static void VerifyRegistryAndScratch()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Bodyguard-level2.prefab");
        var objects=new List<GameObject>();var found=new List<ZeldaFourWayMover>();object scratch=null;
        for(int i=0;i<40;i++)
        {
            var go=Object.Instantiate(prefab,new Vector3(i*20,0,0),Quaternion.identity);objects.Add(go);
            var ai=go.GetComponent<ZeldaCharacterAiBase>();Call(ai,"EnsureGridCapacity",14641);
            var storage=Get(ai,"gridCosts");if(scratch!=null)Check(ReferenceEquals(scratch,storage),"all NPCs share scratch arrays");scratch=storage;
        }
        ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();ZeldaRuntimeRegistry.QueryNearbyMovers(Vector2.zero,7,found);
        Check(found.Count==1,"local query excludes distant NPCs");
        var mover=objects[0].GetComponent<ZeldaFourWayMover>();mover.enabled=true;
        bool present=false;foreach(var active in ZeldaRuntimeRegistry.ControlledMovers)present|=active==mover;Check(present,"control register immediate");
        mover.enabled=false;present=false;foreach(var active in ZeldaRuntimeRegistry.ControlledMovers)present|=active==mover;Check(!present,"control removal immediate");
        foreach(var go in objects)Object.DestroyImmediate(go);
    }
    static void VerifyVisualBudget()
    {
        var go=new GameObject("Visual fixture camera",typeof(Camera));var cam=go.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=5;cam.transform.position=new Vector3(0,0,-10);
        // Force the next shared snapshot even if this editor tick reuses a frame.
        typeof(SceneVisualWorkBudget).GetField("frame",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,-1);
        Check(SceneVisualWorkBudget.IsVisible(new Bounds(Vector3.zero,Vector3.one)),"visible effects retain full update rate");
        Check(!SceneVisualWorkBudget.IsVisible(new Bounds(new Vector3(10000,10000),Vector3.one)),"offscreen art work can be omitted");
        Object.DestroyImmediate(go);
    }
}
