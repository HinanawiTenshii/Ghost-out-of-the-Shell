using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated Unity project, not the designer's open scene.
[InitializeOnLoad]
public static class HeroSmoke
{
    const string Key = nameof(HeroSmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static HeroSmoke() { EditorApplication.update += Tick; }
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); checks++; Debug.Log("HERO_CHECK " + label); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "hero-prefab-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            { SessionState.SetBool(Key, false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hero test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERO_PREFAB_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/Hero.prefab");
        Check(prefab && prefab.GetComponent<HeroZeldaCharacterData>(), "Hero imports in VitalNPCS");
        var go = Object.Instantiate(prefab);
        var data = go.GetComponent<HeroZeldaCharacterData>();
        var renderer = go.GetComponent<SpriteRenderer>();
        var mover = go.GetComponent<ZeldaFourWayMover>();
        var ai = go.GetComponent<ZeldaCharacterAiBase>();
        foreach (var component in go.GetComponentsInChildren<Component>(true)) Check(component, "component present: " + component.GetType().Name);
        Check(go.GetComponents<ZeldaCharacterData>().Length == 1, "single character data component");
        Check(ai && ai.enabled && !mover.enabled, "idle NPC, not initially player-controlled");
        Check(!go.GetComponent<NativeCharacterSkillData>(), "no unrequested native skill");
        Check(data.CurrentHealth == 10 && data.SkillValue == 4 && data.AttackPower == 3 && data.MoveSpeed == 5,
            "advanced-paladin baseline stats");
        var config = new SerializedObject(data);
        Check(config.FindProperty("permissionLevel").intValue == 0 && config.FindProperty("possessionEnergy").intValue == 3 &&
            config.FindProperty("possessionCost").intValue == 1, "editable permission and possession values");
        Check(data.CameraOrthographicSize == 9 && data.PlayerVisionRadius == 13, "current camera defaults");
        Check(go.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !go.GetComponent<BoxCollider2D>().isTrigger,
            "normal character body collision");
        Check(data.CanAttack && data.AttackPrefab && data.AttackVisualShape == ZeldaAttackVisualShape.Sword,
            "shared sword attack wired");
        Check(!PixelCharacterWalkAnimator.Supports(data), "native walk rig is not animated twice");
        Color blue = config.FindProperty("armorColor").colorValue;
        Check(blue.b > blue.g && blue.g > blue.r && blue.g > .6f, "sky-blue main clothing palette");
        var create = typeof(HeroZeldaCharacterData).GetMethod("CreateSprite", F);
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int pose in Enumerable.Range(0, 4))
        {
            var sprite = (Sprite)create.Invoke(data, new object[] { direction, pose });
            Check(sprite.pixelsPerUnit == 16 && sprite.texture.width == 16 && sprite.texture.height == 16 &&
                sprite.texture.filterMode == FilterMode.Point && sprite.texture.isReadable, "16 PPU readable pose " + direction + "/" + pose);
            var pixels = sprite.texture.GetPixels();
            // RGBA32 quantizes authored float colors to 8-bit channels.
            Check(pixels.Count(c => Mathf.Abs(c.r - blue.r) < .005f && Mathf.Abs(c.g - blue.g) < .005f &&
                Mathf.Abs(c.b - blue.b) < .005f && c.a > .9f) >= 12 && pixels.Count(c => c.a > 0) >= 90,
                "visible blue hero silhouette " + direction + "/" + pose);
            Object.Destroy(sprite.texture); Object.Destroy(sprite);
        }
        foreach (var facing in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
        {
            data.ApplyCharacterVisualWithMovement(renderer, facing, false, false);
            Sprite idle = renderer.sprite;
            data.ApplyCharacterVisualWithMovement(renderer, facing, true, false);
            Check(renderer.sprite && renderer.sprite != idle, "native walking pose " + facing);
            data.ApplyCharacterVisualWithMovement(renderer, facing, false, true);
            Check(renderer.sprite && renderer.sprite != idle, "attacking pose " + facing);
            data.ApplyCharacterVisual(renderer, facing, false, false);
            Check(renderer.sprite == idle, "cached pose reused " + facing);
        }
        var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
        var catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
        Check(catalog.FindCharacter(saved.characterType, go.name) == prefab, "save catalog resolves Hero specifically");
        saved.currentHealth = 7; data.ApplyRuntimeState(saved);
        Check(data.CurrentHealth == 7 && data.SkillValue == 4, "JSON runtime save restores hero attributes");
        go.SetActive(false); go.SetActive(true);
        data.ApplyCharacterVisualWithMovement(renderer, Vector2.left, true, false);
        Check(renderer.sprite, "visual survives streaming reactivation");
        mover.ReceiveControl(Vector2.right);
        Check(mover.enabled && !ai.enabled && ZeldaRuntimeRegistry.GetControlledMover() == mover, "possession hands control to player");
        Check(mover.TryPerformAttack(Vector2.right) && mover.IsAttacking, "possessed hero can use sword attack");
        Check(Object.FindObjectsOfType<ZeldaAttackHitbox>().Length > 0, "sword hitbox spawns");
        Object.Destroy(go);
        yield return null;
    }
}
