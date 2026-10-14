using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;

[InitializeOnLoad]
public static class SandwormWarningSmoke
{
    static readonly List<Object> owned=new List<Object>();
    static IEnumerator pending;
    static int frame=-1, checks;
    static SandwormWarningSmoke() { EditorApplication.update+=Tick; }
    static T Own<T>(T value) where T:Object { owned.Add(value); return value; }
    static void Check(bool value,string label) { checks++; if(!value) throw new Exception("Sandworm warning: "+label); }
    static void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--) if(owned[i]!=null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        foreach(var site in Object.FindObjectsOfType<SandwormEruptionSite>()) site.Release();
    }
    public static void Run() { SessionState.SetBool("SandwormWarning.Pending",true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool("SandwormWarning.Pending",false))
            { SessionState.SetBool("SandwormWarning.Pending",false); pending=Verify(); }
            if(pending==null || pending.MoveNext()) return;
            pending=null; Cleanup(); Time.timeScale=1f;
            Debug.Log("SANDWORM_WARNING_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception error) { pending=null; Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static SandwormEruptionSite Site(bool warning=true)
    {
        var site=SandwormEruptionSite.Create(SceneManager.GetActiveScene(),Vector2.zero,2f,warning);
        Own(site.gameObject); return site;
    }
    static IEnumerator Verify()
    {
        Time.timeScale=0f;
        var site=Site(); var glyph=site.GetComponentInChildren<SandwormAttackWarning>();
        Check(site.HasWarning && glyph!=null,"emergence has geometric sandworm emblem");
        Check(glyph.GetComponentsInChildren<Renderer>().Length==1,"all primitive strokes batched into one renderer");
        var warningRenderer=glyph.GetComponent<Renderer>();
        Check(warningRenderer.sortingLayerID==0 && warningRenderer.sortingOrder==-1,
            "warning stays above floor but below characters and ghosts");
        var mesh=glyph.GetComponent<MeshFilter>().sharedMesh;
        Check(mesh.vertexCount>100 && mesh.vertexCount<300,"maw outline and central exclamation are one small static geometric mesh");
        foreach(var vertex in mesh.vertices) Check(vertex.magnitude<=site.Radius*1.01f,"geometry stays within real damage radius tolerance");
        Check(mesh.bounds.extents.x>site.Radius*.98f && mesh.bounds.extents.y>site.Radius*.96f,
            "open jaws span the actual danger footprint without a separate octagon frame");
        int renderers=site.GetComponentsInChildren<Renderer>().Length;
        float low=1f, high=0f;
        for(int i=0;i<150;i++) { site.AdvanceVisuals(.01f); low=Mathf.Min(low,glyph.DisplayOpacity); high=Mathf.Max(high,glyph.DisplayOpacity); }
        Check(low>.6f && high-low<.34f,"ordinary tracking has only a mild steady pulse");
        site.SetWarningImminent(true,5f); low=1f; high=0f; int toggles=0; bool wasBright=true;
        Vector3 scale=glyph.transform.localScale; Bounds bounds=mesh.bounds;
        for(int i=0;i<100;i++)
        {
            site.AdvanceVisuals(.01f); bool bright=glyph.DisplayOpacity>.5f;
            if(bright!=wasBright) toggles++; wasBright=bright;
            low=Mathf.Min(low,glyph.DisplayOpacity); high=Mathf.Max(high,glyph.DisplayOpacity);
            Check(scale==glyph.transform.localScale && mesh.bounds==bounds,"flashing never changes indicated range");
        }
        Check(high-low>.85f && toggles>=9 && toggles<=10,"imminent attack flashes strongly at the configured frequency");
        float paused=glyph.DisplayOpacity; site.AdvanceVisuals(0f);
        Check(paused==glyph.DisplayOpacity,"pause freezes warning animation");
        site.Pulse(true); Check(!site.IsWarningImminent,"urgent blink stops exactly when emergence begins");
        Check(warningRenderer.sortingOrder==-1,"flashing and eruption cannot promote warning over characters");
        foreach(var renderer in site.GetComponentsInChildren<SpriteRenderer>())
            if(renderer.enabled && renderer.gameObject.name.StartsWith("Pooled"))
                Check(renderer.sortingOrder>=6,"ground-warning change leaves dust/debris foreground ordering unchanged");
        site.Finish(); site.AdvanceVisuals(3f);
        Check(!glyph.GetComponent<Renderer>().enabled && site.IsFinished,"finished warning completely fades away");
        Check(site.GetComponentsInChildren<Renderer>().Length==renderers && renderers<=64,"no added per-frame renderers");
        Cleanup(); yield return null;

        var root=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        var movement=root.GetComponent<SandwormBossPrototype>(); movement.ResetPose();
        var attack=root.GetComponent<SandwormEruptionAttack>(); Random.InitState(7491);
        Check(attack.TryLockAttack(new Vector2(8,6)),"fixed attack plans successfully");
        SandwormEruptionSite emergence=null, landing=null;
        foreach(var current in Object.FindObjectsOfType<SandwormEruptionSite>())
        { if((Vector2)current.transform.position==attack.LockedAttackPoint) emergence=current; else landing=current; }
        Check(emergence!=null && landing!=null && emergence.HasWarning && !landing.HasWarning,"only emergence creates a glyph");
        bool distant=false, urgent=false, landingDust=false; float previous=float.PositiveInfinity;
        for(int i=0;i<3000;i++)
        {
            movement.Advance(.02f);
            float eta=movement.GetDirectedArrivalSeconds(attack.LockedAttackPoint);
            attack.AdvanceAttack(.02f);
            if(attack.Phase==SandwormEruptionAttack.AttackPhase.Approaching)
            {
                Check(eta<=previous+.001f,"directed route ETA counts down without straight-line false positives"); previous=eta;
                Check(emergence.IsWarningImminent==(eta<=.85f),"flash uses real remaining path time");
                distant|=!emergence.IsWarningImminent; urgent|=emergence.IsWarningImminent;
            }
            if(attack.LandingPulses==0)
                foreach(var renderer in landing.GetComponentsInChildren<Renderer>())
                    Check(!renderer.enabled,"no landing indicator or opening appears in advance");
            else if(landing!=null)
            { landingDust|=landing.ActiveFragmentCount>0; Check(!landing.HasWarning,"actual landing keeps dust but never adds a warning"); }
            if(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming) break;
        }
        Check(distant && urgent && landingDust,"normal warning, imminent blink and marker-free landing exercised");
        Check(attack.EmergencePulses==8 && attack.LandingPulses==8,"all damage pulses remain unchanged");
        Cleanup(); yield return null;
    }

    public static void Preview()
    {
        try
        {
            var site=Site();
            var camera=Own(new GameObject("Sandworm glyph preview camera")).AddComponent<Camera>();
            camera.transform.position=new Vector3(0,0,-10); camera.orthographic=true; camera.orthographicSize=2.55f;
            camera.backgroundColor=new Color(15f/255f,33f/255f,60f/255f); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.allowHDR=false; camera.allowMSAA=false;
            var rt=Own(new RenderTexture(640,640,24)); camera.targetTexture=rt;
            var texture=Own(new Texture2D(640,640,TextureFormat.RGBA32,false));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","sandworm-warning-frames")); Directory.CreateDirectory(folder);
            for(int i=0;i<120;i++)
            {
                if(i==60) site.SetWarningImminent(true,5f);
                site.AdvanceVisuals(1f/30f); camera.Render(); RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,640,640),0,0); texture.Apply(); RenderTexture.active=null;
                File.WriteAllBytes(Path.Combine(folder,i.ToString("D3")+".png"),texture.EncodeToPNG());
            }
            camera.targetTexture=null; Cleanup(); Debug.Log("SANDWORM_WARNING_PREVIEW_PASS"); EditorApplication.Exit(0);
        }
        catch(Exception error) { Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
