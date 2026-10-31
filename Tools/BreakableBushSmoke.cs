using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BreakableBushSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    const string PathName = "Assets/Prefabs/Decorations/Plants/BreakableBush.prefab";
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static BreakableBushSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "breakable-bush-smoke";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/BushSmoke.unity");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathName);
        Check(prefab != null && prefab.GetComponent<DoorData>().Durability == 3, "prefab imports with AP3 threshold");
        var sprite = prefab.GetComponent<SpriteRenderer>().sprite;
        Check(sprite != null && AssetDatabase.Contains(sprite) && AssetDatabase.Contains(sprite.texture), "persistent sprite and texture, no runtime art generation");
        var pixels = sprite.texture.GetPixels32();
        Check(pixels.Any(p => p.a == 0) && pixels.Count(p => p.a > 0) > pixels.Length*.65f, "bevelled foliage mask survives asset reload");
        Check(prefab.GetComponentsInChildren<SpriteRenderer>().All(r => r.sprite == sprite), "all foliage shares the same tiny sprite");
        var editInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Check(editInstance.GetComponent<SpriteRenderer>().bounds.size.x > 2f, "visible in editor before play");
        Object.DestroyImmediate(editInstance);
        SessionState.SetBool(nameof(BreakableBushSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(BreakableBushSmoke), false))
            { SessionState.SetBool(nameof(BreakableBushSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 100; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Bush tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("BREAKABLE_BUSH_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static DoorData Spawn(Vector2 position, string name)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PathName), position, Quaternion.identity);
        go.name = name;
        return go.GetComponent<DoorData>();
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        new GameObject("Test Audio Listener").AddComponent<AudioListener>();
        var bush = Spawn(Vector2.zero, "Test Bush");
        var collider = bush.GetComponent<BoxCollider2D>();
        Check(bush.GetComponentsInChildren<Collider2D>(true).Length == 1 && !collider.isTrigger && collider.enabled, "single solid collider");
        Check(collider.size == new Vector2(2.15f,1.25f) && bush.transform.localScale == Vector3.one, "large editable collider with unit prefab scale");
        Check(bush.GetComponent<Rigidbody2D>() == null && bush.GetComponent<DoorHingeInteraction>() == null, "static scenery, no opening-door behavior");
        Check(bush.GetComponentInChildren<ZeldaRequirementWindow>() != null, "existing attack requirement window installed");
        var leaves = bush.GetComponentsInChildren<SpriteRenderer>().Where(r => r.gameObject != bush.gameObject).Select(r => r.transform).ToArray();
        Check(leaves.Length == 6 && leaves.All(bush.IsAttachedVisual), "all leaf layers participate in hit shake");
        var rest = leaves.Select(t => t.localPosition).ToArray();
        var originalSprite = bush.GetComponent<SpriteRenderer>().sprite;
        Physics2D.SyncTransforms();
        Check(Physics2D.Raycast(new Vector2(-3,0), Vector2.right, 6).collider == collider, "blocks passage at broad middle");
        Check(Physics2D.Raycast(new Vector2(-3,.5f), Vector2.right, 6).collider == collider, "blocks passage near outer edge");
        for (int i = 0; i < 12; i++) bush.ReceiveAttack(i % 2 + 1);
        yield return null; yield return null;
        Check(!bush.IsDestroyed && collider.enabled, "repeated AP1/AP2 hits never accumulate destruction");
        Check(bush.transform.position == Vector3.zero && collider.offset == new Vector2(0,-.025f), "hit shake cannot displace collider");
        Check(leaves.Where((t,i) => (t.localPosition - rest[i]).sqrMagnitude > .000001f).Count() == 6, "entire bush shakes together");
        Check(bush.GetComponentsInChildren<SpriteRenderer>().Length == 8, "repeated hits share one temporary shake renderer");
        float until = Time.time + .35f;
        while (Time.time < until) yield return null;
        Check(leaves.Where((t,i) => t.localPosition != rest[i]).Count() == 0, "leaves return to rest");
        Check(bush.GetComponentsInChildren<SpriteRenderer>().Length == 7 && !bush.GetComponent<SpriteRenderer>().forceRenderingOff, "temporary shake visuals removed");
        bush.gameObject.SetActive(false); bush.gameObject.SetActive(true);
        Check(bush.Durability == 3 && bush.GetComponent<SpriteRenderer>().sprite == originalSprite && collider.enabled, "streaming reload keeps art/collision/threshold");
        Preview();

        var manager = SceneTravelStateManager.GetOrCreate();
        var scene = SceneManager.GetActiveScene();
        var intact = Call(manager, "CreateSceneSnapshot", scene);
        Set(bush, "durability", 9);
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, intact, false), "existing scene snapshot applies");
        Check(bush.Durability == 3, "DoorData threshold survives saved state restoration");

        // Use the real hitbox trigger path, not only a direct DoorData call.
        var hit = new GameObject("Bush AP3 Attack").AddComponent<ZeldaAttackHitbox>();
        hit.Configure(.4f, Vector2.one, Color.white, 3, null, ZeldaAttackVisualShape.Box);
        until = Time.time + .2f;
        while (Time.time < until) yield return null;
        Check(bush == null, "AP3 physical hitbox destroys complete bush hierarchy");
        Check(Physics2D.RaycastAll(new Vector2(-3,.5f), Vector2.right, 2.5f).All(h => h.collider.isTrigger), "destroyed bush no longer blocks passage (ignore live attack trigger)");
        var fragments = Object.FindObjectsOfType<DoorFragmentVisual>();
        Check(fragments.Length == 0, "bush does not reuse door debris");
        var effect = GameObject.Find("Bush Destruction");
        Check(effect != null && effect.GetComponentsInChildren<ParticleSystem>().Length == 3, "dedicated leaves/twigs/dust effect outlives bush");
        Check(effect.GetComponentsInChildren<Collider2D>().Length == 0, "debris never obstructs characters");
        Check(effect.GetComponent<CameraVisionStreamingExempt>() != null && effect.hideFlags == HideFlags.DontSave, "effect is transient and not distance-unloaded mid-animation");
        Check(effect.GetComponentsInChildren<ParticleSystem>().All(p => p.particleCount > 0 && !p.main.loop && !p.collision.enabled), "all custom debris layers are playing");
        var audio = effect.GetComponent<AudioSource>();
        Check(audio != null && audio.isPlaying && audio.clip.name == "BushBreak" && !audio.loop && audio.spatialBlend == 1 && audio.maxDistance == 18,
            "unique bush sound plays once and attenuates over distance");
        Check(!Object.FindObjectsOfType<AudioSource>().Any(a => a.isPlaying && a.clip != null && a.clip.name == "DoorBreak"), "no duplicate generic door audio");
        EffectPreview(effect);
        until = Time.time + 1.5f;
        while (Time.time < until) yield return null;
        Check(Object.FindObjectsOfType<DoorFragmentVisual>().Length == 0, "fragments finish fading and clean up");
        Check(effect == null, "custom VFX/audio root cleans up");

        var broken = Call(manager, "CreateSceneSnapshot", scene);
        var sceneReloadBush = Spawn(Vector2.zero, "Test Bush");
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, broken, false), "destroyed snapshot restores");
        Check(!sceneReloadBush.gameObject.activeSelf, "destroyed scene bush stays absent after restoration");
        Object.Destroy(sceneReloadBush.gameObject);
        var strong = Spawn(new Vector2(8,0), "Strong hit bush");
        strong.ReceiveAttack(7);
        Check(strong.IsDestroyed && !strong.GetComponent<BoxCollider2D>().enabled, "AP above 3 removes collision immediately");
        yield return null; yield return null;
        Check(strong == null, "higher power also removes all leaf layers");
        var normalDoor = new GameObject("Legacy Door").AddComponent<DoorData>();
        normalDoor.GetComponent<SpriteRenderer>().sprite = originalSprite;
        normalDoor.ReceiveAttack(3);
        Check(GameObject.Find("Wood Door Debris") != null && Object.FindObjectsOfType<DoorFragmentVisual>().Length == 0,
            "normal DoorData uses its own vector debris, not bush effects");
        Check(Object.FindObjectsOfType<AudioSource>().Any(a => a.isPlaying && a.clip != null && a.clip.name == "DoorBreak"), "normal DoorData retains original audio");
        var glass = new GameObject("Legacy Glass").AddComponent<DoorData>();
        glass.GetComponent<SpriteRenderer>().sprite = originalSprite;
        Set(glass, "breakMaterial", DoorData.BreakMaterial.Glass);
        glass.ReceiveAttack(3);
        Check(GameObject.Find("Glass Door Debris") != null, "glass uses its own vector shards");
        Check(Object.FindObjectsOfType<AudioSource>().Any(a => a.isPlaying && a.clip != null && a.clip.name == "GlassBreak"), "glass retains its own original audio");
    }
    static void EffectPreview(GameObject effect)
    {
        var effectObjects = effect.GetComponentsInChildren<Transform>();
        var layers = effectObjects.Select(t => t.gameObject.layer).ToArray();
        foreach (var item in effectObjects) item.gameObject.layer = 31;
        var camera = new GameObject("Bush Effect Preview").AddComponent<Camera>(); camera.enabled = false;
        camera.cullingMask = 1 << 31;
        camera.orthographic = true; camera.orthographicSize = 1.8f;
        camera.transform.position = new Vector3(0,0,-10); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.047f,.122f,.188f,1);
        var rt = RenderTexture.GetTemporary(640,420,24,RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
        camera.targetTexture = rt; camera.aspect = 640f/420; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(640,420,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,640,420),0,0); image.Apply();
        File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../BreakableBush-effect-preview.png"),image.EncodeToPNG());
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.Destroy(camera.gameObject);
        for (int i = 0; i < effectObjects.Length; i++) effectObjects[i].gameObject.layer = layers[i];
    }
    static void Preview()
    {
        var left = Spawn(new Vector2(18.35f,0), "Preview Left");
        var right = Spawn(new Vector2(21.65f,0), "Preview Right");
        var collider = right.GetComponent<BoxCollider2D>();
        var line = new GameObject("Collider Preview").AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(.2f,.9f,1,1);
        line.startWidth = line.endWidth = .015f; line.sortingOrder = 20; line.loop = true;
        var c = right.transform.position + (Vector3)collider.offset; var half = collider.size * .5f;
        line.positionCount = 4;
        line.SetPositions(new[] { c+new Vector3(-half.x,-half.y), c+new Vector3(half.x,-half.y), c+new Vector3(half.x,half.y), c+new Vector3(-half.x,half.y) });
        var camera = new GameObject("Bush Preview Camera").AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 1.6f;
        camera.transform.position = new Vector3(20,.05f,-10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.047f,.122f,.188f,1);
        var rt = RenderTexture.GetTemporary(960,420,24,RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.targetTexture = rt; camera.aspect = 960f/420; camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(960,420,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,960,420),0,0); image.Apply();
        File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../BreakableBush-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(left.gameObject); Object.Destroy(right.gameObject); Object.Destroy(line.material); Object.Destroy(line.gameObject); Object.Destroy(camera.gameObject);
    }
}
