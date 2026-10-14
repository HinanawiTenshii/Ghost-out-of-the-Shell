using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Copy into the isolated validation project's Assets/Editor to run.
[InitializeOnLoad]
public static class ScribeSmoke
{
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ScribeSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("ScribeSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("ScribeSmoke", false))
            { SessionState.SetBool("ScribeSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("SCRIBE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Clear()
    { foreach (var go in owned) if (go) Object.DestroyImmediate(go); owned.Clear(); }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        foreach (bool patrol in new[] { false, true })
        {
            string path = "Assets/Prefabs/Level2NPCS/" + (patrol ? "Patrol/Scribe-level2.prefab" : "Idle/Scribe-level2-Idle.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab, "prefab imports: " + path);
            var root = Object.Instantiate(prefab); owned.Add(root);
            var data = root.GetComponent<ScribeZeldaCharacterData>();
            Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one civilian-derived data component");
            foreach (var c in root.GetComponentsInChildren<Component>(true)) Check(c, "no missing components");
            Check(root.transform.localScale == Vector3.one, "civilian body scale preserved");
            Check(data.CurrentHealth == 3 && data.AttackPower == 1 && data.MoveSpeed == 3, "civilian health/attack/speed: " + data.CurrentHealth + "/" + data.AttackPower + "/" + data.MoveSpeed + " in " + path);
            var so = new SerializedObject(data);
            Check(so.FindProperty("skillValue").intValue == 1 && so.FindProperty("permissionLevel").intValue == 1, "scribe skill/permission");
            Check(so.FindProperty("possessionEnergy").intValue == 2 && so.FindProperty("possessionCost").intValue == 1, "possession values");
            Check(data.CanAttack && data.AttackPrefab && data.AttackVisualShape == ZeldaAttackVisualShape.Rock, "civilian self-defense attack wired");
            Check(data.AttackDuration == .2f && data.AttackSize == new Vector2(.42f, .36f), "civilian attack dimensions");
            var ai = root.GetComponent<ZeldaCharacterAiBase>();
            Check(ai && ai.enabled && ai.GetType() == (patrol ? typeof(CivilianPatrolCharacterAi) : typeof(CivilianIdleCharacterAi)), "correct AI variant");
            Check(!root.GetComponent<ZeldaFourWayMover>().enabled, "not initially player controlled");
            Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !root.GetComponent<BoxCollider2D>().isTrigger, "body collision preserved");
            var config = new SerializedObject(ai);
            Check(config.FindProperty("visionRadius").floatValue == 7 && config.FindProperty("peripheralVisionAngle").floatValue == 240 &&
                config.FindProperty("peripheralVisionRadius").floatValue == 1.7f && config.FindProperty("immediateResponseDistance").floatValue == 5.5f, "latest Level2 vision settings");
            if (patrol) Check(!config.FindProperty("useCustomPatrolRoute").boolValue && config.FindProperty("patrolPoint2Offset").vector2Value == new Vector2(2, 0), "editable patrol defaults");
            Check(PixelCharacterWalkAnimator.Supports(data), "walk rig supports new type");
            var renderer = root.GetComponent<SpriteRenderer>();
            foreach (var facing in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
            {
                data.ApplyCharacterVisualWithMovement(renderer, facing, false, false);
                Sprite idle = renderer.sprite;
                Check(idle && idle.pixelsPerUnit == 16 && idle.texture.isReadable && idle.texture.filterMode == FilterMode.Point, "readable 16 PPU frame");
                data.ApplyCharacterVisualWithMovement(renderer, facing, true, false);
                Check(renderer.sprite && renderer.sprite != idle, "shared walk pose");
                data.ApplyCharacterVisualWithMovement(renderer, facing, false, true);
                Check(renderer.sprite && renderer.sprite != idle, "distinct reaching arm");
                data.ApplyCharacterVisual(renderer, facing, false, false);
                Check(renderer.sprite == idle, "idle sprite cached");
            }
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var before = renderer.sprite.texture.GetPixels();
            so.FindProperty("clothingColor").colorValue = Color.magenta; so.ApplyModifiedPropertiesWithoutUndo();
            typeof(ScribeZeldaCharacterData).GetMethod("OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(data, null);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.down, true, false);
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var after = renderer.sprite.texture.GetPixels(); int changed = 0;
            for (int i = 0; i < before.Length; i++)
            { Check(before[i].a == after[i].a, "palette editing preserves silhouette"); if (before[i] != after[i]) changed++; }
            Check(changed > 10, "robe palette editable and walk cache invalidated safely");
            var state = data.CaptureRuntimeState();
            Check(state.characterType.Contains("ScribeZeldaCharacterData"), "save keeps distinct character type");
            data.ApplyRuntimeState(state); Check(data.CurrentHealth == 3, "saved health restores");
            root.SetActive(false); root.SetActive(true);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.left, true, false);
            Check(renderer.sprite, "visual survives distance-streaming reactivation");
            yield return null;
            root.SetActive(false);
        }
    }
}
