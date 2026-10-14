using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in an isolated Unity project with the current production assets.
public static class GoldenRuneSealSmoke
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    static T Field<T>(GeometricFortressDecoration component, string name)
    {
        return (T)typeof(GeometricFortressDecoration).GetField(name, Private).GetValue(component);
    }
    static void Invoke(GeometricFortressDecoration component, string name, params object[] args)
    {
        typeof(GeometricFortressDecoration).GetMethod(name, Private).Invoke(component, args);
    }
    static GameObject Spawn(string name, float x)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/" + name + ".prefab");
        Check(asset != null, name + " asset exists");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.transform.position = new Vector3(x, 0, 0);
        return go;
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var red = Spawn("Fortress-RuneSeal", -1.45f);
            var gold = Spawn("Fortress-RuneSeal-Gold", 1.45f);
            var redArt = red.GetComponent<GeometricFortressDecoration>();
            var art = gold.GetComponent<GeometricFortressDecoration>();
            Invoke(redArt, "Rebuild"); Invoke(art, "Rebuild");
            Color accent = Field<Color>(art, "accentColor");
            Check(accent == new Color(1, .68f, .12f, 1), "gold palette");
            Check(Field<Color>(redArt, "accentColor") == new Color(1, .14f, .08f, 1), "red unchanged");
            Check(Field<Vector2>(art, "size") == new Vector2(1.8f, 1.8f), "original size");
            Check(!gold.GetComponent<BoxCollider2D>().enabled, "decoration remains non-solid");
            Check(Field<Mesh>(art, "mesh").vertexCount == Field<Mesh>(redArt, "mesh").vertexCount, "same geometry budget");
            Vector3[] a = Field<Mesh>(art, "mesh").vertices, b = Field<Mesh>(redArt, "mesh").vertices;
            for (int i = 0; i < a.Length; i++) Check(a[i] == b[i], "same geometry vertex " + i);
            foreach (var renderer in gold.GetComponentsInChildren<Renderer>())
                Check(renderer.sortingOrder <= -2 && renderer.sortingLayerName == "Default", "world sorting retained");
            float period = Field<float>(art, "runeBreathPeriod"), depth = Field<float>(art, "runeBreathDepth");
            Check(period == 3.2f && depth == .35f, "breathing configuration");
            Check(Mathf.Abs(GeometricFortressDecoration.RuneBreathMultiplier(period * .25f, period, depth) - 1.35f) < .001f, "breathing peak");
            Check(Mathf.Abs(GeometricFortressDecoration.RuneBreathMultiplier(period * .75f, period, depth) - .65f) < .001f, "breathing trough");
            foreach (var c in Field<Mesh>(art, "effectMesh").colors)
                Check(c.r > .99f && c.g > .67f && c.g < .69f && c.b < .13f, "gold glow vertex color");

            // Explicitly build play-mode particles in this disposable editor scene.
            Invoke(art, "BuildRuneParticles", Vector2.one);
            var particles = Field<ParticleSystem>(art, "runeParticles");
            Check(particles.main.maxParticles == 16 && particles.emission.rateOverTime.constant == 2.5f, "same particle budget");
            foreach (var glyph in Field<Mesh[]>(art, "runeGlyphMeshes"))
                foreach (var c in glyph.colors32)
                    Check(c.r == 255 && c.g >= 172 && c.g <= 174 && c.b <= 31, "packed gold glyph color");
            Check(particles.GetComponent<Renderer>().sharedMaterial == Field<Material>(art, "effectMaterial"), "particles share breathing glow");
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = 12;
            var main = particles.main; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            particles.Simulate(1.6f, true, true, true);
            Check(particles.particleCount > 0, "particles emitted");

            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 1.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .045f, .105f, 1);
            var rt = new RenderTexture(1100, 560, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            Texture2D preview = Render(camera, rt);
            File.WriteAllBytes(Application.dataPath + "/../GoldenRuneSeal-preview.png", preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);

            // Check the actual GPU particle output separately, not just serialized colors.
            particles.gameObject.layer = 30; camera.cullingMask = 1 << 30;
            camera.transform.position = new Vector3(1.45f, 0, -10);
            Texture2D particleImage = Render(camera, rt);
            int goldPixels = 0, bluePixels = 0;
            foreach (var c in particleImage.GetPixels())
            {
                if (c.r > .15f && c.g > .10f && c.r > c.b * 1.7f && c.g > c.b * 1.2f) goldPixels++;
                if (c.b > .25f && c.b > c.r * 1.4f) bluePixels++;
            }
            Check(goldPixels > 20 && bluePixels == 0, "GPU particles are gold, not blue: " + goldPixels + "/" + bluePixels);
            File.WriteAllBytes(Application.dataPath + "/../GoldenRuneSeal-particles.png", particleImage.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(particleImage);
            RenderTexture.active = null; camera.targetTexture = null; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(gold); UnityEngine.Object.DestroyImmediate(red);
            Debug.Log("GOLDEN RUNE SEAL PASS " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Texture2D Render(Camera camera, RenderTexture rt)
    {
        camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        return image;
    }
}
