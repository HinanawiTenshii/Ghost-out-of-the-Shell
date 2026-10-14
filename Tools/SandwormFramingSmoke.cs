using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandwormFramingSmoke
{
    static readonly List<Object> owned = new List<Object>();
    static IEnumerator pending;
    static int frame = -1, checks;
    delegate float SmoothZoom(float current, float target, ref float velocity, float smoothTime, float deltaTime);
    static SandwormFramingSmoke() { EditorApplication.update += Tick; }
    static T Own<T>(T obj) where T : Object { owned.Add(obj); return obj; }
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception("Sandworm framing: "+message); }
    static void Near(float actual, float expected, string message)
    { Check(Mathf.Abs(actual-expected)<.025f, message+" actual="+actual+" expected="+expected); }
    static void Cleanup()
    {
        for (int i=owned.Count-1;i>=0;i--) if (owned[i]!=null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); Time.timeScale=1f;
    }
    public static void Run()
    {
        SessionState.SetBool("SandwormFramingSmoke.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if (SessionState.GetBool("SandwormFramingSmoke.Pending",false))
            { SessionState.SetBool("SandwormFramingSmoke.Pending",false); pending=Verify(); }
            if (pending==null || pending.MoveNext()) return;
            pending=null; Cleanup(); Debug.Log("SANDWORM_FRAMING_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception error) { pending=null; Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static ZeldaFourWayMover Player(float size, float vision)
    {
        var root=Own(new GameObject("Framing player"));
        var mover=root.AddComponent<ZeldaFourWayMover>();
        var so=new SerializedObject(mover.CharacterData);
        so.FindProperty("cameraOrthographicSize").floatValue=size;
        so.FindProperty("playerVisionRadius").floatValue=vision;
        so.ApplyModifiedPropertiesWithoutUndo();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        return mover;
    }

    static void VerifyZoomSmoothing()
    {
        var smooth=(SmoothZoom)Delegate.CreateDelegate(typeof(SmoothZoom),
            typeof(CameraFollowActiveZeldaMover).GetMethod("SmoothZoomValue", BindingFlags.Static|BindingFlags.NonPublic));
        // Reproduce the old inertia bug independently of render/update timings.
        float old=10f, oldVelocity=0f;
        for(int i=0;i<6;i++) old=Mathf.SmoothDamp(old,13f,ref oldVelocity,.25f,Mathf.Infinity,1f/60f);
        float newTarget=old-.05f, newVelocity=oldVelocity;
        float oldNext=Mathf.SmoothDamp(old,newTarget,ref oldVelocity,.25f,Mathf.Infinity,1f/60f);
        Check(oldNext>old,"regression fixture reproduces previous opposite-direction drift");
        Check(smooth(old,newTarget,ref newVelocity,.25f,1f/60f)<=old,"reversal never keeps moving away from target");

        foreach(float dt in new[]{1f/15f,1f/30f,1f/60f,1f/144f})
        {
            float current=10f, velocity=0f;
            for(int i=0;i<600;i++)
            {
                float target=i<6?13f:i<12?10f:i<18?14f:i<30?11f:10f;
                float step=i%11==0?dt*3f:dt; // Include a slow frame during transition.
                float next=smooth(current,target,ref velocity,.4f,step);
                Check(next>=Mathf.Min(current,target)-.000001f && next<=Mathf.Max(current,target)+.000001f,
                    "every frame stays between current value and target dt="+dt+" frame="+i);
                Check(!float.IsNaN(velocity) && !float.IsInfinity(velocity),"finite velocity");
                current=next;
                if(i==8)
                {
                    float savedVelocity=velocity;
                    Check(smooth(current,target,ref velocity,.4f,0f)==current && velocity==savedVelocity,
                        "pause leaves zoom and velocity unchanged");
                }
            }
            Near(current,10f,"repeated interruptions converge without drift");
        }
        float atRestVelocity=0f;
        Check(smooth(10f,10f,ref atRestVelocity,.25f,0f)==10f && atRestVelocity==0f,"paused settled zoom stays finite");
    }

    static IEnumerator WatchZoom(Camera camera, CameraCircularVision vision, float targetSize, float ratio, float duration)
    {
        float until=Time.unscaledTime+duration;
        float previous=camera.orthographicSize;
        while(Time.unscaledTime<until)
        {
            yield return null;
            float current=camera.orthographicSize;
            Check(current>=Mathf.Min(previous,targetSize)-.0001f && current<=Mathf.Max(previous,targetSize)+.0001f,
                "live zoom has no overshoot or rebound: "+previous+" -> "+current+" target="+targetSize);
            Check(Mathf.Abs(vision.VisionRadius/current-ratio)<.001f,
                "vision and camera share displayed multiplier throughout transition");
            previous=current;
        }
    }

    static IEnumerator Verify()
    {
        VerifyZoomSmoothing();
        Time.timeScale=0f;
        Scene scene=SceneManager.GetActiveScene();
        var a=Own(new GameObject("Framing owner A")).AddComponent<CameraVisionStreamingExempt>();
        var b=Own(new GameObject("Framing owner B")).AddComponent<CameraVisionStreamingExempt>();
        CameraTemporaryFraming.Set(a,1.3f); CameraTemporaryFraming.Set(a,1.3f);
        CameraTemporaryFraming.Set(b,1.3f);
        Near(CameraTemporaryFraming.GetMultiplier(scene),1.3f,"overlapping worms do not compound or duplicate");
        CameraTemporaryFraming.Remove(a);
        Near(CameraTemporaryFraming.GetMultiplier(scene),1.3f,"one ending worm cannot cancel another active worm");
        b.gameObject.SetActive(false);
        Near(CameraTemporaryFraming.GetMultiplier(scene),1f,"disabled owner request is pruned");
        b.gameObject.SetActive(true);
        CameraTemporaryFraming.Set(b,1.3f); Object.DestroyImmediate(b.gameObject);
        Near(CameraTemporaryFraming.GetMultiplier(scene),1f,"destroyed owner cannot leak multiplier");
        var other=SceneManager.CreateScene("Framing additive test");
        var remote=Own(new GameObject("Other-scene owner")); SceneManager.MoveGameObjectToScene(remote,other);
        CameraTemporaryFraming.Set(remote.AddComponent<CameraVisionStreamingExempt>(),1.3f);
        Near(CameraTemporaryFraming.GetMultiplier(scene),1f,"other scenes do not affect current camera");
        Near(CameraTemporaryFraming.GetMultiplier(other),1.3f,"request applies in owning scene");
        var unload=SceneManager.UnloadSceneAsync(other);
        while (!unload.isDone) yield return null;
        Near(CameraTemporaryFraming.GetMultiplier(other),1f,"scene unload releases framing");

        var player=Player(10f,20f);
        var cameraRoot=Own(new GameObject("Framing camera"));
        var camera=cameraRoot.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=10f; camera.enabled=false;
        var follow=cameraRoot.AddComponent<CameraFollowActiveZeldaMover>();
        var vision=cameraRoot.AddComponent<CameraCircularVision>();
        Time.timeScale=1f;
        float until=Time.unscaledTime+1.5f;
        while(Time.unscaledTime<until) yield return null;
        Near(camera.orthographicSize,10f,"baseline camera size"); Near(vision.VisionRadius,20f,"baseline vision");
        CameraTemporaryFraming.Set(a,1.3f);
        Near(camera.orthographicSize,10f,"request does not snap camera size");
        Near(vision.VisionRadius,20f,"request does not snap the mask ahead of the camera");
        var transition=WatchZoom(camera,vision,13f,2f,.09f);
        while(transition.MoveNext()) yield return transition.Current;
        CameraTemporaryFraming.Remove(a);
        transition=WatchZoom(camera,vision,10f,2f,.06f);
        while(transition.MoveNext()) yield return transition.Current;
        CameraTemporaryFraming.Set(a,1.3f);
        transition=WatchZoom(camera,vision,13f,2f,1.8f);
        while(transition.MoveNext()) yield return transition.Current;
        Near(camera.orthographicSize,13f,"real camera smooths toward thirty percent larger size");
        Near(vision.VisionRadius,26f,"real vision mask expands thirty percent");
        follow.enabled=false; follow.enabled=true;
        transition=WatchZoom(camera,vision,13f,2f,.2f);
        while(transition.MoveNext()) yield return transition.Current;
        Near(camera.orthographicSize,13f,"re-enable does not compound an active expansion");
        Near(player.CharacterData.CameraOrthographicSize,10f,"camera base data remains unchanged");
        Near(player.CharacterData.PlayerVisionRadius,20f,"vision base data remains unchanged");
        Check(CharacterPotionBuffs.TryApply(player.CharacterData,AlchemyPotionKind.Sight),"sight potion fixture applies");
        yield return null; yield return null;
        Near(vision.VisionRadius,32.5f,"sight potion composes with encounter multiplier exactly once");
        for(int i=0;i<20;i++) yield return null;
        Near(vision.VisionRadius,32.5f,"effective range does not grow every frame");
        var switched=Player(6f,12f);
        until=Time.unscaledTime+1.5f; while(Time.unscaledTime<until) yield return null;
        Near(camera.orthographicSize,7.8f,"controlled-character change uses new base camera size");
        Near(vision.VisionRadius,15.6f,"controlled-character change uses new base range");
        CameraTemporaryFraming.Remove(a);
        transition=WatchZoom(camera,vision,6f,2f,1.8f);
        while(transition.MoveNext()) yield return transition.Current;
        Near(camera.orthographicSize,6f,"ending effect restores current character, not old snapshot");
        Near(vision.VisionRadius,12f,"ending effect removes only temporary range increase");
        Near(switched.CharacterData.CameraOrthographicSize,6f,"new character data is never overwritten");
    }
}
