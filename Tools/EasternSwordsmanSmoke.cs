using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Copy into the isolated validation project's Assets/Editor to run.
[InitializeOnLoad]
public static class EasternSwordsmanSmoke
{
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static readonly List<GameObject> owned = new List<GameObject>();
    static EasternSwordsmanSmoke() { EditorApplication.update += Tick; }
    public static void Run() { UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); SessionState.SetBool("EasternSwordsmanSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("EasternSwordsmanSmoke", false))
            { SessionState.SetBool("EasternSwordsmanSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("EASTERN_SWORDSMAN_PASS checks=" + checks); EditorApplication.Exit(0);
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
            string path = "Assets/Prefabs/Level2NPCS/" + (patrol ? "Patrol/EasternSwordsman-Patrol.prefab" : "Idle/EasternSwordsman.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Check(prefab, "prefab imports: " + path);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("EasternSwordsman", "AdvancedPaladin"));
            Check(original, "advanced paladin imports");
            // Apart from appearance and the data type, every serialized gameplay value must match.
            foreach (var component in original.GetComponents<Component>())
            {
                if (component is Transform || component is SpriteRenderer) continue;
                var counterpart = prefab.GetComponent(component.GetType());
                Check(counterpart, "same component contract: " + component.GetType());
                CompareSettings(component, counterpart);
            }
            var root = Object.Instantiate(prefab); owned.Add(root);
            var data = root.GetComponent<EasternSwordsmanZeldaCharacterData>();
            Check(data && root.GetComponents<ZeldaCharacterData>().Length == 1, "one paladin-derived data component");
            foreach (var c in root.GetComponentsInChildren<Component>(true)) Check(c, "no missing components");
            Check(root.transform.localScale == original.transform.localScale, "advanced paladin scale preserved");
            Check(data.CurrentHealth == 10 && data.AttackPower == 3 && data.MoveSpeed == 5, "advanced paladin health/attack/speed: " + data.CurrentHealth + "/" + data.AttackPower + "/" + data.MoveSpeed + " in " + path);
            var so = new SerializedObject(data);
            Check(so.FindProperty("skillValue").intValue == 4 && so.FindProperty("permissionLevel").intValue == 0, "swordsman skill/permission");
            Check(so.FindProperty("possessionEnergy").intValue == 3 && so.FindProperty("possessionCost").intValue == 1, "possession values");
            Check(data.CanAttack && data.AttackPrefab && data.AttackVisualShape == ZeldaAttackVisualShape.Sword, "sword attack wired");
            Check(data.AttackDuration == .25f && data.AttackSize == new Vector2(.55f, .9f), "advanced paladin attack dimensions");
            var ai = root.GetComponent<ZeldaCharacterAiBase>();
            Check(ai && ai.enabled && ai.GetType() == original.GetComponent<ZeldaCharacterAiBase>().GetType(), "correct AI variant");
            Check(!root.GetComponent<ZeldaFourWayMover>().enabled, "not initially player controlled");
            Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !root.GetComponent<BoxCollider2D>().isTrigger, "body collision preserved");
            var config = new SerializedObject(ai);
            Check(config.FindProperty("visionRadius").floatValue == 7 && config.FindProperty("peripheralVisionAngle").floatValue == 240 &&
                config.FindProperty("peripheralVisionRadius").floatValue == 1.7f && config.FindProperty("immediateResponseDistance").floatValue == 5.5f, "latest Level2 vision settings");
            if (patrol) Check(!config.FindProperty("useCustomPatrolRoute").boolValue && config.FindProperty("patrolPoint2Offset").vector2Value == new Vector2(2, 0), "editable patrol defaults");
            Check(!PixelCharacterWalkAnimator.Supports(data), "native paladin walk cycle is not applied twice");
            var renderer = root.GetComponent<SpriteRenderer>();
            foreach (var facing in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
            {
                data.ApplyCharacterVisualWithMovement(renderer, facing, false, false);
                Sprite idle = renderer.sprite;
                Check(idle && idle.pixelsPerUnit == 16 && idle.texture.isReadable && idle.texture.filterMode == FilterMode.Point, "readable 16 PPU frame");
                data.ApplyCharacterVisualWithMovement(renderer, facing, true, false);
                Check(renderer.sprite && renderer.sprite != idle, "native walk pose");
                data.ApplyCharacterVisualWithMovement(renderer, facing, false, true);
                Check(renderer.sprite && renderer.sprite != idle, "distinct reaching arm");
                data.ApplyCharacterVisual(renderer, facing, false, false);
                Check(renderer.sprite == idle, "idle sprite cached");
            }
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var before = renderer.sprite.texture.GetPixels();
            so.FindProperty("armorColor").colorValue = Color.magenta; so.ApplyModifiedPropertiesWithoutUndo();
            typeof(EasternSwordsmanZeldaCharacterData).GetMethod("OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(data, null);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.down, true, false);
            data.ApplyCharacterVisual(renderer, Vector2.down, false, false);
            var after = renderer.sprite.texture.GetPixels(); int changed = 0;
            for (int i = 0; i < before.Length; i++)
            { Check(before[i].a == after[i].a, "palette editing preserves silhouette"); if (before[i] != after[i]) changed++; }
            Check(changed >= 4, "robe palette editable and walk cache invalidated safely: " + changed);
            var state = data.CaptureRuntimeState();
            Check(state.characterType.Contains("EasternSwordsmanZeldaCharacterData"), "save keeps distinct character type");
            var catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
            Check(catalog && catalog.FindCharacter(state.characterType, root.name) == prefab, "save catalog resolves exact swordsman variant");
            data.ApplyRuntimeState(state); Check(data.CurrentHealth == 10 && data.SkillValue == 4, "saved health and skill restore");
            root.SetActive(false); root.SetActive(true);
            data.ApplyCharacterVisualWithMovement(renderer, Vector2.left, true, false);
            Check(renderer.sprite, "visual survives distance-streaming reactivation");
            yield return null;
            root.SetActive(false);
        }
    }

    static void CompareSettings(Component original, Component advanced)
    {
        var a = new SerializedObject(original);
        var b = new SerializedObject(advanced);
        var p = a.GetIterator();
        while (p.NextVisible(true))
        {
            if (p.propertyPath.StartsWith("m_")) continue;
            string rootField = p.propertyPath.Split('.')[0];
            if (rootField == "armorColor" || rootField == "armorShadow" || rootField == "armorHighlight" ||
                rootField == "capeColor" || rootField == "capeShadow" || rootField == "visorColor") continue;
            var q = b.FindProperty(p.propertyPath);
            Check(q != null && q.propertyType == p.propertyType, "same field: " + p.propertyPath);
            object left = Value(p), right = Value(q);
            Check(object.Equals(left, right), "unchanged setting: " + original.GetType().Name + "." + p.propertyPath);
        }
    }
    static object Value(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.longValue;
            case SerializedPropertyType.ArraySize: return p.intValue;
            case SerializedPropertyType.Boolean: return p.boolValue;
            case SerializedPropertyType.Float: return p.doubleValue;
            case SerializedPropertyType.String: return p.stringValue;
            case SerializedPropertyType.Enum: return p.enumValueIndex;
            case SerializedPropertyType.Color: return p.colorValue;
            case SerializedPropertyType.Vector2: return p.vector2Value;
            case SerializedPropertyType.Vector3: return p.vector3Value;
            case SerializedPropertyType.Vector4: return p.vector4Value;
            case SerializedPropertyType.Quaternion: return p.quaternionValue;
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue;
            case SerializedPropertyType.LayerMask: return p.intValue;
            case SerializedPropertyType.Generic: return null; // Its children are checked by the iterator.
            default: throw new Exception("Add explicit comparison for " + p.propertyPath + " (" + p.propertyType + ")");
        }
    }
}
