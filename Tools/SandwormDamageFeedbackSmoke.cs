using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Isolated Unity Play Mode + GPU rendering; never opens/saves authored scenes.
[InitializeOnLoad]
public static class SandwormDamageFeedbackSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "SandwormDamageFeedbackSmoke.Pending";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test;
    static int frame = -1, checks;
    static SandwormDamageFeedbackSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(Pending,false)) { SessionState.SetBool(Pending,false); test=Verify(); }
            if(test==null || test.MoveNext()) return;
            test=null; Cleanup(); Debug.Log("SANDWORM_DAMAGE_FEEDBACK_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { test=null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value,string label) { checks++; if(!value) throw new Exception(label); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup() { foreach(var go in owned) if(go!=null) Object.DestroyImmediate(go); owned.Clear(); }
    static void Present(SandwormZeldaCharacterData data) => typeof(SandwormZeldaCharacterData).GetMethod("LateUpdate",Flags).Invoke(data,null);
    static void Age(ZeldaCharacterData data,float delta) => typeof(ZeldaCharacterData).GetMethod("UpdateDamageFeedback",Flags).Invoke(data,new object[]{delta});
    static SandwormZeldaCharacterData Worm()
    {
        var root=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        root.transform.position=Vector3.zero;
        root.GetComponent<SandwormBossPrototype>().ResetPose();
        root.GetComponent<SandwormBossPrototype>().SetMovementEnabled(false);
        return root.GetComponent<SandwormZeldaCharacterData>();
    }
    static Texture2D Render(Camera camera)
    {
        var rt=new RenderTexture(1200,320,24); camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(1200,320,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1200,320),0,0); image.Apply();
        camera.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(rt); return image;
    }
    static float Center(Texture2D image,out int red)
    {
        var pixels=image.GetPixels32(); double sum=0; int count=0; red=0;
        for(int i=0;i<pixels.Length;i++)
        {
            var p=pixels[i]; if(p.r<=30) continue;
            sum+=i%image.width; count++;
            if(p.r>p.g*2 && p.r>p.b*2) red++;
        }
        Check(count>10000,"GPU draws the whole worm"); return (float)(sum/count);
    }
    static void Gpu(SandwormZeldaCharacterData data,SpriteRenderer[] renderers)
    {
        var bounds=renderers[0].bounds;
        foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var camera=Own(new GameObject("Whole-body feedback preview camera")).AddComponent<Camera>();
        camera.transform.position=bounds.center+Vector3.back*20;
        camera.orthographic=true; camera.orthographicSize=Mathf.Max(bounds.extents.y+1,bounds.extents.x*320/1200+1);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.03f,.04f,.07f);
        var material=renderers[0].sharedMaterial;
        Check(material.shader.isSupported,"feedback shader supported");
        material.SetVector("_WorldShakeOffset",Vector4.zero);
        var still=Render(camera);
        float stillCenter=Center(still,out int red);
        Check(red>10000,"GPU renders red across the body, not a missing/magenta shader");
        material.SetVector("_WorldShakeOffset",new Vector4(.12f,0,0,0));
        var shifted=Render(camera);
        float shiftedCenter=Center(shifted,out _);
        float expected=.12f/(camera.orthographicSize*2)*320;
        Check(Mathf.Abs(shiftedCenter-stillCenter-expected)<1.1f,"GPU shake is a uniform world displacement");
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../Sandworm-damage-feedback-preview.png"),shifted.EncodeToPNG());
        Object.DestroyImmediate(still); Object.DestroyImmediate(shifted); Object.DestroyImmediate(camera.gameObject);
        Present(data);
    }
    static IEnumerator Verify()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Whole-body sandworm hit test"));
        Time.timeScale=0;
        var data=Worm(); var worm=data.GetComponent<SandwormBossPrototype>();
        var states=data.GetComponent<SandwormStateController>();
        var visuals=data.GetComponentsInChildren<SandwormSegmentVisual>();
        var renderers=new SpriteRenderer[visuals.Length]; var originalColors=new Color[visuals.Length];
        var originalMaterials=new Material[visuals.Length]; var positions=new Vector3[visuals.Length];
        for(int i=0;i<visuals.Length;i++)
        {
            renderers[i]=visuals[i].GetComponent<SpriteRenderer>();
            originalColors[i]=renderers[i].color; originalMaterials[i]=renderers[i].sharedMaterial;
        }
        Check(visuals.Length==8,"all eight authored body parts found");
        foreach(bool underground in new[]{false,true})
        {
            if(underground) states.EnterUnderground(); else states.EnterAboveGround();
            for(int i=0;i<visuals.Length;i++) positions[i]=visuals[i].transform.position;
            int hp=data.CurrentHealth; data.TakeDamage(1); Present(data); Physics2D.SyncTransforms();
            Check(data.CurrentHealth==hp-1,"normal shared health damage");
            foreach(var message in ShaderUtil.GetShaderMessages(Resources.Load<Shader>("SandwormDamageFeedback")))
                Check(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,"shader compiles: "+message.message);
            for(int i=0;i<visuals.Length;i++)
            {
                Check(renderers[i].color==data.GetDamageFeedbackTint(originalColors[i]),"whole-body red: "+visuals[i].name);
                Check(renderers[i].sharedMaterial==renderers[0].sharedMaterial,"one material shared by all sections");
                Check(visuals[i].transform.position==positions[i],"no transform/collider displacement");
                Check(visuals[i].HasUndergroundOverlay==underground,"underground texture retained");
                Check(visuals[i].GetComponent<Collider2D>().enabled!=underground,"collision state unchanged");
            }
            if(!underground) Gpu(data,renderers);
            // Repeated hits refresh the timer without capturing an already-red base.
            data.TakeDamage(1); Present(data); Age(data,1f); Present(data);
            for(int i=0;i<visuals.Length;i++)
                Check(renderers[i].color==originalColors[i] && renderers[i].sharedMaterial==originalMaterials[i],"exact restoration after repeated hits");
        }
        Check(data.GetComponentsInChildren<Renderer>().Length==8,"no duplicate geometry/renderers");
        data.TakeDamage(1); Present(data); data.enabled=false;
        for(int i=0;i<visuals.Length;i++) Check(renderers[i].color==originalColors[i] && renderers[i].sharedMaterial==originalMaterials[i],"disable clears transient feedback");
        data.enabled=true; Age(data,1f); Present(data);
        states.EnterUnderground(); worm.SetMovementEnabled(true);
        Check(worm.TrySetDestination(new Vector2(8,0)),"roaming fixture plans");
        data.TakeDamage(1);
        for(int frameIndex=0;frameIndex<20;frameIndex++)
        {
            worm.Advance(.025f);
            for(int i=0;i<visuals.Length;i++) positions[i]=visuals[i].transform.position;
            Age(data,.025f); Present(data);
            for(int i=0;i<visuals.Length;i++) Check(visuals[i].transform.position==positions[i],"feedback never rewinds roaming pose");
        }
        var attack=data.GetComponent<SandwormEruptionAttack>();
        UnityEngine.Random.InitState(7491);
        Check(attack.TryLockAttack(new Vector2(8,6)),"eruption fixture plans");
        bool checkedFlight=false;
        for(int n=0;n<3000 && !checkedFlight;n++)
        {
            worm.Advance(.025f); attack.AdvanceAttack(.025f);
            if(attack.Phase!=SandwormEruptionAttack.AttackPhase.Traversing) continue;
            data.TakeDamage(1);
            for(int i=0;i<visuals.Length;i++) positions[i]=visuals[i].transform.position;
            Present(data);
            for(int i=0;i<visuals.Length;i++) Check(visuals[i].transform.position==positions[i],"feedback does not change segmented flight pose");
            checkedFlight=true;
        }
        Check(checkedFlight,"eruption still reaches flight");
        attack.CancelAttack(); Age(data,1f); Present(data);
        // Actual time-based oscillation changes while the body stays still.
        worm.SetMovementEnabled(false); data.TakeDamage(1);
        Time.timeScale=1f; Time.captureFramerate=60;
        var offsets=new HashSet<float>();
        for(int n=0;n<20;n++)
        {
            Present(data);
            if(data.IsShowingDamageFeedback) offsets.Add(renderers[0].sharedMaterial.GetVector("_WorldShakeOffset").x);
            yield return null;
        }
        Check(offsets.Count>2,"visual shake oscillates over actual frames");
        Check(!data.IsShowingDamageFeedback && renderers[0].sharedMaterial==originalMaterials[0],"shared duration ends naturally");
        Time.captureFramerate=0; Time.timeScale=0;
        Cleanup(); yield return null;
    }
}
