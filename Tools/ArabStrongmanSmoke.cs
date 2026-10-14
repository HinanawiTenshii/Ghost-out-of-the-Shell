using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Copy into the isolated validation project's Assets/Editor to run.
[InitializeOnLoad]
public static class ArabStrongmanSmoke
{
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ArabStrongmanSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("ArabStrongmanSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("ArabStrongmanSmoke", false))
            { SessionState.SetBool("ArabStrongmanSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("ARAB_STRONGMAN_PASS checks=" + checks); EditorApplication.Exit(0);
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
            string path = "Assets/Prefabs/Level2NPCS/" + (patrol ? "Patrol/ArabStrongman-level2-Patrol.prefab" : "Idle/ArabStrongman-level2.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab, "prefab imports: " + path);
            var root = Object.Instantiate(prefab); owned.Add(root);
            var data = root.GetComponent<ArabStrongmanZeldaCharacterData>();
            Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one Strongman-derived data component");
            foreach (var c in root.GetComponentsInChildren<Component>(true)) Check(c, "no missing components");
            Check(root.transform.localScale == Vector3.one * 1.1f, "Strongman body scale preserved");
            Check(data.CurrentHealth == 7 && data.AttackPower == 4 && data.MoveSpeed == 4, "Strongman health/attack/speed: " + data.CurrentHealth + "/" + data.AttackPower + "/" + data.MoveSpeed + " in " + path);
            var so = new SerializedObject(data);
            Check(so.FindProperty("skillValue").intValue == 2 && so.FindProperty("permissionLevel").intValue == 2, "soldier skill/permission");
            Check(so.FindProperty("possessionEnergy").intValue == 3 && so.FindProperty("possessionCost").intValue == 2, "possession values");
            Check(data.CanAttack && data.AttackPrefab && data.AttackVisualShape == ZeldaAttackVisualShape.Hammer, "shared heavy hammer attack wired");
            Check(data.AttackDuration == .3f && data.AttackSize == new Vector2(.85f, .75f), "Strongman attack dimensions");
            var ai = root.GetComponent<ZeldaCharacterAiBase>();
            Check(ai && ai.enabled && ai.GetType() == (patrol ? typeof(ZeldaPatrolCharacterAi) : typeof(ZeldaCharacterAiBase)), "correct AI variant");
            Check(!root.GetComponent<ZeldaFourWayMover>().enabled, "not initially player controlled");
            Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !root.GetComponent<BoxCollider2D>().isTrigger, "body collision preserved");
            var config = new SerializedObject(ai);
            Check(config.FindProperty("visionRadius").floatValue == 7 && config.FindProperty("peripheralVisionAngle").floatValue == 240 &&
                config.FindProperty("peripheralVisionRadius").floatValue == 1.7f && config.FindProperty("immediateResponseDistance").floatValue == 5.5f, "latest Level2 vision settings");
            if (patrol) Check(!config.FindProperty("useCustomPatrolRoute").boolValue && config.FindProperty("patrolPoint2Offset").vector2Value == new Vector2(2, 0), "editable patrol defaults");
            Check(PixelCharacterWalkAnimator.Supports(data), "walk rig supports new type");
            var renderer = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
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
            so.FindProperty("armorColor").colorValue = Color.magenta; so.ApplyModifiedPropertiesWithoutUndo();
            typeof(ArabStrongmanZeldaCharacterData).GetMethod("OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(data, null);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.down, true, false);
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var after = renderer.sprite.texture.GetPixels(); int changed = 0;
            for (int i = 0; i < before.Length; i++)
            { Check(before[i].a == after[i].a, "palette editing preserves silhouette"); if (before[i] != after[i]) changed++; }
            Check(changed > 10, "armor palette editable and walk cache invalidated safely");
            var state = data.CaptureRuntimeState();
            Check(state.characterType.Contains("ArabStrongmanZeldaCharacterData"), "save keeps distinct character type");
            data.ApplyRuntimeState(state); Check(data.CurrentHealth == 7, "saved health restores");
            root.SetActive(false); root.SetActive(true);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.left, true, false);
            Check(renderer.sprite, "visual survives distance-streaming reactivation");
            yield return null;
            root.SetActive(false);
        }
    }
}
