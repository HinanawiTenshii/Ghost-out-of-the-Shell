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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DoorDestructionVisualSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DoorDestructionVisualSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    static object Get(object o, string name) => o.GetType().GetField(name,F).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name,F).SetValue(o,value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name,F).Invoke(o,args);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "door-vector-debris-smoke";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/DoorVectorSmoke.unity");
        SessionState.SetBool(nameof(DoorDestructionVisualSmoke),true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(DoorDestructionVisualSmoke),false))
            { SessionState.SetBool(nameof(DoorDestructionVisualSmoke),false); suite = Verify(); deadline = EditorApplication.timeSinceStartup+120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Debris test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DOOR_VECTOR_DEBRIS_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        new GameObject("Test Listener").AddComponent<AudioListener>();
        var doors = new List<DoorData>();
        string[] names = { "DoorHinge", "DoubleDoorHinge", "DoubleSlidingDoor", "Glass" };
        for (int i = 0; i < names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/"+names[i]+".prefab");
            Check(prefab != null,"prefab imports "+names[i]);
            var instance = Object.Instantiate(prefab,new Vector3(i*8,0),Quaternion.identity);
            var leaves = instance.GetComponentsInChildren<DoorData>(true);
            Check(leaves.Length == (i == 1 || i == 2 ? 2 : 1),"same panel count "+names[i]);
            doors.AddRange(leaves);
            foreach(var door in leaves)
            {
                int threshold = door.Durability;
                Check(threshold == (i == 2 ? 4 : i == 3 ? 2 : 1),"original attack threshold");
                door.ReceiveAttack(threshold-1);
                Check(!door.IsDestroyed && door.GetComponent<Collider2D>().enabled,"weak attack only shakes");
            }
        }
        float end = Time.time+.4f;
        while(Time.time<end) yield return null;
        var manager = SceneTravelStateManager.GetOrCreate();
        object snapshot = Call(manager,"CreateSceneSnapshot",SceneManager.GetActiveScene());
        Set(doors[0],"fragmentSpeed",99f);
        Check((bool)Call(manager,"RestoreSceneSnapshot",SceneManager.GetActiveScene(),snapshot,false),"old-style scene snapshot restores");
        Check(Mathf.Abs((float)Get(doors[0],"fragmentSpeed")-4.2f)<.001f,"existing fragment controls remain compatible with saves");
        var outputs = new List<GameObject>();
        foreach(var door in doors)
        {
            var source = door.GetComponent<SpriteRenderer>();
            var scene = door.gameObject.scene;
            bool glass = door.IsBreakableGlass;
            bool metal = door.GetComponentInParent<DoubleSlidingDoor>() != null;
            var color = source.color;
            door.ReceiveAttack(door.Durability);
            Check(door.IsDestroyed && !door.GetComponent<Collider2D>().enabled,"collision removed immediately");
            var effect = GameObject.Find((glass ? "Glass" : metal ? "Metal" : "Wood")+" Door Debris");
            // Several doors share a material: select the newly-created, untested root.
            effect = scene.GetRootGameObjects().First(g=>g.name.EndsWith(" Door Debris") && !outputs.Contains(g));
            outputs.Add(effect);
            Check(effect.scene == scene && effect.hideFlags == HideFlags.DontSave,"transient scene-local VFX");
            Check(effect.GetComponentsInChildren<Collider2D>(true).Length == 0,"debris cannot block gameplay");
            Check(effect.GetComponentsInChildren<MonoBehaviour>().Length == 1,"no per-piece behavior/update allocations");
            var systems = effect.GetComponentsInChildren<ParticleSystem>();
            Check(systems.Length == 3 && systems.Sum(p=>p.particleCount) == 33,"bounded 18 fragments + 9 chips + 6 fine pieces");
            foreach(var ps in systems)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Check(renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh.vertexCount <= 6,"simple native vector polygon silhouettes");
                Check(renderer.sharedMaterial.mainTexture == Texture2D.whiteTexture,"solid colours, no pixel textures");
                Check(!renderer.enableGPUInstancing,"sprite material uses compatible mesh vertex colour layout");
                Check(!ps.collision.enabled && !ps.main.loop && ps.main.simulationSpace == ParticleSystemSimulationSpace.World,"one-shot nonphysical particles");
                var particles = new ParticleSystem.Particle[ps.main.maxParticles]; int count = ps.GetParticles(particles);
                Check(count>0 && particles.Take(count).All(p=>Mathf.Abs(p.position.z-source.transform.position.z)<.001f),"fragments stay in 2D plane");
                if(!glass && !metal) Check(particles.Take(count).All(p=>((Color)p.startColor).r>((Color)p.startColor).b),"wood retains warm door tint");
            }
            Check(effect.GetComponentInChildren<AudioSource>() == null,"VFX never adds duplicate sound");
        }
        Check(Object.FindObjectsOfType<DoorFragmentVisual>().Length == 0,"generic DoorData no longer spawns per-piece GameObjects");
        Check(Object.FindObjectsOfType<AudioSource>().Count(a=>a.isPlaying && a.clip!=null && a.clip.name=="DoorBreak")==5,"original wood/sliding door audio unchanged");
        Check(Object.FindObjectsOfType<AudioSource>().Count(a=>a.isPlaying && a.clip!=null && a.clip.name=="GlassBreak")==1,"original glass audio unchanged");
        end = Time.time+1.15f;
        while(Time.time<end) yield return null;
        Check(outputs.All(g=>g==null),"all particle roots clean up");
        Check(doors.All(d=>d==null),"all destroyed panels removed");

        // Existing custom bush feedback is still authoritative.
        var bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Plants/BreakableBush.prefab");
        var bush = Object.Instantiate(bushPrefab,new Vector3(40,0),Quaternion.identity).GetComponent<DoorData>();
        bush.ReceiveAttack(3);
        Check(GameObject.Find("Bush Destruction") != null && GameObject.Find("Wood Door Debris") == null,"bush keeps dedicated effects");
        Check(GameObject.Find("Bush Destruction").GetComponent<AudioSource>().clip.name=="BushBreak","bush keeps its sound");

        // Observe the actual particle meshes at several normalized ages.
        Preview();
        var sample = new GameObject("Rotated Old Door").AddComponent<SpriteRenderer>();
        sample.sprite = bushPrefab.GetComponent<SpriteRenderer>().sprite;
        sample.transform.localScale = new Vector3(3,.15f,1); sample.transform.rotation = Quaternion.Euler(0,0,67);
        var sampleEffect = DoorDestructionVisual.Play(sample,DoorDestructionVisual.Surface.Glass,18,.85f,4.2f,.2f);
        foreach(var ps in sampleEffect.GetComponentsInChildren<ParticleSystem>())
        {
            var particles = new ParticleSystem.Particle[ps.main.maxParticles]; int n = ps.GetParticles(particles);
            Check(particles.Take(n).All(p=>sample.sprite.bounds.Contains(sample.transform.InverseTransformPoint(p.position))),"rotated/nonuniform panel particles start inside source silhouette");
        }
        var renderer2 = sampleEffect.GetComponentInChildren<ParticleSystemRenderer>();
        var shared = renderer2.sharedMaterial; var mesh = renderer2.mesh;
        var another = DoorDestructionVisual.Play(sample,DoorDestructionVisual.Surface.Glass,500,.85f,4.2f,.2f);
        Check(another.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.main.maxParticles)<=74,"large configured counts remain bounded");
        Check(another.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial==shared && another.GetComponentInChildren<ParticleSystemRenderer>().mesh==mesh,"cached material and polygon meshes reused");
        Object.Destroy(sampleEffect); Object.Destroy(another); Object.Destroy(sample.gameObject);
    }
    static void Preview()
    {
        var source = new GameObject("Preview Source").AddComponent<SpriteRenderer>();
        var sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Glass.prefab").GetComponent<SpriteRenderer>().sprite;
        source.sprite = sprite; source.enabled = false; source.transform.localScale = new Vector3(2.1f,.2f,1);
        var effects = new List<GameObject>();
        var surfaces = new[] {DoorDestructionVisual.Surface.Wood,DoorDestructionVisual.Surface.Glass,DoorDestructionVisual.Surface.Metal};
        var colors = new[] {new Color(.68f,.39f,.18f),new Color(.076f,.78f,.934f),new Color(.65f,.72f,.75f)};
        for(int row=0;row<3;row++) for(int col=0;col<3;col++)
        {
            source.transform.position = new Vector3(100+(col-1)*4.2f,(1-row)*2.4f,0); source.color=colors[col];
            var effect = DoorDestructionVisual.Play(source,surfaces[col],18,.85f,4.2f,.2f); effects.Add(effect);
            foreach(var t in effect.GetComponentsInChildren<Transform>()) t.gameObject.layer=31;
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(.04f+row*.28f,false,false,true);
        }
        var camera = new GameObject("Vector Debris Preview").AddComponent<Camera>(); camera.enabled=false; camera.cullingMask=1<<31;
        camera.transform.position=new Vector3(100,0,-10); camera.orthographic=true; camera.orthographicSize=3.8f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.047f,.122f,.188f,1);
        var rt=RenderTexture.GetTemporary(1260,760,24,RenderTextureFormat.ARGB32); var previous=RenderTexture.active;
        camera.targetTexture=rt; camera.aspect=1260f/760; camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(1260,760,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1260,760),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../DoorVectorDebris-preview.png"),image.EncodeToPNG());
        var pixels = image.GetPixels();
        for(int col=0;col<3;col++)
        {
            int visible = 0;
            for(int y=500;y<760;y++) for(int x=col*420;x<(col+1)*420;x++)
            {
                var c = pixels[y*1260+x];
                if(col==0 ? c.r>.25f && c.r>c.b*1.3f : col==1 ? c.g>.4f && c.b>.4f : c.r>.35f && c.g>.4f) visible++;
            }
            Check(visible>60,"rendered debris preserves material palette column "+col);
        }
        Object.Destroy(image); camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(camera.gameObject); Object.Destroy(source.gameObject); foreach(var effect in effects) Object.Destroy(effect);
    }
}
