using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Random=UnityEngine.Random;

[InitializeOnLoad]
public static class SandwormSwallowSmoke
{
    const string Prefab="Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    static readonly List<Object> owned=new List<Object>();
    static IEnumerator pending;
    static int frame=-1, checks;
    static SandwormSwallowSmoke() { EditorApplication.update+=Tick; }
    static T Own<T>(T value) where T:Object { owned.Add(value); return value; }
    static void Check(bool condition,string message)
    { checks++; if(!condition) throw new Exception("Sandworm swallow: "+message); }
    static void Cleanup()
    {
        for(int i=owned.Count-1;i>=0;i--) if(owned[i]!=null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        foreach(var effect in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(effect.gameObject);
        foreach(var site in Object.FindObjectsOfType<SandwormEruptionSite>()) site.Release();
    }
    public static void Run() { SessionState.SetBool("SandwormSwallowSmoke.Pending",true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount) return;
        frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool("SandwormSwallowSmoke.Pending",false))
            { SessionState.SetBool("SandwormSwallowSmoke.Pending",false); pending=Verify(); }
            if(SessionState.GetBool("SandwormSwallowSmoke.Preview",false))
            { SessionState.SetBool("SandwormSwallowSmoke.Preview",false); pending=RenderPreview(); }
            if(pending==null || pending.MoveNext()) return;
            pending=null; Cleanup(); Time.timeScale=1f; Time.captureFramerate=0;
            Debug.Log("SANDWORM_SWALLOW_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { pending=null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static SandwormEruptionAttack Worm()
    {
        var root=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)));
        root.transform.position=Vector3.zero;
        root.GetComponent<SandwormBossPrototype>().ResetPose();
        var attack=root.GetComponent<SandwormEruptionAttack>();
        Random.InitState(7491);
        Check(attack.TryLockAttack(new Vector2(8,6)),"fixture plans an attack");
        return attack;
    }
    static void Step(SandwormEruptionAttack attack,float delta=.037f)
    { attack.GetComponent<SandwormBossPrototype>().Advance(delta); attack.AdvanceAttack(delta); }
    static void Until(SandwormEruptionAttack attack,SandwormEruptionAttack.AttackPhase phase,float delta=.037f)
    {
        for(int i=0;i<2000 && attack.Phase!=phase;i++) Step(attack,delta);
        Check(attack.Phase==phase,"reaches "+phase+" without getting stuck; actual="+attack.Phase);
    }
    static T Spawn<T>(string resource,Vector2 at) where T:Component
    {
        var template=Resources.Load<GameObject>("PickupItems/"+resource);
        Check(template!=null,"resource exists: "+resource);
        var root=Own(Object.Instantiate(template)); root.transform.position=at; root.SetActive(true);
        return root.GetComponent<T>();
    }
    static BombPickupItem Bomb(Vector2 at,bool purple=false) => Spawn<BombPickupItem>(purple?"Bombs/PurpleBombPickupItem":"Bombs/BombPickupItem",at);
    static BatterySocket Socket(Vector2 at)
    {
        var root=Own(new GameObject("Swallow socket",typeof(BoxCollider2D)));
        root.transform.position=at;
        var socket=root.AddComponent<BatterySocket>();
        socket.ApplyRuntimeState(new BatterySocket.RuntimeState { installed=true });
        return socket;
    }
    static ZeldaCharacterData Target(Vector2 at)
    {
        var root=Own(new GameObject("Harmless re-entry target",typeof(BoxCollider2D)));
        root.transform.position=at;
        var data=root.AddComponent<CivilianZeldaCharacterData>();
        var so=new SerializedObject(data); so.FindProperty("health").intValue=200; so.ApplyModifiedPropertiesWithoutUndo();
        var saved=data.CaptureRuntimeState(); saved.health=saved.currentHealth=200; data.ApplyRuntimeState(saved);
        return data;
    }
    static void SetFloat(Object target,string name,float value)
    { var so=new SerializedObject(target); so.FindProperty(name).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void VerifyConsumption(int kind)
    {
        var attack=Worm(); var point=attack.LockedAttackPoint;
        Component item;
        int expected;
        if(kind<4)
        {
            var bomb=Bomb(point,(kind&1)!=0); expected=bomb.ExplosionAttackPower;
            if(kind>=2)
            { item=bomb.CreateConfiguredPlacedBomb(point,null); Own(item.gameObject); Object.DestroyImmediate(bomb.gameObject); }
            else item=bomb;
        }
        else if(kind==4) { item=Spawn<BatteryPickupItem>("BatteryPickupItem",point); expected=4; }
        else if(kind==5) { item=Socket(point); expected=4; }
        else if(kind==6) { item=Spawn<StabilityCrystalPickupItem>("StabilityCrystalPickupItem",point); expected=0; }
        else
        {
            var box=Spawn<CardboardBoxPickupItem>("CardboardBoxPickupItem",point);
            var bomb=Bomb(point);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(CardboardBoxPickupItem).GetField("hasStoredItem",flags).SetValue(box,true);
            typeof(CardboardBoxPickupItem).GetField("storedItemPrefab",flags).SetValue(box,bomb);
            if(kind==8) box.ArmStoredBomb(null);
            expected=bomb.ExplosionAttackPower;
            bomb.gameObject.SetActive(false); item=box;
        }
        var extra=new GameObject("Compound collider",typeof(BoxCollider2D)); extra.transform.SetParent(item.transform,false);
        var outside=Bomb(point+Vector2.up*(attack.DamageRadius+2f));
        int hp=attack.GetComponent<SandwormZeldaCharacterData>().CurrentHealth;
        Until(attack,SandwormEruptionAttack.AttackPhase.Traversing);
        Check(attack.SwallowedExplosiveCount==1 && attack.SwallowedAttackPower==expected,"kind "+kind+" swallowed once, even with compound colliders");
        Check(attack.GetComponent<SandwormZeldaCharacterData>().CurrentHealth==hp,"no damage/explosion until flight middle");
        Check(Object.FindObjectsOfType<BombExplosionVisual>().Length==0,"no ground explosion visual");
        Check(Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==0,"no ground bomb hitbox");
        Check(outside.isActiveAndEnabled,"outside-radius explosive is untouched");
        if(item is BatterySocket socket) Check(!socket.HasBattery && !socket.HasPower && socket.isActiveAndEnabled,"only socket payload removed and power cut");
        else if(item is CardboardBoxPickupItem box) Check(!box.HasStoredItem,"hidden bomb payload/fuse removed");
        else Check(!item.gameObject.activeInHierarchy,"consumed pickup disabled immediately");
        Check(!((IConsumableExplosive)item).TryConsumeExplosive(out _),"cannot consume same explosive twice");
        Until(attack,SandwormEruptionAttack.AttackPhase.Falling);
        Check(attack.GetComponent<SandwormZeldaCharacterData>().CurrentHealth==hp-expected*2,
            "kind "+kind+" internal blast deals exactly twice its own explosion power");
        Cleanup();
    }

    static void VerifyFullCycle(float dt,float stunDuration=5f)
    {
        var attack=Worm(); SetFloat(attack,"swallowedStunDuration",stunDuration);
        var move=attack.GetComponent<SandwormBossPrototype>();
        var data=attack.GetComponent<SandwormZeldaCharacterData>();
        var states=attack.GetComponent<SandwormStateController>();
        var bomb=Bomb(attack.LockedAttackPoint,true);
        int damage=bomb.ExplosionAttackPower*2, initialHealth=data.CurrentHealth;
        var target=Target(attack.LandingPoint);
        int harmfulLanding=0;
        attack.SegmentPulse+=(point,power,index,landing)=> { if(landing) harmfulLanding++; };
        Until(attack,SandwormEruptionAttack.AttackPhase.Falling,dt);
        Check(attack.EmergencePulses==8 && attack.LandingPulses==0,"all segments emerged before middle-body explosion, none landed yet");
        Check(data.CurrentHealth==initialHealth-damage,"one internal explosion applies doubled damage once");
        Check(Mathf.Abs(attack.InternalBlastPoint.x-attack.FallGroundPoint.x)<.001f &&
            attack.InternalBlastPoint.y>attack.LockedAttackPoint.y+attack.DamageRadius,"blast at apex above corresponding ground projection");
        Check(move.HasDestination,"roaming continuation preplanned before falling/stun");
        float previousMiddle=move.BodySegments[move.BodySegments.Length/2].position.y;
        while(attack.Phase==SandwormEruptionAttack.AttackPhase.Falling)
        {
            Step(attack,dt);
            float middle=move.BodySegments[move.BodySegments.Length/2].position.y;
            Check(middle<=previousMiddle+.001f,"body descends continuously"); previousMiddle=middle;
            foreach(var visual in attack.GetComponentsInChildren<SandwormSegmentVisual>())
                Check(!visual.HasUndergroundOverlay,"whole falling/stunned body is exposed");
        }
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Stunned,"fall finishes directly in stun");
        Check(states.State==SandwormState.AboveGround,"stunned boss actually above ground");
        Check(data.CaptureRuntimeState().characterSpecificState.Contains("\"state\":0"),"save cannot strand the boss exposed without its recovery timeline");
        foreach(var collider in attack.GetComponentsInChildren<Collider2D>()) Check(collider.enabled,"stunned boss is damageable through segment colliders");
        var indicator=attack.GetComponentInChildren<ZeldaAiStateIndicator>();
        Check(indicator!=null && indicator.GetComponent<SpriteRenderer>().enabled &&
            indicator.GetComponent<SpriteRenderer>().sprite.name!="Zelda AI State Indicator","uses shared stun-star icon");
        float ground=attack.FallGroundPoint.y;
        Check(Mathf.Abs(move.BodySegments[move.BodySegments.Length/2].position.y-ground)<.001f,
            "middle touches the endpoint horizontal line first");
        Check(move.Head.position.y>ground+1f && move.Tail.position.y>ground+1f,
            "head and tail are both raised at first contact");
        Check(move.Head.right.y>0f && move.Tail.right.y<0f,"head and tail tips point upwards along the curled body");
        Check(attack.GroundImpactPulses==1,"only middle dust emitted at initial impact");
        float remaining=attack.StunRemaining, time=0f;
        Vector2? settledHead=null;
        while(attack.Phase==SandwormEruptionAttack.AttackPhase.Stunned)
        {
            Step(attack,dt); time+=dt;
            if(attack.Phase==SandwormEruptionAttack.AttackPhase.Stunned && stunDuration-attack.StunRemaining>=.55f)
            {
                foreach(var visual in attack.GetComponentsInChildren<SandwormSegmentVisual>())
                    Check(Mathf.Abs(visual.transform.position.y-ground)<.001f,"body settles flat after the landing reaction");
                if(settledHead.HasValue) Check(Vector2.Distance(settledHead.Value,move.HeadPosition)<.00001f,
                    "settled body remains still for the rest of the authored stun");
                settledHead=move.HeadPosition;
                Check(attack.GroundImpactPulses==3,"head/tail dust follows middle contact exactly once each");
            }
        }
        Check(time>=remaining-.001f && time<remaining+dt+.001f,"configured stun duration respected exactly");
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.ReturningToBurrow,"stun directly hands off to cached return path");
        Check(!indicator.GetComponent<SpriteRenderer>().enabled,"stun icon disappears immediately");
        bool mixed=false;
        for(int i=0;i<2000 && attack.Phase!=SandwormEruptionAttack.AttackPhase.Roaming;i++)
        {
            Vector2 previous=move.HeadPosition;
            var oldPhase=attack.Phase;
            Vector2 tail=move.Tail.position;
            bool settling=attack.Phase==SandwormEruptionAttack.AttackPhase.Settling;
            Step(attack,dt);
            Check(Vector2.Distance(previous,move.HeadPosition)>move.WorldMoveSpeed*dt*.55f,
                "no return/burial/roaming handoff pause: "+oldPhase+" -> "+attack.Phase+" from="+previous+" to="+move.HeadPosition+" dt="+dt);
            if(settling) Check(Vector2.Distance(tail,move.Tail.position)<move.WorldMoveSpeed*dt*1.5f,"tail does not snap after burial");
            int buried=0; foreach(var visual in attack.GetComponentsInChildren<SandwormSegmentVisual>()) if(visual.HasUndergroundOverlay) buried++;
            mixed|=buried>0 && buried<8;
            float multiplier=CameraTemporaryFraming.GetMultiplier(attack.gameObject.scene);
            Check(Mathf.Abs(multiplier-(attack.IsExplosiveRecovery?1.3f:1f))<.0001f,"framing retained until full harmless burial");
        }
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && states.IsUnderground,"recovery completes back underground and roaming");
        Check(mixed && attack.RecoveryBurialPulses==8,"all eight segments bury sequentially");
        Check(harmfulLanding==0 && target.CurrentHealth==200,"return burial never damages nearby characters");
        Check(data.CurrentHealth==initialHealth-damage,"internal explosion cannot repeat during recovery");
        foreach(var collider in attack.GetComponentsInChildren<Collider2D>()) Check(!collider.enabled,"fully buried collision disabled");
        Check(!CameraVisionStreamingRegion.HasActiveRegions,"no retained region after recovery");
        Cleanup();
    }

    static void VerifyShortStun(float stun)
    {
        var attack=Worm(); SetFloat(attack,"swallowedStunDuration",stun);
        var move=attack.GetComponent<SandwormBossPrototype>(); Bomb(attack.LockedAttackPoint);
        Until(attack,SandwormEruptionAttack.AttackPhase.Falling,.0166667f);
        bool returned=false;
        for(int i=0;i<2000 && attack.Phase!=SandwormEruptionAttack.AttackPhase.Roaming;i++)
        {
            Vector2 head=move.Head.position, tail=move.Tail.position;
            Step(attack,.0166667f);
            Check(Vector2.Distance(head,move.Head.position)<1.4f && Vector2.Distance(tail,move.Tail.position)<1.4f,
                "short/zero stun never snaps the curled body into the return pose");
            returned|=attack.Phase==SandwormEruptionAttack.AttackPhase.ReturningToBurrow;
        }
        Check(returned && attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming,
            "short/zero stun completes recovery without an extra stationary wait");
        Check(attack.GroundImpactPulses==3,"short/zero stun retains the three staged ground impacts");
        Cleanup();
    }

    static IEnumerator Verify()
    {
        Time.timeScale=0f;
        for(int kind=0;kind<9;kind++) { VerifyConsumption(kind); yield return null; }
        foreach(float dt in new[]{.0166667f,.037f,.12f}) { VerifyFullCycle(dt); yield return null; }
        VerifyFullCycle(.037f,1.2f); yield return null;
        VerifyShortStun(0f); VerifyShortStun(.1f); yield return null;
        // Late explosive (after head has emerged), simultaneous payloads, and cancellation.
        var attack=Worm(); Until(attack,SandwormEruptionAttack.AttackPhase.Traversing);
        int originalHealth=attack.GetComponent<SandwormZeldaCharacterData>().CurrentHealth;
        int combinedPower=Bomb(attack.LockedAttackPoint).ExplosionAttackPower+Bomb(attack.LockedAttackPoint,true).ExplosionAttackPower+4;
        Spawn<BatteryPickupItem>("BatteryPickupItem",attack.LockedAttackPoint);
        Until(attack,SandwormEruptionAttack.AttackPhase.Stunned);
        Check(attack.SwallowedExplosiveCount==3,"later emergence pulses consume and combine multiple explosives");
        Check(attack.GetComponent<SandwormZeldaCharacterData>().CurrentHealth==originalHealth-2*combinedPower,
            "multiple swallowed explosives each contribute double their own power");
        attack.enabled=false;
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && attack.GetComponent<SandwormStateController>().IsUnderground,
            "disable during stun restores underground state");
        Check(CameraTemporaryFraming.GetMultiplier(attack.gameObject.scene)==1f,"cancel restores camera");
        Check(attack.GetComponentInChildren<ZeldaAiStateIndicator>()==null,"cancel clears stun marker");
        Cleanup(); yield return null;
        var foreignScene=SceneManager.CreateScene("Swallow scene isolation");
        attack=Worm();
        var foreign=Bomb(attack.LockedAttackPoint);
        SceneManager.MoveGameObjectToScene(foreign.gameObject,foreignScene);
        Until(attack,SandwormEruptionAttack.AttackPhase.Traversing);
        Check(attack.SwallowedExplosiveCount==0 && foreign.isActiveAndEnabled,"other scene's coincident explosive is untouched");
        Cleanup();
        var unload=SceneManager.UnloadSceneAsync(foreignScene); while(!unload.isDone) yield return null;
        attack=Worm(); Bomb(attack.LockedAttackPoint);
        Until(attack,SandwormEruptionAttack.AttackPhase.Stunned);
        var character=attack.GetComponent<SandwormZeldaCharacterData>();
        var saved=character.CaptureRuntimeState(); character.ApplyRuntimeState(saved);
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && attack.GetComponent<SandwormStateController>().IsUnderground,
            "loading during recovery clears transient pose, stun and framing");
        Check(CameraTemporaryFraming.GetMultiplier(attack.gameObject.scene)==1f,"load releases framing");
        Cleanup(); yield return null;
    }

    public static void Preview()
    { SessionState.SetBool("SandwormSwallowSmoke.Preview",true); EditorApplication.EnterPlaymode(); }

    static IEnumerator RenderPreview()
    {
        Time.timeScale=1f; Time.captureFramerate=30;
        var attack=Worm(); Bomb(attack.LockedAttackPoint,true);
        var camera=Own(new GameObject("Sandworm swallow preview camera")).AddComponent<Camera>();
        float middle=(attack.LockedAttackPoint.x+attack.LandingPoint.x)*.5f;
        camera.transform.position=new Vector3(middle,attack.LockedAttackPoint.y+5f,-50f);
        camera.orthographic=true; camera.orthographicSize=18f; camera.aspect=1.7777778f;
        camera.backgroundColor=new Color(15f/255f,33f/255f,60f/255f); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.allowHDR=false; camera.allowMSAA=false;
        var line=Own(new GameObject("Ground projection line")).AddComponent<LineRenderer>();
        line.sharedMaterial=Own(new Material(Shader.Find("Sprites/Default")));
        line.positionCount=2; line.SetPosition(0,new Vector3(middle-35f,attack.LockedAttackPoint.y,0));
        line.SetPosition(1,new Vector3(middle+35f,attack.LockedAttackPoint.y,0));
        line.startColor=line.endColor=new Color(.3f,.45f,.6f,.45f); line.widthMultiplier=.04f; line.sortingOrder=-10;
        var rt=Own(new RenderTexture(960,540,24)); camera.targetTexture=rt;
        var texture=Own(new Texture2D(960,540,TextureFormat.RGBA32,false));
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","sandworm-curled-fall-frames")); Directory.CreateDirectory(folder);
        bool started=false; int index=0, completed=0;
        var lastPhase=attack.Phase;
        for(int i=0;i<900;i++)
        {
            yield return null;
            if(attack.Phase!=SandwormEruptionAttack.AttackPhase.Approaching) started=true;
            if(!started) continue;
            if(i%2==0)
            {
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply(); RenderTexture.active=null;
                File.WriteAllBytes(Path.Combine(folder,index.ToString("D3")+".png"),texture.EncodeToPNG());
                if(attack.Phase!=lastPhase) Debug.Log("SWALLOW_PREVIEW frame="+index+" phase="+attack.Phase);
                lastPhase=attack.Phase; index++;
            }
            if(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && ++completed>20) break;
        }
        Check(attack.RecoveryBurialPulses==8,"preview completes swallowed-bomb recovery");
        camera.targetTexture=null; Time.captureFramerate=0;
        Debug.Log("SANDWORM_SWALLOW_PREVIEW_PASS frames="+index);
    }
}
