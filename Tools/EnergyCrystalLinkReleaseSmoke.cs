using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnergyCrystalLinkReleaseSmoke
{
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static EnergyCrystalLinkReleaseSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("EnergyCrystalLinkReleaseSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    public static void Preview()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var crystal = Prefab<EnergyCrystal>("EnergyCrystal");
            crystal.transform.position = new Vector3(-3, 0);
            foreach (float y in new[] { -1.4f, 1.4f })
            {
                var skeleton = Skeleton(new Vector2(3, y));
                skeleton.GetComponent<SpriteRenderer>().sprite = skeleton.GetPreviewSprite();
                crystal.BindSkeleton(skeleton);
            }
            var visual = crystal.GetComponent<EnergyCrystalVisual>();
            Call(visual, "WriteLinks", .7f);
            var source = crystal.transform.Find(EnergyCrystalVisual.LinkMeshName).GetComponent<MeshFilter>();
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 3.1f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(19, 27, 39, 255);
            var target = new RenderTexture(600, 400, 24);
            camera.targetTexture = target;
            var frameTexture = new Texture2D(600, 400, TextureFormat.RGB24, false);
            var strip = new Texture2D(2400, 400, TextureFormat.RGB24, false);
            for (int i = 0; i < 4; i++)
            {
                if (i == 1)
                {
                    EnergyCrystalLinkReleaseVisual.Spawn(source, 1.05f, 1.15f);
                    visual.SetBroken(true);
                }
                if (i > 0) Call(Effects()[0], "WriteFrame", new[] { 0f, .22f, .55f, .9f }[i]);
                camera.Render(); RenderTexture.active = target;
                frameTexture.ReadPixels(new Rect(0, 0, 600, 400), 0, 0); frameTexture.Apply();
                strip.SetPixels(i * 600, 0, 600, 400, frameTexture.GetPixels());
            }
            strip.Apply();
            string output = System.IO.Path.GetFullPath(Application.dataPath + "/../../../.codex-temp/EnergyCrystalLinkRelease-preview.png");
            System.IO.File.WriteAllBytes(output, strip.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(frameTexture); Object.DestroyImmediate(strip);
            Debug.Log("ENERGY_CRYSTAL_LINK_RELEASE_PREVIEW " + output); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("EnergyCrystalLinkReleaseSmoke", false))
            {
                SessionState.SetBool("EnergyCrystalLinkReleaseSmoke", false);
                steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("ENERGY_CRYSTAL_LINK_RELEASE_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static T Prefab<T>(string file) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/" + file + ".prefab");
        Check(prefab != null, "Prefab exists: " + file);
        return Object.Instantiate(prefab).GetComponent<T>();
    }
    static SkeletonZeldaCharacterData Skeleton(Vector2 position)
    {
        var skeleton = Prefab<SkeletonZeldaCharacterData>("Skeleton-level2");
        skeleton.GetComponent<SkeletonCharacterAi>().enabled = false;
        skeleton.GetComponent<ZeldaFourWayMover>().enabled = false;
        skeleton.transform.position = position;
        skeleton.GetComponent<Rigidbody2D>().position = position;
        return skeleton;
    }
    static EnergyCrystalLinkReleaseVisual[] Effects() => Object.FindObjectsOfType<EnergyCrystalLinkReleaseVisual>();
    static Vector3[] WorldVertices(MeshFilter f) => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray();
    static IEnumerator Until(Func<bool> ready, string label)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!ready()) { if (Time.realtimeSinceStartup > deadline) throw new Exception(label); yield return null; }
        Check(true, label);
    }
    static void Clear()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.DestroyImmediate(root);
    }

    static IEnumerator Verify()
    {
        Time.timeScale = 1; Time.captureFramerate = 60;
        foreach (float scaleSign in new[] { 1f, -1f })
        {
            var crystal = Prefab<EnergyCrystal>("EnergyCrystal");
            crystal.transform.position = new Vector3(3, 4, 2);
            crystal.transform.rotation = Quaternion.Euler(0, 0, 32);
            crystal.transform.localScale = new Vector3(scaleSign * 1.4f, .7f, 1);
            var visual = crystal.GetComponent<EnergyCrystalVisual>();
            var a = Skeleton(new Vector2(8, 4));
            var b = Skeleton(new Vector2(1, -2));
            var sleeping = Skeleton(new Vector2(-40, 4));
            crystal.BindSkeleton(a); crystal.BindSkeleton(b); crystal.BindSkeleton(sleeping);
            sleeping.gameObject.SetActive(false);
            Call(visual, "WriteLinks", .7f);
            var source = crystal.transform.Find(EnergyCrystalVisual.LinkMeshName).GetComponent<MeshFilter>();
            Vector3[] original = WorldVertices(source);
            var originalColors = source.sharedMesh.colors;
            var originalMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            var state = UnityEngine.Random.state;
            crystal.ReceiveAttack(1);
            Check(state.Equals(UnityEngine.Random.state), "Visual randomness does not perturb gameplay RNG");
            Check(crystal.IsBroken && !a.CanReassemble && !b.CanReassemble, "Breaking still removes resurrection immediately");
            Check(Effects().Length == 1, "All active links use one detached effect");
            var release = Effects()[0];
            Check(release.gameObject.scene == crystal.gameObject.scene && release.transform.parent == null, "Scene-local independent root");
            Check(release.GetComponent<CameraVisionStreamingExempt>(), "Scatter is not distance-unloaded");
            Check(release.GetComponentsInChildren<Collider2D>().Length == 0, "No particle collision objects");
            Check(!source.gameObject.activeInHierarchy, "Old tether immediately hidden");
            var mesh = release.GetComponentInChildren<MeshFilter>();
            var renderer = release.GetComponentInChildren<MeshRenderer>();
            Check(renderer.sortingOrder == -1 && renderer.gameObject.layer == source.gameObject.layer, "Original world masking/sorting retained");
            Check(renderer.sharedMaterial != originalMaterial && renderer.sharedMaterial.GetFloat("_Intensity") == originalMaterial.GetFloat("_Intensity"), "Independent matching glow material");
            Vector3[] initial = WorldVertices(mesh);
            Check(initial.Length == original.Length && initial.Length > 32, "Every visible bead retained, none added");
            for (int i = 0; i < initial.Length; i++)
            {
                Check(Vector3.Distance(initial[i], original[i]) < .0001f, "No handover snap with rotated/mirrored/nonuniform scale");
                Check(mesh.sharedMesh.colors[i] == originalColors[i], "Original green/alpha retained at release");
            }
            Check(release.transform.childCount == 1, "One mesh child, not per-particle GameObjects");
            crystal.ReceiveAttack(10); Check(Effects().Length == 1, "Repeated damage does not emit twice");
            var brokenState = crystal.CaptureRuntimeState(); crystal.ApplyRuntimeState(brokenState);
            Check(Effects().Length == 1, "Restoring broken state does not emit");
            crystal.gameObject.SetActive(false); crystal.gameObject.SetActive(true);
            Check(Effects().Length == 1 && !source.gameObject.activeSelf, "Re-enable does not replay intact links");

            Call(release, "WriteFrame", .3f);
            Vector3[] scattered = WorldVertices(mesh);
            Check(scattered.Length == original.Length, "Particles remain visible long enough to scatter");
            var quadrants = new HashSet<int>();
            for (int i = 0; i < scattered.Length; i += 4)
            {
                var delta = (scattered[i] + scattered[i + 2] - original[i] - original[i + 2]) * .5f;
                Check(delta.magnitude > .05f, "Particles drift away from their own tether positions");
                quadrants.Add((delta.x > 0 ? 1 : 0) | (delta.y > 0 ? 2 : 0));
                Check(mesh.sharedMesh.colors[i].a < originalColors[i].a, "Glow gradually fades");
                Check(mesh.sharedMesh.colors[i].g > mesh.sharedMesh.colors[i].r, "Scattered beads remain green");
            }
            Check(quadrants.Count == 4, "Particles scatter in every direction, not toward one endpoint");
            crystal.transform.position += Vector3.one * 100;
            a.transform.position += Vector3.right * 100;
            Call(release, "WriteFrame", .3f);
            Check(WorldVertices(mesh).SequenceEqual(scattered), "Endpoints moving cannot drag released particles");
            Object.DestroyImmediate(crystal.gameObject);
            Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject);
            Object.DestroyImmediate(sleeping.gameObject);
            Check(release != null && renderer.sharedMaterial != null, "Survives destruction of source and skeletons");
            Time.timeScale = 0; yield return null; yield return null;
            float frozen = (float)typeof(EnergyCrystalLinkReleaseVisual).GetField("elapsed", Flags).GetValue(release);
            yield return null; yield return null;
            Check((float)typeof(EnergyCrystalLinkReleaseVisual).GetField("elapsed", Flags).GetValue(release) == frozen, "Pause freezes scatter timer");
            Time.timeScale = 1;
            var ownedMesh = mesh.sharedMesh; var ownedMaterial = renderer.sharedMaterial;
            yield return Until(() => release == null, "Scatter automatically ends");
            yield return null;
            Check(ownedMesh == null && ownedMaterial == null, "Temporary mesh and material cleaned up");
            Clear(); yield return null;
        }
        // No visible tether: no new visual. Save-state loading remains entirely silent.
        var empty = Prefab<EnergyCrystal>("EnergyCrystal");
        empty.ReceiveAttack(1); Check(Effects().Length == 0, "Unbound crystal has nothing to release");
        Clear(); yield return null;
        var restored = Prefab<EnergyCrystal>("EnergyCrystal");
        var member = Skeleton(Vector2.right * 3); restored.BindSkeleton(member);
        Call(restored.GetComponent<EnergyCrystalVisual>(), "WriteLinks", 0f);
        restored.ApplyRuntimeState(new EnergyCrystal.RuntimeState { broken = true });
        Check(Effects().Length == 0, "Loading a broken crystal never scatters particles");
        Clear(); yield return null;
        var offloaded = Prefab<EnergyCrystal>("EnergyCrystal");
        member = Skeleton(Vector2.right * 3); offloaded.BindSkeleton(member);
        Call(offloaded.GetComponent<EnergyCrystalVisual>(), "WriteLinks", 0f);
        member.gameObject.SetActive(false); offloaded.gameObject.SetActive(false);
        offloaded.ReceiveAttack(1);
        Check(Effects().Length == 0, "Offloaded stale links do not appear during damage");
        Clear(); yield return null;
    }
}
