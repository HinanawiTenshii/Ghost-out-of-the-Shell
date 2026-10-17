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
public static class TrapdoorTravelSmoke
{
    const string Key = nameof(TrapdoorTravelSmoke), A = "TrapdoorTravelA", B = "TrapdoorTravelB";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks; static double deadline;
    static TrapdoorTravelSmoke() { EditorApplication.update += Tick; }
    static void Set(object o, string name, object value) => o.GetType().GetField(name, F).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Debug.Log("TRAPDOOR_CHECK " + label); }
    static TrapdoorDestination Endpoint(string name, string scene, Vector2 position)
    {
        string path = "Assets/" + name + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<TrapdoorDestination>(path);
        if (result == null) { result = ScriptableObject.CreateInstance<TrapdoorDestination>(); AssetDatabase.CreateAsset(result, path); }
        Set(result, "sceneName", scene); Set(result, "fallbackPosition", position); EditorUtility.SetDirty(result); return result;
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated only");
        PlayerSettings.productName = "trapdoor-travel-smoke";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Trapdoor.prefab");
        Check(prefab.GetComponent<TrapdoorInteraction>() != null && !prefab.GetComponent<BoxCollider2D>().enabled, "prefab keeps nonblocking ground appearance");
        var toA = Endpoint("TrapdoorEndpointA", A, new Vector2(24, 0));
        var toB = Endpoint("TrapdoorEndpointB", B, new Vector2(8, 3));
        Endpoint("TrapdoorMissingEndpoint", B, new Vector2(8, 3));
        for (int i = 0; i < 2; i++)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollowActiveZeldaMover));
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10); camera.GetComponent<Camera>().orthographic = true;
            var cross = (GameObject)PrefabUtility.InstantiatePrefab(prefab); cross.name = "Cross";
            cross.transform.position = i == 0 ? new Vector2(24, 0) : new Vector2(8, 3);
            var door = cross.GetComponent<TrapdoorInteraction>(); Set(door, "travelMode", TrapdoorInteraction.TravelMode.CrossScene);
            Set(door, "ownEndpoint", i == 0 ? toA : toB); Set(door, "crossSceneTarget", i == 0 ? toB : toA);
            if (i == 0)
            {
                var first = (GameObject)PrefabUtility.InstantiatePrefab(prefab); first.name = "First"; first.transform.position = Vector2.zero;
                var second = (GameObject)PrefabUtility.InstantiatePrefab(prefab); second.name = "Second"; second.transform.position = new Vector2(12, 0);
                Set(first.GetComponent<TrapdoorInteraction>(), "localTarget", second.GetComponent<TrapdoorInteraction>());
                Set(first.GetComponent<TrapdoorInteraction>(), "isLocked", true); Set(first.GetComponent<TrapdoorInteraction>(), "password", "0451");
                Set(second.GetComponent<TrapdoorInteraction>(), "localTarget", first.GetComponent<TrapdoorInteraction>());
                var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/AdvancedPriest-level2-Idle.prefab"));
                player.name = "Test Player"; player.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            }
            EditorSceneManager.SaveScene(scene, "Assets/" + (i == 0 ? A : B) + ".unity");
        }
        AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/"+A+".unity", true), new EditorBuildSettingsScene("Assets/"+B+".unity", true) };
        EditorSceneManager.OpenScene("Assets/"+A+".unity");
        SessionState.SetInt(Key+"checks", checks); SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false)) { SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key+"checks", 0); steps.Push(Verify()); deadline = EditorApplication.timeSinceStartup+200; }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("trapdoor test timeout");
            while (steps.Count > 0) { var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; } if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return; }
            Debug.Log("TRAPDOOR_TRAVEL_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static ZeldaFourWayMover P => ZeldaRuntimeRegistry.GetControlledMover();
    static TrapdoorInteraction Door(string name) => Object.FindObjectsOfType<TrapdoorInteraction>(true).Single(d => d.name == name);
    static void Move(Vector2 position) { P.transform.position = position; P.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms(); }
    static IEnumerator Ready()
    {
        for (int stable = 0; stable < 3;) { stable = !DocumentReader.IsInputBlocked && !GameSaveSystem.IsPreparingSceneSave && !GameSaveSystem.IsLoading ? stable+1 : 0; yield return null; }
    }
    static void Submit(DoorPasswordPanel panel, string digits)
    {
        var entered = (int[])panel.GetType().GetField("enteredDigits", F).GetValue(panel);
        for (int i=0; i<4; i++) entered[i] = digits[i]-'0';
        Call(panel, "SubmitAndClose");
    }
    static IEnumerator Verify()
    {
        yield return Ready();
        var actor = GameObject.Find("Test Player").GetComponent<ZeldaFourWayMover>();
        actor.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(actor); actor.GetComponent<ZeldaCharacterAiBase>().enabled=false;
        var data = actor.CharacterData.CaptureRuntimeState(); data.health = 20; data.currentHealth = 13; data.skillValue = 1; actor.CharacterData.ApplyRuntimeState(data);
        var bagObject = new GameObject("Inventory"); bagObject.SetActive(false); var bag=bagObject.AddComponent<PersistentInventory>(); Set(bag,"saveToPlayerPrefs",false); bagObject.SetActive(true);bag.enabled=false;bag.ClearAll();bag.SetSlot(0,"gold_coin",7);
        Move(Vector2.zero); yield return null;
        var first = Door("First");
        Check(!first.TryInteract() && !DoorPasswordPanel.IsOpen && first.IsLocked,"locked passage inherits door skill gate");
        data.skillValue=10;P.CharacterData.ApplyRuntimeState(data);
        bool cursor = Cursor.visible; var cursorLock=Cursor.lockState;
        Check(first.TryInteract() && DoorPasswordPanel.IsOpen && Time.timeScale==0 && Cursor.visible,"existing password panel pauses and shows cursor");
        var panel=first.GetComponent<DoorPasswordPanel>();panel.CloseWithoutSubmission();
        Check(first.IsLocked&&!RetroSceneLoadReveal.IsBlockingInput&&Time.timeScale==1&&Cursor.visible==cursor&&Cursor.lockState==cursorLock,"cancel preserves lock and restores input");
        Check(first.TryInteract(),"password reopen");Submit(panel,"9999");
        Check(first.IsLocked&&!RetroSceneLoadReveal.IsBlockingInput&&P.transform.position==Vector3.zero,"wrong password cannot travel");
        Check(GameSaveSystem.Instance.Save(1,false),"save locked passage");
        Check(first.TryInteract(),"correct password attempt");Submit(panel,"0451");
        Check(!first.IsLocked&&RetroSceneLoadReveal.IsBlockingInput,"unlock starts same-scene black cover");
        Check(!Door("Second").TryInteract(),"black cover blocks repeated travel");
        yield return Ready();
        Check(SceneManager.GetActiveScene().name==A&&Vector2.Distance(P.transform.position,new Vector2(12,0))<.02f,"same scene arrives at linked hatch");
        Check(P==actor&&P.CharacterData.CurrentHealth==13&&bag.GetQuantity("gold_coin")==7,"local travel preserves body HP and inventory");
        Check(!Door("First").IsLocked,"source remains unlocked");
        Check(Vector2.Distance(Camera.main.transform.position,P.transform.position)<.05f,"camera snaps under cover");
        var second=Door("Second");
        Check(second.TryInteract(),"linked reverse passage");yield return Ready();
        Check(Vector2.Distance(P.transform.position,Vector2.zero)<.02f,"reverse arrival at first hatch");
        second.gameObject.SetActive(false);
        Check(Door("First").TryInteract(),"inactive streamed target still resolves");yield return Ready();
        Check(Vector2.Distance(P.transform.position,new Vector2(12,0))<.02f,"inactive target arrival correct");second.gameObject.SetActive(true);
        Check(GameSaveSystem.Instance.Save(2,false),"save unlocked passage");
        Check(GameSaveSystem.Instance.Load(1),"load locked checkpoint");yield return Ready();
        Check(Door("First").IsLocked,"locked state restored");
        Check(GameSaveSystem.Instance.Load(2),"load unlocked checkpoint");yield return Ready();
        Check(!Door("First").IsLocked,"unlocked state restored");
        Move(Door("Cross").transform.position); string bodyId=P.CharacterData.IndividualAccessId;
        Check(Door("Cross").TryInteract(),"start cross-scene passage");yield return Ready();
        Check(SceneManager.GetActiveScene().name==B&&Vector2.Distance(P.transform.position,new Vector2(8,3))<.02f,"cross scene resolves matching endpoint");
        Check(P.CharacterData.IndividualAccessId==bodyId&&P.CharacterData.CurrentHealth==13&&PersistentInventory.Instance.GetQuantity("gold_coin")==7,"cross scene keeps identity HP inventory");
        // Prove arrival uses the restored endpoint transform, not the asset's cached position.
        Door("Cross").transform.position=new Vector2(15,5);Move(new Vector2(15,5));
        Check(Door("Cross").TryInteract(),"cross scene return");yield return Ready();
        Check(SceneManager.GetActiveScene().name==A&&Vector2.Distance(P.transform.position,new Vector2(24,0))<.02f&&!Door("First").IsLocked,"return retains unlocked hatch and target position");
        Check(Door("Cross").TryInteract(),"revisit moved target");yield return Ready();
        Check(Vector2.Distance(P.transform.position,new Vector2(15,5))<.02f,"destination resolved after saved transforms restore");
        Check(GameSaveSystem.Instance.Save(3,false)&&GameSaveSystem.Instance.Load(3),"disk save/load after cross scene travel");yield return Ready();
        Check(Door("Cross").TryInteract(),"return after disk restore");yield return Ready();
        Check(SceneManager.GetActiveScene().name==A&&!Door("First").IsLocked,"binding and lock survive disk restore");
        Move(Door("First").transform.position);
        var original = Door("Second");Set(Door("First"),"localTarget",null);
        Check(!Door("First").TryInteract()&&!RetroSceneLoadReveal.IsBlockingInput,"missing local binding fails without black screen");
        Set(Door("First"),"localTarget",Door("First"));
        Check(!Door("First").TryInteract(),"self binding rejected");Set(Door("First"),"localTarget",original);
        var wall=new GameObject("Test wall",typeof(BoxCollider2D));wall.layer=LayerMask.NameToLayer("Blocks");wall.transform.position=new Vector2(.5f,0);wall.transform.localScale=new Vector3(.1f,2,1);Move(new Vector2(1,0));
        Check(!Door("First").TryInteract(),"wall blocks hatch interaction");Object.Destroy(wall);yield return null;
        Move(Door("First").transform.position);
        var arena=new GameObject("Test Arena").AddComponent<ArenaChallengeController>();arena.region=new Rect(-100,-100,200,200);
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState {phase=ArenaChallengeController.Phase.Preparing,entrantId=P.CharacterData.IndividualAccessId});
        Check(!Door("First").TryInteract(),"active arena prevents hatch escape");arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState());Object.Destroy(arena.gameObject);
        // Missing remote hatch: cached safe exit is used, overlay still releases.
        Move(Door("Cross").transform.position);Set(Door("Cross"),"crossSceneTarget",AssetDatabase.LoadAssetAtPath<TrapdoorDestination>("Assets/TrapdoorMissingEndpoint.asset"));
        Check(Door("Cross").TryInteract(),"missing remote endpoint uses configured fallback");yield return Ready();
        Check(SceneManager.GetActiveScene().name==B&&Vector2.Distance(P.transform.position,new Vector2(8,3))<.02f,"fallback never leaves loading cover stuck");
    }
}
