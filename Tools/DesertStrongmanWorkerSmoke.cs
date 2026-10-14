using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Execute in the isolated validation project; never save a scene or prefab.
[InitializeOnLoad]
public static class DesertStrongmanWorkerSmoke
{
    static IEnumerator test;
    static int checks, frame = -1;
    static DesertStrongmanWorkerSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("DesertStrongmanWorkerSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("DesertStrongmanWorkerSmoke", false))
            { SessionState.SetBool("DesertStrongmanWorkerSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Debug.Log("DESERT_STRONGMAN_WORKER_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        foreach (bool patrol in new[] { false, true })
        {
            string path = "Assets/Prefabs/Level2NPCS/" + (patrol ? "Patrol/Strongman-level2-Patrol.prefab" : "Idle/Strongman-level2.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab, "prefab imports");
            var root = Object.Instantiate(prefab);
            foreach (var c in root.GetComponentsInChildren<Component>(true)) Check(c, "no missing components");
            var data = root.GetComponent<DesertStrongmanZeldaCharacterData>();
            Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "unchanged character data type");
            var authored = new SerializedObject(prefab.GetComponent<DesertStrongmanZeldaCharacterData>());
            Check(data.CurrentHealth == authored.FindProperty("health").intValue && data.MoveSpeed == authored.FindProperty("moveSpeed").floatValue, "independent authored variant stats");
            Check(data.AttackPower == 4 && data.AttackVisualShape == ZeldaAttackVisualShape.Hammer && data.AttackPrefab, "hammer attack preserved");
            Check(root.transform.localScale == Vector3.one * 1.1f && !root.GetComponent<BoxCollider2D>().isTrigger, "size and collision preserved");
            Check(root.GetComponent<ZeldaCharacterAiBase>().GetType() == (patrol ? typeof(ZeldaPatrolCharacterAi) : typeof(ZeldaCharacterAiBase)), "AI variant preserved");
            var renderer = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
            foreach (var facing in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
            {
                data.ApplyCharacterVisual(renderer, facing, false, false); var idle = renderer.sprite;
                Check(idle && idle.pixelsPerUnit == 16 && idle.pivot == new Vector2(10, 6) && idle.texture.isReadable && idle.texture.filterMode == FilterMode.Point, "unchanged pixel grid and pivot");
                data.ApplyCharacterVisualWithMovement(renderer, facing, true, false);
                Check(renderer.sprite != idle, "walk frame available");
                data.ApplyCharacterVisual(renderer, facing, false, true);
                Check(renderer.sprite != idle, "attack arm frame available");
                data.ApplyCharacterVisual(renderer, facing, false, false); Check(renderer.sprite == idle, "idle cached, no per-frame texture creation");
            }
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var px = renderer.sprite.texture.GetPixels();
            Check(px[17 * 20 + 8].r > .7f && px[17 * 20 + 8].b < .2f, "red sweatband");
            Check(px[16 * 20 + 7].r < px[15 * 20 + 7].r * .4f, "distinct eyes, clear cheek underneath");
            var config = new SerializedObject(data);
            foreach (string field in new[] { "tunicColor", "headclothColor", "trousersColor" })
            {
                data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
                var before = renderer.sprite.texture.GetPixels32();
                var property = config.FindProperty(field); Color original = property.colorValue;
                property.colorValue = Color.magenta; config.ApplyModifiedPropertiesWithoutUndo();
                typeof(DesertStrongmanZeldaCharacterData).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(data, null);
                data.ApplyCharacterVisualWithMovement(renderer, Vector2.down, true, false);
                data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
                var after = renderer.sprite.texture.GetPixels32(); int changed = 0;
                for (int i = 0; i < 400; i++)
                { Check(before[i].a == after[i].a, "palette changes do not alter silhouette"); if (!before[i].Equals(after[i])) changed++; }
                Check(changed > 3, "editable workwear palette and safe walk cache invalidation");
                property.colorValue = original; config.ApplyModifiedPropertiesWithoutUndo();
            }
            var state = data.CaptureRuntimeState();
            Check(state.characterType.Contains("DesertStrongmanZeldaCharacterData"), "save identity preserved");
            data.ApplyRuntimeState(state); root.SetActive(false); root.SetActive(true);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.left, true, false);
            Check(renderer.sprite, "streaming reactivation visual");
            Object.Destroy(root);
            yield return null;
        }
    }
}
