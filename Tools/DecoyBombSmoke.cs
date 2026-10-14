using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in the isolated full-script Unity test project.
[InitializeOnLoad]
public static class DecoyBombSmoke
{
    const string Pending = "DecoyBombSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator test;
    static int frame = -1, checks;
    static readonly List<GameObject> owned = new List<GameObject>();
    static BombPickupItem decoy, normal;
    static GameObject attackPrefab;
    static DecoyBombSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false))
            { SessionState.SetBool(Pending, false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Cleanup(); Debug.Log("DECOY_BOMB_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool b, string text) { checks++; if (!b) throw new Exception("Decoy: " + text); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var f = type.GetField(name, Flags | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception("No field " + name);
    }
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static object Call(object target, string name, params object[] args)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        { var m = type.GetMethod(name, Flags | BindingFlags.DeclaredOnly); if (m != null) return m.Invoke(target, args); }
        throw new Exception("No method " + name);
    }
    static void Cleanup()
    {
        foreach (var go in owned) if (go != null) Object.DestroyImmediate(go);
        owned.Clear();
        foreach (var effect in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(effect.gameObject);
        foreach (var effect in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(effect.gameObject);
    }
    static PlacedBomb Place(Vector2 pos, BombPickupItem template = null)
    {
        var bomb = (template != null ? template : decoy).CreateConfiguredPlacedBomb(pos, null);
        Own(bomb.gameObject); Physics2D.SyncTransforms(); return bomb;
    }
    static ZeldaCharacterAiBase Guard(Vector2 pos, Type aiType = null)
    {
        var go = Own(new GameObject("Decoy guard")); go.SetActive(false); go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .4f;
        var data = go.AddComponent<SwordZeldaCharacterData>();
        Set(data, "attackPrefab", attackPrefab);
        go.AddComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaCharacterAiBase)go.AddComponent(aiType ?? typeof(ZeldaCharacterAiBase));
        Set(ai, "enableAwarenessSound", false);
        go.SetActive(true);
        Call(ai, "SetFacingDirection", Vector2.right);
        Physics2D.SyncTransforms(); return ai;
    }
    static SandwormEruptionAttack Worm()
    {
        var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        go.transform.position = Vector3.zero;
        go.GetComponent<SandwormBossPrototype>().ResetPose();
        UnityEngine.Random.InitState(7491);
        return go.GetComponent<SandwormEruptionAttack>();
    }
    static void Step(SandwormEruptionAttack worm)
    { worm.GetComponent<SandwormBossPrototype>().Advance(.025f); worm.AdvanceAttack(.025f); }
    static void Preview()
    {
        var a = Own(Object.Instantiate(normal.gameObject)); a.transform.position = new Vector3(-.45f, 0);
        var b = Own(Object.Instantiate(decoy.gameObject)); b.transform.position = new Vector3(.45f, 0);
        var cam = Own(new GameObject("Decoy preview")).AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = .45f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.045f,.065f,.13f);
        var rt = new RenderTexture(800,400,24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(800,400,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,800,400),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../DecoyBomb-preview.png"),image.EncodeToPNG());
        cam.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
        Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(cam.gameObject);
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        SceneManager.SetActiveScene(SceneManager.CreateScene("Decoy bomb isolated tests"));
        Time.timeScale = 0f;
        decoy = Resources.Load<GameObject>("PickupItems/Bombs/DecoyBombPickupItem").GetComponent<BombPickupItem>();
        normal = Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem").GetComponent<BombPickupItem>();
        Check(decoy.IsDecoy && !normal.IsDecoy, "decoy flag only on new item");
        Check(decoy.ExplosionAttackPower == normal.ExplosionAttackPower && decoy.MaxStackSize == normal.MaxStackSize &&
            decoy.StreamingRetentionRadius == normal.StreamingRetentionRadius, "ordinary bomb data preserved");
        Check(AssetDatabase.AssetPathToGUID("Assets/Resources/PickupItems/Bombs/BombPickupItem.prefab") ==
            "b87e566d519644d8a90031cdb564fda6", "ordinary bomb GUID preserved");
        int normalCount=0, decoyCount=0;
        foreach(var prefab in Resources.LoadAll<GameObject>("PickupItems"))
        {
            var item = prefab.GetComponent<PickupItemBase>();
            if(item != null && item.ItemId=="bomb") normalCount++;
            if(item != null && item.ItemId=="decoy_bomb") decoyCount++;
        }
        Check(normalCount==1 && decoyCount==1,"recursive catalog finds one of each identity");
        Preview();
        var bait = Place(Vector2.zero); bait.Advance(3600);
        Check(bait.IsActiveDecoy,"no timed explosion after one hour");
        var saved = SavedScalarFields.Capture(bait,true);
        Object.DestroyImmediate(bait.gameObject);
        bait=Place(Vector2.zero); SavedScalarFields.Apply(bait,saved); bait.RestoreSavedOwner(null); bait.Advance(100);
        Check(bait.IsActiveDecoy && bait.SaveSourceItemId=="decoy_bomb","device scalar/save identity round trip retains no fuse");
        Check(bait.TryConsumeExplosive(out int power) && power==normal.ExplosionAttackPower && !bait.IsActiveDecoy,
            "swallow removes beacon without ground blast and returns ordinary power");
        Check(Object.FindObjectsOfType<BombExplosionVisual>().Length==0,"consumption never explodes on ground");
        Cleanup();
        var timed=Place(Vector2.zero,normal); timed.Advance(normal.FuseDuration+.1f);
        Check(!timed.IsArmed && Object.FindObjectsOfType<BombExplosionVisual>().Length>0,"normal bomb still explodes on timer");
        Cleanup(); yield return null;

        attackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaAttackHitbox.prefab");
        Check(attackPrefab != null, "real melee prefab exists in isolated fixture");
        var guard=Guard(Vector2.zero);
        var pickup=Own(Object.Instantiate(decoy.gameObject)); pickup.transform.position=Vector2.right*2;
        Check(!(bool)Call(guard,"UpdateDecoyBombTarget"),"unarmed/Q-dropped pickup does not attract");
        Object.DestroyImmediate(pickup);
        bait=Place(Vector2.right*2);
        var wall=Own(new GameObject("Opaque Blocks wall")); wall.layer=LayerMask.NameToLayer("Blocks");
        wall.transform.position=Vector2.right; wall.AddComponent<BoxCollider2D>().size=new Vector2(.3f,4f); Physics2D.SyncTransforms();
        Check(!(bool)Call(guard,"UpdateDecoyBombTarget"),"unseen bait behind Blocks does not attract");
        Object.DestroyImmediate(wall); Physics2D.SyncTransforms();
        Check((bool)Call(guard,"UpdateDecoyBombTarget") && guard.DecoyBombTarget==bait,"visible armed bait acquired");
        Check(guard.CurrentTarget==null && guard.CurrentState==ZeldaAiState.Idle,"bait does not create player hostility");
        Check(guard.RequiresVisionStreamingRetention,"pursuit retained outside camera");
        Call(guard,"TickHostile",.025f);
        Check(((Vector2)Field(guard,"moveDirection").GetValue(guard)).x>0,"pursuit moves toward bait");
        // Let real Update/FixedUpdate, attack spawning, and trigger contact finish the approach.
        Time.timeScale=1f;
        float until=Time.realtimeSinceStartup+12f;
        while(bait!=null && bait.IsArmed && Time.realtimeSinceStartup<until) yield return null;
        Check(bait==null || !bait.IsArmed,"NPC actually moves, swings and detonates lure; guard="+guard.transform.position+
            " attacking="+guard.GetComponent<ZeldaFourWayMover>().IsAttacking+" target="+guard.DecoyBombTarget);
        Time.timeScale=0f; Cleanup(); yield return null;

        attackPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaAttackHitbox.prefab");
        var dormant=(AutomatonCharacterAi)Guard(Vector2.zero,typeof(AutomatonCharacterAi));
        bait=Place(Vector2.right*2);
        Check(!(bool)Call(dormant,"UpdateDecoyBombTarget"),"dormant automaton stays dormant");
        dormant.ActivateHostility();
        Check((bool)Call(dormant,"UpdateDecoyBombTarget"),"activated automaton also targets bait");
        Cleanup(); yield return null;

        var player=Own(new GameObject("Player away from bait")); player.transform.position=new Vector2(-18,10);
        player.AddComponent<CivilianZeldaCharacterData>();
        var playerMover=player.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(playerMover);
        var worm=Worm(); bait=Place(new Vector2(8,6));
        Check(worm.TryTrackPlayerAttack() && worm.DecoyTarget==bait && worm.LockedAttackPoint==(Vector2)bait.transform.position,
            "sandworm prioritizes bait over controlled player");
        for(int i=0;i<3000 && worm.SwallowedExplosiveCount==0;i++) Step(worm);
        Check(worm.SwallowedExplosiveCount==1 && worm.SwallowedAttackPower==normal.ExplosionAttackPower && !bait.IsActiveDecoy,
            "sandworm reaches bait and consumes it without ground detonation");
        int hp=worm.GetComponent<SandwormZeldaCharacterData>().CurrentHealth;
        for(int i=0;i<3000 && worm.Phase!=SandwormEruptionAttack.AttackPhase.Falling;i++) Step(worm);
        Check(worm.Phase==SandwormEruptionAttack.AttackPhase.Falling &&
            worm.GetComponent<SandwormZeldaCharacterData>().CurrentHealth==hp-normal.ExplosionAttackPower*2,
            "same mid-flight explosion, damage and fall as other bombs");
        Cleanup(); yield return null;

        worm=Worm(); Check(worm.TryLockAttack(new Vector2(8,6)),"locked player-position fixture");
        bait=Place(new Vector2(4,8)); worm.AdvanceAttack(.025f);
        Check(worm.DecoyTarget==bait && worm.LockedAttackPoint==(Vector2)bait.transform.position,"new beacon redirects locked but buried worm");
        Object.DestroyImmediate(bait.gameObject); worm.AdvanceAttack(.025f);
        Check(worm.Phase==SandwormEruptionAttack.AttackPhase.Roaming,"lost bait with no player cancels instead of stuck");
        Cleanup(); yield return null;

        worm=Worm(); bait=Place(new Vector2(8,6)); worm.AdvanceAttack(.025f);
        Check(worm.RoamingProgress<4 && worm.DecoyTarget==bait && worm.Phase==SandwormEruptionAttack.AttackPhase.Approaching,
            "bait starts approach without waiting four roaming arrivals or requiring a player");
        Cleanup(); yield return null;
    }
}
