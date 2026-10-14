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
public static class TurretTravelSmoke
{
    const string A = "TurretTravelA", B = "TurretTravelB";
    const string Folder = "Assets/TurretTravelTest";
    const string WardPath = "Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab";
    const string TurretPath = "Assets/Prefabs/Level2NPCS/Turret-level2.prefab";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int frame = -1, checks; static double deadline;
    static TurretTravelSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "turret-travel-smoke";
        Directory.CreateDirectory(Folder);
        BuildScene(A, true); BuildScene(B, false);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Folder + "/" + A + ".unity", true),
            new EditorBuildSettingsScene(Folder + "/" + B + ".unity", true) };
        EditorSceneManager.OpenScene(Folder + "/" + A + ".unity");
        SessionState.SetBool(nameof(TurretTravelSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void BuildScene(string name, bool turrets)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().orthographic = true; camera.GetComponent<Camera>().orthographicSize = 7;
        var ward = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WardPath));
        ward.transform.position = new Vector3(0, -3);
        ward.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        ward.GetComponent<ZeldaFourWayMover>().enabled = false;
        if (turrets) for (int i = 0; i < 2; i++)
        {
            var turret = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TurretPath));
            turret.name = "Travel Turret " + i; turret.transform.position = Vector3.left * i;
            var serialized = new SerializedObject(turret.GetComponent<TurretCharacterAi>());
            serialized.FindProperty("protectedCharacter").objectReferenceValue = ward.GetComponent<ZeldaCharacterData>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene, Folder + "/" + name + ".unity");
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TurretTravelSmoke), false))
            { SessionState.SetBool(nameof(TurretTravelSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Travel timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("TURRET_TRAVEL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool v, string m) { checks++; if (!v) throw new Exception(m); }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; }
        throw new Exception("Missing " + n);
    }
    static object Call(object o, string n, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var method = t.GetMethod(n, F); if (method != null) return method.Invoke(o, args); }
        throw new Exception("Missing " + n);
    }
    static int Listeners(ZeldaCharacterData ward)
    {
        var handlers = (Action<ZeldaCharacterData>)Field(ward, "AttackedBy").GetValue(ward);
        return handlers == null ? 0 : handlers.GetInvocationList().Count(h => h.Target is TurretCharacterAi);
    }
    static bool Arrived(string scene) => SceneManager.GetActiveScene().name == scene &&
        !SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress &&
        !SceneTravelStateManager.Instance.IsQuickRestartPending && !DocumentReader.IsInputBlocked;
    static void AssertBinding(TurretCharacterAi[] turrets, ZeldaFourWayMover ward, string id)
    {
        Check(turrets.Length == 2, "both scene-authored turrets restored");
        foreach (var ai in turrets)
        {
            Check(ai.ProtectedCharacter == ward.CharacterData && ai.CaptureTurretState().bound == id,
                "saved identity resolves to returning body before first AI tick: " + ai.name);
            for (int i = 0; i < 4; i++) ai.RefreshProtectionBinding();
        }
        Check(Listeners(ward.CharacterData) == 2, "exactly one damage subscription per turret");
    }
    static ZeldaFourWayMover Aggressor()
    {
        var go = new GameObject("Travel protection attacker"); go.transform.position = new Vector3(4, 0);
        go.AddComponent<SwordZeldaCharacterData>(); var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = false;
        return mover;
    }
    static void AssertRetaliation(TurretCharacterAi[] turrets, ZeldaFourWayMover ward)
    {
        var attacker = Aggressor(); var oldState = ward.CharacterData.CaptureRuntimeState(); Physics2D.SyncTransforms();
        ward.CharacterData.TakeDamage(1, attacker.CharacterData);
        foreach (var ai in turrets) Check(ai.CurrentState == ZeldaAiState.Hostile && ai.CurrentTarget == attacker,
            "actual returned body's damage event reaches its turret immediately");
        ward.CharacterData.ApplyRuntimeState(oldState);
        Object.DestroyImmediate(attacker.gameObject);
        foreach (var ai in turrets) Call(ai, "UpdateTargetAndState", .6f);
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var manager = SceneTravelStateManager.GetOrCreate();
        var ward = Object.FindObjectsOfType<ZeldaFourWayMover>().Single(m => m.GetComponent<TurretZeldaCharacterData>() == null);
        ward.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(ward);
        string identity = ward.CharacterData.IndividualAccessId;
        var turrets = Object.FindObjectsOfType<TurretCharacterAi>();
        AssertBinding(turrets, ward, identity); AssertRetaliation(turrets, ward);
        for (int trip = 0; trip < 2; trip++)
        {
            manager.TravelToScene(B, new Vector2(0, -3));
            while (!Arrived(B)) yield return null;
            Check(turrets.All(t => t == null), "origin turrets really unloaded");
            Check(ZeldaRuntimeRegistry.GetControlledMover() == ward && ward.gameObject.scene.name == "DontDestroyOnLoad",
                "production travel keeps controlled body, not a prefab duplicate");
            Check(Listeners(ward.CharacterData) == 0, "unloaded turrets detach damage subscriptions");
            if (trip == 1)
            {
                string json = manager.CaptureDiskWorld(); var playerSave = manager.CaptureDiskPlayer(); var oldWard = ward;
                Check(json.Contains(identity) && playerSave.character.individualAccessId == identity,
                    "foreign-scene save retains bound identity and player identity");
                manager.RestoreDiskWorld(json, B, playerSave, Resources.Load<SaveGameCatalog>("SaveGameCatalog"));
                while (!Arrived(B)) yield return null;
                ward = ZeldaRuntimeRegistry.GetControlledMover();
                Check(ward != null && ward != oldWard && ward.CharacterData.IndividualAccessId == identity,
                    "real disk restoration reconstructs a different object with the same identity");
            }
            manager.TravelToScene(A, new Vector2(0, -3));
            while (!Arrived(A)) yield return null;
            Check(ward == ZeldaRuntimeRegistry.GetControlledMover(), "return preserves body control");
            turrets = Object.FindObjectsOfType<TurretCharacterAi>();
            AssertBinding(turrets, ward, identity);
            var duplicate = Object.FindObjectsOfType<ZeldaFourWayMover>(true).FirstOrDefault(m => m != ward &&
                m.GetComponent<TurretZeldaCharacterData>() == null && m.gameObject.scene.name == A);
            Check(duplicate != null && !duplicate.gameObject.activeInHierarchy && duplicate.CharacterData.IndividualAccessId != identity,
                "fresh authored copy is inactive and has a distinct identity");
            // A stale serialized scene reference must never overwrite the saved identity.
            Field(turrets[0], "protectedCharacter").SetValue(turrets[0], duplicate.CharacterData);
            Check(turrets[0].CaptureTurretState().bound == identity, "stale scene reference cannot corrupt the saved binding");
            turrets[0].RefreshProtectionBinding();
            AssertBinding(turrets, ward, identity); AssertRetaliation(turrets, ward);
        }

        // Additive / persistent-object case: the turret remains loaded while its
        // ward's gameplay scene changes. No remote incidents or duplicate listeners.
        var origin = SceneManager.GetActiveScene(); var foreign = SceneManager.CreateScene("Foreign active level");
        foreach (var ai in turrets) ai.BindCharacter(ward.CharacterData);
        SceneManager.SetActiveScene(foreign);
        Check(Listeners(ward.CharacterData) == 0 && turrets.All(t => t.HasBinding && t.ProtectedCharacter == null),
            "departing body releases only local references, never the binding identity");
        SceneManager.SetActiveScene(origin);
        AssertBinding(turrets, ward, identity);
        foreach (var ai in turrets) ai.enabled = false;
        Check(Listeners(ward.CharacterData) == 0, "disabled turret releases callbacks");
        foreach (var ai in turrets) ai.enabled = true;
        AssertBinding(turrets, ward, identity);
        AssertRetaliation(turrets, ward);
        turrets[0].BindCharacter(null);
        Check(!turrets[0].HasBinding, "explicit unbind clears durable identity");
        SceneManager.SetActiveScene(foreign); SceneManager.SetActiveScene(origin);
        Check(!turrets[0].HasBinding && Listeners(ward.CharacterData) == 1, "scene change cannot revive an explicitly removed binding");
    }
}
