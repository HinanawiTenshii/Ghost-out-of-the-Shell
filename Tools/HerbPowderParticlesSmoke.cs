using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in the isolated smoke project, never in an authored gameplay scene.
[InitializeOnLoad]
public static class HerbPowderParticlesSmoke
{
    const string Pending = "HerbPowderParticlesSmoke.Pending";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static readonly string[] Names = { "Red", "Green", "Blue" };
    static HerbPowderParticlesSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("HERB_POWDER_PARTICLES_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    static void Sync(object visual) => visual.GetType().GetMethod("SyncParticleVisibility", Private).Invoke(visual, null);
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        SceneManager.SetActiveScene(SceneManager.CreateScene("Herbal Powder Particle Test"));
        Time.timeScale = 0f;
        var camera = new GameObject("Powder Preview Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = .68f;
        camera.transform.position = new Vector3(0, .2f, -10);
        camera.backgroundColor = new Color(.065f, .09f, .15f); camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(960,480,24); camera.targetTexture = target; camera.aspect = 2f;
        var image = new Texture2D(960,480,TextureFormat.RGBA32,false);
        var roots = new List<GameObject>();
        var systems = new List<ParticleSystem>();
        var buffer = new ParticleSystem.Particle[64];
        for (int i = 0; i < 3; i++)
        {
            var prefab = Resources.Load<GameObject>("PickupItems/" + Names[i] + "HerbPowderPickupItem");
            Check(prefab.GetComponent<PickupItemVisualBase>().InventoryIcon != null, "Inventory icon still available");
            Check(prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0, "Reading inventory icon creates no emitter");
            var root = UnityEngine.Object.Instantiate(prefab); roots.Add(root);
            root.GetComponent<PickupItemBase>().enabled = false;
            root.transform.position = new Vector3((i - 1) * .85f, 0, 0);
            var visual = root.GetComponent<HerbPowderPickupItemVisual>();
            var source = root.GetComponent<SpriteRenderer>();
            var ps = root.GetComponentInChildren<ParticleSystem>(); systems.Add(ps);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Check(ps != null && root.GetComponentsInChildren<ParticleSystem>().Length == 1, "Exactly one emitter");
            Check(ps.main.maxParticles == 32 && !ps.collision.enabled && !ps.trigger.enabled, "Bounded, decorative effect only");
            Check(ps.GetComponentsInChildren<Collider2D>().Length == 0, "Particle effect adds no hitbox");
            Check(!ps.main.useUnscaledTime && ps.main.simulationSpace == ParticleSystemSimulationSpace.World, "Pause-aware world-space drift");
            Check(renderer.sortingLayerID == source.sortingLayerID && renderer.sortingOrder == source.sortingOrder + 1 && renderer.sortingOrder < 30000, "Normal scene sorting below visibility mask");
            Check(renderer.maskInteraction == source.maskInteraction && ps.gameObject.layer == root.layer, "Inherits visibility layer and mask");
            Color tint = Get<Color>(visual, "powderColor");
            Color mid = ps.colorOverLifetime.color.gradient.Evaluate(.5f);
            Check(mid[i] > mid[(i+1)%3] && mid[i] > mid[(i+2)%3], "Correct dominant hue " + Names[i]);
            Check(Vector3.Distance(new Vector3(tint.r,tint.g,tint.b), new Vector3(mid.r,mid.g,mid.b)) < .15f, "Effect tint follows authored powder");
            Check(ps.colorOverLifetime.color.gradient.Evaluate(0).a == 0 && ps.colorOverLifetime.color.gradient.Evaluate(1).a == 0, "Gradual birth and full fade-out");
            Check(ps.colorOverLifetime.color.gradient.Evaluate(.6f).a > ps.colorOverLifetime.color.gradient.Evaluate(.9f).a, "Alpha decreases during ascent");

            root.transform.rotation = Quaternion.Euler(0,0,110);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = ps.emission; emission.enabled = false;
            ps.Emit(new ParticleSystem.EmitParams { position = root.transform.position, startLifetime = 1.6f }, 12);
            ps.Simulate(.4f, true, false, false);
            int count = ps.GetParticles(buffer); bool left = false, right = false;
            Check(count == 12, "Live particles survive initial rise");
            for (int j=0;j<count;j++)
            {
                Vector3 offset = buffer[j].position - root.transform.position;
                Check(offset.y > .02f && Mathf.Abs(offset.z) < .001f, "Every mote rises in XY even on rotated pickup: " + offset + " life=" + buffer[j].remainingLifetime);
                left |= offset.x < -.005f; right |= offset.x > .005f;
            }
            Check(left && right, "Motes spread to both sides");
            Vector3 previous = buffer[0].position;
            root.transform.position += Vector3.right * 2;
            ps.GetParticles(buffer);
            Check(Vector3.Distance(previous,buffer[0].position) < .0001f, "Existing motes are not dragged by source movement");
            root.transform.position -= Vector3.right * 2; root.transform.rotation = Quaternion.identity;
            ps.Simulate(2f, true, false, false);
            Check(ps.particleCount == 0, "Motes expire without lingering objects");
            emission.enabled = true; ps.Play(); ps.Simulate(1f,true,false,false);
            Check(ps.particleCount > 0 && ps.particleCount <= 32, "Continuous emission within budget");
            source.enabled = false; Sync(visual);
            Check(ps.particleCount == 0 && !renderer.enabled && ps.isStopped, "Hidden item clears particles");
            source.enabled = true; Sync(visual);
            Check(ps.isPlaying && renderer.enabled, "Visible item resumes emitter");
            ps.Simulate(.8f,true,false,false); visual.enabled = false;
            Check(ps.particleCount == 0 && !renderer.enabled, "Disabling visual clears effect");
            visual.enabled = true; root.SetActive(false);
            Check(ps.particleCount == 0 && ps.isStopped, "Distance unloading leaves no particles");
            root.SetActive(true);
            Check(root.GetComponentsInChildren<ParticleSystem>().Length == 1 && ps.isPlaying, "Reload reuses existing emitter");
            Set(visual, "animateParticles", false); Sync(visual);
            Check(!renderer.enabled && ps.isStopped, "Inspector animation toggle works");
            Set(visual, "animateParticles", true); Sync(visual);
            ps.Simulate(.8f,true,false,false); ps.Play();
        }
        int pausedCount = systems[0].GetParticles(buffer);
        float pausedLife = buffer[0].remainingLifetime;
        for (int frame=0;frame<5;frame++) yield return null;
        int afterCount = systems[0].GetParticles(buffer);
        Check(pausedCount == afterCount && Mathf.Abs(buffer[0].remainingLifetime - pausedLife) < .00001f, "Real game pause freezes particles");

        // Actual GPU animation preview and opaque vision-overlay coverage.
        string frames = Application.dataPath + "/../herb-powder-frames"; Directory.CreateDirectory(frames);
        foreach (var ps in systems) {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(100 + systems.IndexOf(ps)); ps.Simulate(1.6f,true,true,false);
        }
        int changingPixels = 0;
        Color32[] first = null;
        for (int frame=0;frame<48;frame++)
        {
            foreach (var ps in systems) ps.Simulate(.05f,true,false,false);
            Capture(camera,target,image);
            if (frame==0) first=image.GetPixels32();
            if (frame==20)
            {
                var pixels=image.GetPixels32();
                for(int p=0;p<pixels.Length;p++) if(!pixels[p].Equals(first[p])) changingPixels++;
                File.WriteAllBytes(Application.dataPath + "/../HerbPowderParticles-preview.png", image.EncodeToPNG());
                for(int i=0;i<3;i++)
                {
                    int visibleMotes=0;
                    for(int y=275;y<460;y++) for(int x=i*300+30;x<i*300+330;x++)
                    { Color c=pixels[y*960+x]; if(c[i] > .22f && c[i] > c[(i+1)%3]+.05f && c[i] > c[(i+2)%3]+.05f) visibleMotes++; }
                    Check(visibleMotes>10,"Actual GPU shows coloured motes above " + Names[i] + " pile");
                }
            }
            File.WriteAllBytes(Path.Combine(frames,"powder-"+frame.ToString("000")+".png"),image.EncodeToPNG());
        }
        Check(changingPixels>50,"GPU shows motion instead of static decoration");
        var mask=GameObject.CreatePrimitive(PrimitiveType.Quad); mask.transform.localScale=Vector3.one*5;
        var maskMaterial=new Material(Shader.Find("Sprites/Default")); maskMaterial.color=Color.black;
        var maskRenderer=mask.GetComponent<MeshRenderer>(); maskRenderer.sharedMaterial=maskMaterial; maskRenderer.sortingOrder=30000;
        Capture(camera,target,image);
        Check(image.GetPixel(480,330).maxColorComponent <= 1f && image.GetPixel(480,330).r < .01f && image.GetPixel(480,200).g < .01f,"Visibility overlay covers powders and motes");
        UnityEngine.Object.Destroy(mask); UnityEngine.Object.Destroy(maskMaterial);
        var oldMaterial = Get<Material>(roots[0].GetComponent<HerbPowderPickupItemVisual>(),"particleMaterial");
        var oldSystem=systems[0];
        foreach(var root in roots) UnityEngine.Object.Destroy(root);
        yield return null; yield return null;
        Check(oldMaterial==null && oldSystem==null,"Pickup/destruction cleans emitter and material");
        camera.targetTexture=null; RenderTexture.active=null;
        UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
        Time.timeScale=1f;
    }
    static void Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,960,480),0,0); image.Apply();
    }
}
