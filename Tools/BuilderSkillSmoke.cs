using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BuilderSkillSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame = -1; static double deadline;
    static BuilderSkillSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project");
        PlayerSettings.productName = "builder-skill-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(BuilderSkillSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(BuilderSkillSmoke), false))
            { SessionState.SetBool(nameof(BuilderSkillSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("BUILDER_SKILL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(name, F); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + name);
    }
    static void Sense(TurretCharacterAi ai) { Physics2D.SyncTransforms(); Call(ai, "UpdateTargetAndState", .13f); }
    static TurretZeldaCharacterData[] Summons() => Object.FindObjectsOfType<BuilderTurretBond>(true)
        .Where(b => !string.IsNullOrEmpty(b.OwnerId)).Select(b => b.GetComponent<TurretZeldaCharacterData>()).ToArray();
    static ZeldaFourWayMover Actor(string name, Vector2 point)
    {
        var go = new GameObject(name); go.transform.position = point;
        go.AddComponent<SwordZeldaCharacterData>(); var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = false; return mover;
    }
    static void Energy(ZeldaCharacterData owner, int value)
    {
        var state = owner.CaptureRuntimeState(); state.possessionEnergy = value;
        state.spentBasePossessionEnergy = state.spentGrowthPossessionEnergy = state.crystalEnergyThirds = 0;
        owner.ApplyRuntimeState(state); owner.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static object SnapshotFromDisk(string json, string sceneName)
    {
        var type = typeof(SceneTravelStateManager);
        var world = JsonUtility.FromJson(json, type.GetNestedType("DiskWorld", BindingFlags.NonPublic));
        object disk = null; foreach (var s in Get<IList>(world, "scenes")) if (Get<string>(s, "name") == sceneName) disk = s;
        Check(disk != null, "JSON scene exists");
        var state = Activator.CreateInstance(type.GetNestedType("SceneState", BindingFlags.NonPublic), true);
        Set(state, "sceneName", sceneName); var entries = Get<IDictionary>(state, "objects");
        foreach (var e in Get<IList>(disk, "objects")) entries.Add(Get<string>(e, "path"), e);
        return state;
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1; while (DocumentReader.IsInputBlocked) yield return null;
        var scene = SceneManager.CreateScene("Builder Skill Test"); SceneManager.SetActiveScene(scene);
        var growth = PlayerGrowthAttributes.GetOrCreate(); growth.ApplyRuntimeState(default);
        string[] paths = { "Idle/AdvancedEngineer-level2-Idle.prefab", "Patrol/AdvancedEngineer-level2.prefab" };
        foreach (string path in paths)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/" + path);
            Check(p.GetComponent<BuilderCharacterSkillData>() != null && p.GetComponent<ZeldaCharacterData>().NativeCharacterSkillId == "builder", "native skill on " + path);
        }
        var ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Patrol/Engineer-level2.prefab");
        Check(ordinary.GetComponent<BuilderCharacterSkillData>() == null, "ordinary engineer unchanged");
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/" + paths[0]));
        var owner = root.GetComponent<ZeldaCharacterData>(); var mover = root.GetComponent<ZeldaFourWayMover>();
        mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var basic = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "builder");
        var improved = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "builder_2");
        var passive = Array.Find(PlayerGrowthAttributes.Skills, s => s.id == "craftsmanship_1");
        Check(!growth.IsSkillDiscovered(basic), "builder locked before discovery");
        Energy(owner, 3);
        Check(owner.TryUseCharacterSkill(), "native skill works before learning");
        var turret = Summons().Single();
        Check(owner.FinalPossessionEnergy == 0, "base cost exactly three");
        Check(turret.transform.position.x > 1 && Mathf.Abs(turret.transform.position.y) < .01f, "constructed in facing direction without overlap");
        Check(turret.GetComponent<TurretCharacterAi>().ProtectedCharacter == owner && turret.GetComponent<BuilderTurretBond>().OwnerId == owner.IndividualAccessId, "both protection and lifetime bind to caster body");
        mover.EndItemAttack(); Check(!BuilderSkillRuntime.TryUse(owner) && Summons().Length == 1, "insufficient energy creates nothing");
        Object.DestroyImmediate(turret.gameObject);
        growth.RecordPossessedCharacter(owner); growth.AddCollectibles(20);
        Check(growth.IsSkillDiscovered(basic) && !growth.TryUpgradeSkill("builder_2"), "discovery and prerequisites");
        Check(growth.TryUpgradeSkill("builder") && growth.CollectibleCount == 16, "learn builder cost four");
        Check(growth.ToggleEquippedSkill(0, "builder") && SkillPageArt.GetAbilitySprite(growth.GetEquippedSkill(0)) != null, "equip builder icon");
        Check(growth.TryUpgradeSkill("builder_2") && growth.GetEquippedSkill(0) == "builder_2" && BuilderSkillRuntime.EnergyCost == 2, "upgrade replaces equipped skill and lowers energy cost");
        int originalSkill = owner.FinalSkillValue;
        Check(growth.TryUpgradeSkill("craftsmanship_1") && owner.FinalSkillValue == originalSkill + 1, "second upgrade permanently adds one skill");
        Check(passive.type == PlayerGrowthAttributes.SkillType.Passive && !growth.ToggleEquippedSkill(1, passive.id), "passive does not replace or occupy active skill slot");
        var progress = JsonUtility.FromJson<PlayerGrowthAttributes.RuntimeState>(JsonUtility.ToJson(growth.CaptureRuntimeState()));
        growth.ApplyRuntimeState(progress); growth.ApplyRuntimeState(progress);
        Check(owner.FinalSkillValue == originalSkill + 1 && growth.GetEquippedSkill(0) == "builder_2", "growth save roundtrip no double counting");
        Check(basic.position.y == improved.position.y && improved.position.y == passive.position.y && basic.position.y < -165 &&
            basic.position.x < improved.position.x && improved.position.x < passive.position.x, "three connected nodes in requested lower-left row");
        Check(BuilderCharacterSkillData.GetIcon(0) != null && SkillPageArt.GetAbilitySprite("builder_2") != SkillPageArt.GetAbilitySprite("builder"), "native turret and distinct ranked glyphs");

        foreach (Vector2 direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
        {
            mover.ReceiveControl(direction); Energy(owner, 5);
            Check(owner.TryUseCharacterSkill(), "build " + direction);
            turret = Summons().Single(); Vector2 offset = turret.transform.position - owner.transform.position;
            Check(Vector2.Dot(offset.normalized, direction) > .99f && owner.FinalPossessionEnergy == 3, "placement and upgraded cost " + direction);
            Object.DestroyImmediate(turret.gameObject);
        }
        mover.ReceiveControl(Vector2.right); Energy(owner, 5);
        var wall = new GameObject("Construction wall"); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector3(1, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.08f, 3);
        Check(!owner.TryUseCharacterSkill() && owner.FinalPossessionEnergy == 5 && Summons().Length == 0, "blocked construction refunds by never spending");
        Object.DestroyImmediate(wall);
        GhostFormRuntime.Restore(owner, 5, 5);
        Check(!owner.TryUseCharacterSkill(), "ghost form cannot build"); GhostFormRuntime.Restore(owner, 0);
        Time.timeScale = 0; Check(!owner.TryUseCharacterSkill(), "pause cannot build"); Time.timeScale = 1;
        Check(owner.TryUseCharacterSkill(), "construct defender"); turret = Summons().Single();
        var turretAi = turret.GetComponent<TurretCharacterAi>();
        var enemy = Actor("Enemy", new Vector2(3, 2)); var enemyAi = enemy.gameObject.AddComponent<ZeldaCharacterAiBase>();
        var friend = Actor("Friend", new Vector2(2, -2)); var friendAi = friend.gameObject.AddComponent<ZeldaCharacterAiBase>();
        Sense(turretAi); Check(turretAi.CurrentState == ZeldaAiState.Idle, "neutral characters are ignored");
        Call(enemyAi, "BeginTemporaryHostility", mover, false); Sense(turretAi);
        Check(turretAi.CurrentTarget == enemy, "player-bound turret acquires player-hostile character proactively");
        enemy.transform.position = new Vector3(30, 0); Sense(turretAi);
        Check(turretAi.CurrentTarget == null, "automatic defense ends outside turret vision");
        enemy.transform.position = new Vector3(3, 2); Call(enemyAi, "BeginTemporaryHostility", friend, false); Sense(turretAi);
        Check(turretAi.CurrentTarget == null, "third-party hostility is not hostility to owner");
        Call(enemyAi, "BeginTemporaryHostility", mover, false);
        var obstruction = new GameObject("Defender occlusion"); obstruction.layer = LayerMask.NameToLayer("Blocks");
        obstruction.transform.position = Vector3.Lerp(turret.transform.position, enemy.transform.position, .5f);
        obstruction.AddComponent<BoxCollider2D>().size = Vector2.one; Sense(turretAi);
        Check(turretAi.CurrentTarget == null, "automatic defense respects occlusion"); Object.DestroyImmediate(obstruction);
        Sense(turretAi); Check(turretAi.CurrentTarget == enemy, "visible hostile target reacquired");
        enemy.transform.position = new Vector3(30, 0);
        Call(turretAi, "ProtectedAttacked", enemy.CharacterData); Sense(turretAi);
        string aggressorId = enemy.CharacterData.IndividualAccessId;
        Check(turretAi.CurrentTarget == null && turretAi.CaptureTurretState().target == aggressorId,
            "summoned turret remembers a distant aggressor without engaging");
        Call(friendAi, "BeginTemporaryHostility", mover, false); Sense(turretAi);
        Check(turretAi.CurrentTarget == friend && turretAi.CaptureTurretState().target == aggressorId,
            "unseen remembered attacker does not prevent automatic interception of another visible enemy");
        friend.transform.position = new Vector3(30, -2); Sense(turretAi);
        Check(turretAi.CurrentTarget == null && turretAi.CaptureTurretState().target == aggressorId,
            "automatic target leaving view neither fires nor erases aggressor memory");
        enemy.transform.position = new Vector3(3, 2); Call(enemyAi, "BeginTemporaryHostility", friend, false); Sense(turretAi);
        Check(turretAi.CurrentTarget == enemy, "remembered aggressor reengages even after no longer chasing the owner");
        Object.DestroyImmediate(enemy.gameObject); Object.DestroyImmediate(friend.gameObject);
        Sense(turretAi);

        // Production JSON and scene-return code reconstruct missing runtime roots.
        var manager = SceneTravelStateManager.GetOrCreate(); string json = manager.CaptureDiskWorld();
        Check(json.Contains("builderOwnerId") && json.Contains(owner.IndividualAccessId), "ownership encoded into scene snapshot");
        var snapshot = SnapshotFromDisk(json, scene.name); string id = turret.IndividualAccessId;
        Object.DestroyImmediate(turret.gameObject);
        Check(!owner.IsDead, "unloading a summon does not damage its owner");
        Check((bool)Call(manager, "RestoreSceneSnapshot", scene, snapshot, false), "real scene snapshot restore succeeds");
        turret = Summons().Single(); Sense(turret.GetComponent<TurretCharacterAi>());
        Check(turret.IndividualAccessId == id && turret.GetComponent<TurretCharacterAi>().ProtectedCharacter == owner &&
            turret.GetComponent<BuilderTurretBond>().OwnerId == owner.IndividualAccessId, "JSON restoration retains body identity, protection and death link");
        Call(manager, "RestoreSceneSnapshot", scene, snapshot, false);
        Check(Summons().Length == 1, "repeated restore does not duplicate a turret");
        // Capture an unloaded scene, then kill its caster in a different scene.
        manager.CaptureDiskWorld(); Object.DestroyImmediate(turret.gameObject);
        // Disk loading shares the current scene with the restart checkpoint.
        var scenes = Get<IDictionary>(manager, "sceneStates");
        Set(manager, "quickRestartSceneState", scenes[scene.name]);
        int checkpointCount = Get<IDictionary>(scenes[scene.name], "objects").Count;
        var otherScene = SceneManager.CreateScene("Builder Owner Death Elsewhere");
        SceneManager.MoveGameObjectToScene(root, otherScene); SceneManager.SetActiveScene(otherScene);
        owner.KillFromPotion();
        Check(Get<IDictionary>(Get<object>(manager, "quickRestartSceneState"), "objects").Count == checkpointCount &&
            Get<IDictionary>(scenes[scene.name], "objects").Count < checkpointCount,
            "owner death changes live snapshots without corrupting saved restart checkpoint");
        SceneManager.SetActiveScene(scene); Call(manager, "RestoreScene", scene);
        Check(Summons().Length == 0, "owner death also removes summons stored in unloaded scene snapshots");
        yield return null;

        // Learned skill works on another body; several constructs share its lifetime,
        // while an ordinary manually bound turret remains independent.
        var second = Actor("Learned builder", new Vector2(20, 20));
        second.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(second); Energy(second.CharacterData, 8);
        Check(second.CharacterData.NativeCharacterSkillId != "builder" && BuilderSkillRuntime.TryUse(second.CharacterData), "learned builder works on other bodies");
        var first = Summons().Single(); second.EndItemAttack(); second.ReceiveControl(Vector2.left);
        Check(BuilderSkillRuntime.TryUse(second.CharacterData) && Summons().Length == 2, "multiple turrets supported by available energy");
        var manual = Object.Instantiate(BuilderSkillRuntime.TurretPrefab, new Vector3(20, 25), Quaternion.identity).GetComponent<TurretZeldaCharacterData>();
        manual.GetComponent<TurretCharacterAi>().BindCharacter(second.CharacterData);
        var linked = Summons(); linked[1].gameObject.SetActive(false);
        second.enabled = false; first.GetComponent<ZeldaFourWayMover>().ReceiveControl(Vector2.up);
        ZeldaRuntimeRegistry.ClaimControlledMover(first.GetComponent<ZeldaFourWayMover>());
        second.CharacterData.KillFromPotion();
        Check(linked.All(t => t.IsDead) && !manual.IsDead, "owner death destroys controlled/inactive summoned turrets but not ordinary bound turrets");
        Check(ZeldaRuntimeRegistry.GetControlledMover().CharacterData is GhostZeldaCharacterData,
            "possessing a self-destructed summon returns safely to ghost");
        Preview(growth);
        yield return null;
    }
    static void Preview(PlayerGrowthAttributes growth)
    {
        var root = new GameObject("Builder Skill Preview", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        root.transform.position = new Vector3(1000, 1000, 0);
        var page = root.AddComponent<CharacterSkillPage>();
        Font font = AssetDatabase.FindAssets("ZLabsPixel_12px_M_CN t:Font")
            .Select(g => AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        page.Build(font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Set(page, "selectedId", "builder"); Call(page, "Refresh");
        var tree = Get<SkillTreeScroll>(page, "tree"); tree.Zoom(-.4f);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var builderArt = root.GetComponentsInChildren<SkillPageArt>().Where(a => a.builder).ToArray();
        Check(builderArt.Length == 2 && builderArt.All(a => a.mainTexture != Texture2D.whiteTexture), "both skill-tree nodes use turret textures");
        Check(root.GetComponentsInChildren<SkillPageArt>().Any(a => a.skillBonus), "permanent skill upgrade uses wrench artwork");
        var cam = new GameObject("Builder Preview Camera").AddComponent<Camera>();
        cam.transform.position = root.transform.position + Vector3.back * 10; cam.orthographic = true; cam.orthographicSize = 285;
        cam.cullingMask = 1 << 30; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        var rt = new RenderTexture(1600, 850, 24); cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
        var texture = new Texture2D(1600, 850, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 850), 0, 0); texture.Apply();
        File.WriteAllBytes(Application.dataPath + "/../Builder-skill-preview.png", texture.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(root); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }
}
