using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

[InitializeOnLoad]
public static class SandwormTremorSmoke
{
    static readonly List<Object> owned = new List<Object>();
    static IEnumerator pending;
    static int frame=-1, checks;
    static SandwormTremorSmoke() { EditorApplication.update+=Tick; }
    static T Own<T>(T obj) where T:Object { owned.Add(obj); return obj; }
    static void Check(bool value,string label) { checks++; if (!value) throw new Exception("Sandworm tremor: "+label); }
    public static void Run() { SessionState.SetBool("SandwormTremor.Pending",true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if (SessionState.GetBool("SandwormTremor.Pending",false))
            { SessionState.SetBool("SandwormTremor.Pending",false); pending=Verify(); }
            if (pending==null || pending.MoveNext()) return;
            pending=null; Cleanup(); Time.timeScale=1f; Time.captureFramerate=0;
            Debug.Log("SANDWORM_TREMOR_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception error) { pending=null; Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--) if(owned[i]!=null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        foreach(var site in Object.FindObjectsOfType<SandwormEruptionSite>()) site.Release();
        foreach(var blast in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(blast.gameObject);
    }
    static SandwormCameraTremor Worm()
    {
        var root=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        root.transform.position=Vector3.zero; root.GetComponent<SandwormBossPrototype>().ResetPose();
        var source=root.GetComponent<SandwormCameraTremor>();
        Check(source!=null,"prefab has configured tremor component");
        source.AdvanceTremor(.01f); return source;
    }
    static Camera CameraAt(Vector2 position,float size=40f)
    {
        var root=Own(new GameObject("Tremor gameplay camera",typeof(Camera)));
        root.transform.position=new Vector3(position.x,position.y,-30f);
        var camera=root.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=size;
        camera.aspect=16f/9f; camera.enabled=false; return camera;
    }
    static Vector2 Offset(Camera camera,CameraCircularVision vision=null,float dt=.02f) =>
        CameraEnvironmentalShakeSource.SampleOffset(camera,vision,dt);
    static void Nudge(SandwormCameraTremor source)
    {
        foreach(var part in source.GetComponentsInChildren<SandwormSegmentVisual>()) part.transform.position+=Vector3.right*.01f;
        source.AdvanceTremor(.05f);
    }
    static void VerifyVisibility()
    {
        var source=Worm(); var move=source.GetComponent<SandwormBossPrototype>();
        var camera=CameraAt((Vector2)move.Tail.position+Vector2.down*3.5f);
        var vision=camera.gameObject.AddComponent<CameraCircularVision>();
        vision.SetVisionMaskRadius(2f); vision.RefreshSceneCameraSettings();
        Nudge(source);
        var tail=move.Tail.GetComponent<SpriteRenderer>(); var head=move.Head.GetComponent<SpriteRenderer>();
        Check(!vision.IsWorldBoundsVisible(head.bounds,head.transform),"head is outside actual visibility");
        Check(vision.IsWorldBoundsVisible(tail.bounds,tail.transform),"only a portion of the tail reaches the vision circle");
        Check(Offset(camera,vision).sqrMagnitude>0f,"a visible tail edge is sufficient for rumble");
        Check(Offset(camera,vision,0f)==Vector2.zero,"paused gameplay has no environmental shake");
        vision.SetVisionMaskRadius(.5f); vision.RefreshSceneCameraSettings();
        Check(Offset(camera,vision)==Vector2.zero,"on-screen but masked body does not rumble");
        vision.SetVisionMaskRadius(2f); vision.RefreshSceneCameraSettings();
        var wall=Own(new GameObject("Blocks visibility barrier",typeof(BoxCollider2D)));
        wall.layer=LayerMask.NameToLayer("Blocks");
        wall.transform.position=move.Tail.position+Vector3.down*3f;
        wall.GetComponent<BoxCollider2D>().size=new Vector2(80f,.1f);
        Physics2D.SyncTransforms(); CameraCircularVision.NotifyBlockersChanged(); vision.RefreshSceneCameraSettings();
        Check(Offset(camera,vision)==Vector2.zero,"Blocks-hidden segments do not rumble");
        wall.SetActive(false); Physics2D.SyncTransforms(); CameraCircularVision.NotifyBlockersChanged(); vision.RefreshSceneCameraSettings();
        Check(Offset(camera,vision).sqrMagnitude>0f,"removing the wall restores visible rumble");
        camera.transform.position+=Vector3.right*200f;
        Check(Offset(camera)==Vector2.zero,"outside camera rectangle does not rumble even without a vision mask");
        camera.transform.position-=Vector3.right*200f;
        Check(Offset(camera).sqrMagnitude>0f,"baseline visible movement");
        source.enabled=false; Check(Offset(camera)==Vector2.zero,"disabled component unregisters its request");
        source.enabled=true; Nudge(source);
        move.SetMovementEnabled(false); Check(Offset(camera)==Vector2.zero,"stopped movement cannot retain tremor");
        move.SetMovementEnabled(true); source.AdvanceTremor(.4f);
        Check(Offset(camera)==Vector2.zero,"stationary pose does not rumble");
        Cleanup();
    }
    static void VerifyCycle(bool swallow)
    {
        var source=Worm(); var attack=source.GetComponent<SandwormEruptionAttack>();
        var move=source.GetComponent<SandwormBossPrototype>();
        Random.InitState(7491); Check(attack.TryLockAttack(new Vector2(8,6)),"attack route planned");
        if(swallow)
        {
            var bomb=Own(Object.Instantiate(Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem")));
            bomb.transform.position=attack.LockedAttackPoint;
        }
        var camera=CameraAt((attack.LockedAttackPoint+attack.LandingPoint)*.5f,60f);
        bool approach=false, emerge=false, air=false, land=false, fall=false, stun=false, returned=false;
        int previousEmerge=0, previousLanding=0, previousBurial=0;
        for(int i=0;i<5000;i++)
        {
            move.Advance(.02f); attack.AdvanceAttack(.02f); source.AdvanceTremor(.02f);
            var phase=attack.Phase; Vector2 offset=Offset(camera);
            if(phase==SandwormEruptionAttack.AttackPhase.Approaching && source.CurrentStrength>0f) approach=true;
            if(attack.EmergencePulses>0 && previousEmerge==0)
            { Check(source.CurrentStrength>.02f,"head emergence gives stronger impulse"); emerge=true; }
            if(phase==SandwormEruptionAttack.AttackPhase.Traversing && attack.EmergencePulses==8 && attack.LandingPulses==0)
            { Check(offset==Vector2.zero && source.CurrentStrength==0f,"fully airborne body cancels all quake tails immediately"); air=true; }
            if(attack.LandingPulses>0 && previousLanding==0)
            { Check(source.CurrentStrength>.02f,"head re-entry gives stronger impulse"); land=true; }
            if(phase==SandwormEruptionAttack.AttackPhase.Falling)
            { Check(offset==Vector2.zero,"explosion fall does not rumble"); fall=true; }
            if(phase==SandwormEruptionAttack.AttackPhase.Stunned)
            { Check(offset==Vector2.zero,"entire stun including landing reaction remains still"); stun=true; }
            if(phase==SandwormEruptionAttack.AttackPhase.ReturningToBurrow && source.CurrentStrength>0f) returned=true;
            if(attack.RecoveryBurialPulses>0 && previousBurial==0)
            { Check(source.CurrentStrength>.012f,"small recovery burrow still has a scaled entry impulse"); land=true; }
            Check(offset.magnitude<=camera.orthographicSize*.0281f,"tremor remains within configured screen-space amplitude");
            previousEmerge=attack.EmergencePulses; previousLanding=attack.LandingPulses; previousBurial=attack.RecoveryBurialPulses;
            if(phase==SandwormEruptionAttack.AttackPhase.Roaming) break;
        }
        Check(approach && emerge && air && land,"normal earth-contact stages covered");
        Check(!swallow || (fall && stun && returned),"swallowed-bomb fall/stun/return stages covered");
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming,"full cycle returns to roaming");
        source.gameObject.SetActive(false); Check(Offset(camera)==Vector2.zero,"unloading boss clears shake");
        Cleanup();
    }
    static IEnumerator Verify()
    {
        Time.timeScale=0f;
        VerifyVisibility(); yield return null;
        VerifyCycle(false); yield return null;
        VerifyCycle(true); yield return null;
        var first=Worm(); Nudge(first); var camera=CameraAt(Vector2.zero);
        var second=Worm(); Nudge(second);
        Vector2 both=Offset(camera); second.enabled=false;
        Check((Offset(camera)-both).sqrMagnitude<.0000001f,"multiple equal worms do not multiply amplitude");
        var other=SceneManager.CreateScene("Tremor isolated scene");
        SceneManager.MoveGameObjectToScene(first.gameObject,other);
        Check(Offset(camera)==Vector2.zero,"other scene cannot shake current scene camera");
        var unload=SceneManager.UnloadSceneAsync(other); while(!unload.isDone) yield return null;
        Check(Offset(camera)==Vector2.zero,"scene unload leaves no shake source behind");
        Cleanup();

        // Real LateUpdate ordering: world shake is removed before follow, with no drift,
        // and coexists with the existing fear-roar one-shot shake channel.
        var live=Worm(); live.GetComponent<SandwormEruptionAttack>().enabled=false;
        camera=CameraAt(Vector2.zero,60f); var follow=camera.gameObject.AddComponent<CameraFollowActiveZeldaMover>();
        Vector3 basis=camera.transform.position;
        Time.captureFramerate=60; Time.timeScale=1f; bool shook=false;
        for(int i=0;i<40;i++)
        {
            yield return null;
            float displacement=Vector3.Distance(basis,camera.transform.position);
            shook|=displacement>.001f;
            Check(displacement<.55f,"live camera offsets never accumulate into drift");
        }
        Check(shook,"actual camera receives environmental shake in LateUpdate");
        live.enabled=false; yield return null; yield return null;
        Check(Vector3.Distance(basis,camera.transform.position)<.00001f,"camera restores exact unshaken position");
        follow.PlayShake(.3f,.15f); bool skillShook=false;
        for(int i=0;i<30;i++) { yield return null; skillShook|=Vector3.Distance(basis,camera.transform.position)>.001f; }
        Check(skillShook,"existing skill one-shot shake still works");
        Check(Vector3.Distance(basis,camera.transform.position)<.00001f,"one-shot also leaves no residual offset");
    }
}
