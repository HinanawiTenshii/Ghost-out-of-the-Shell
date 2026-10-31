using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CardboardInspectionProbe : ZeldaCharacterAiBase
{
    public int attacks;
    protected override void TryAttackTarget(Vector2 direction) { attacks++; }
    public void Act() { TickSuspicious(.02f); }
}

[InitializeOnLoad]
public static class CardboardBoxSuspicionSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static CardboardBoxSuspicionSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object obj, string name)
    {
        for (var type=obj.GetType(); type!=null; type=type.BaseType)
        { var field=type.GetField(name,F|BindingFlags.DeclaredOnly); if(field!=null)return field; }
        throw new Exception("Missing field "+name);
    }
    static void Set(object o,string n,object v) => Field(o,n).SetValue(o,v);
    static T Get<T>(object o,string n) => (T)Field(o,n).GetValue(o);
    static bool Observe(CardboardInspectionProbe ai,float seconds) =>
        (bool)typeof(ZeldaCharacterAiBase).GetMethod("UpdateCardboardBoxSuspicion",F).Invoke(ai,new object[]{seconds});
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"isolated only");
        PlayerSettings.productName="cardboard-suspicion-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(nameof(CardboardBoxSuspicionSmoke),true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(nameof(CardboardBoxSuspicionSmoke),false))
            { SessionState.SetBool(nameof(CardboardBoxSuspicionSmoke),false); suite=Verify(); deadline=EditorApplication.timeSinceStartup+60; }
            if(suite==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout");
            if(suite.MoveNext())return;
            suite=null; Debug.Log("CARDBOARD_SUSPICION_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e){suite=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        while(Time.frameCount<3 || RetroSceneLoadReveal.IsPreparingGameplay)yield return null;
        Time.timeScale=0;
        var template=Resources.Load<GameObject>("PickupItems/CardboardBoxPickupItem");
        Check(template!=null,"real cardboard prefab loaded");
        foreach(bool ground in new[]{false,true}) foreach(float maximum in new[]{100f,240f})
        {
            var observer=new GameObject("Box observer").AddComponent<CardboardInspectionProbe>();
            observer.enabled=false;
            Set(observer,"facingDirection",Vector2.right);
            Set(observer,"visionRadius",7f);
            // Existing save/prefab settings must not restore the old fast box rate.
            float legacyRate=maximum==100f?35f:500f;
            SavedScalarFields.Apply(observer,new List<SavedScalarFields.Value>{
                new SavedScalarFields.Value{declaringType=typeof(ZeldaCharacterAiBase).FullName,name="maximumSuspicion",number=maximum},
                new SavedScalarFields.Value{declaringType=typeof(ZeldaCharacterAiBase).FullName,name="suspicionIncreasePerSecond",number=legacyRate}
            });
            GameObject target;
            ZeldaFourWayMover wearer=null;
            ClockworkPuppetBoxDriver driver=null;
            if(ground)
            {
                target=Object.Instantiate(template,new Vector3(.8f,0),Quaternion.identity);
                var box=target.GetComponent<CardboardBoxPickupItem>();
                driver=target.AddComponent<ClockworkPuppetBoxDriver>(); driver.enabled=false;
                Set(driver,"lastMovementTime",Time.time);
                Set(box,"storedPuppetDriver",driver);
            }
            else
            {
                target=new GameObject("Moving wearer"); target.transform.position=new Vector3(.8f,0);
                wearer=target.AddComponent<ZeldaFourWayMover>(); wearer.enabled=true;
                Set(wearer,"moveDirection",Vector2.right);
                target.AddComponent<CardboardBoxWearState>().Configure(template.GetComponent<CardboardBoxPickupItem>(),wearer);
            }
            Physics2D.SyncTransforms();
            Check(Observe(observer,0) && observer.CurrentState==ZeldaAiState.Suspicious,"moving box starts suspicion");
            Check(observer.SuspicionValue==0,"initial zero delta does not advance progress");
            Observe(observer,5);
            Check(Mathf.Abs(observer.SuspicionProgress-.5f)<.0001f,"half full after five seconds for either box type");
            Check(!Get<bool>(observer,"isInspectingCardboardBox"),"not attacking at old three-second threshold");
            observer.Act(); Check(observer.attacks==0,"nearby box remains safe before ten seconds");
            // Stopping after being seen still follows the original inspection rule.
            if(wearer!=null)Set(wearer,"moveDirection",Vector2.zero);
            if(driver!=null)Set(driver,"lastMovementTime",float.NegativeInfinity);
            var saved=observer.CaptureSaveState();
            Set(observer,"suspicionValue",0f); observer.ApplySaveState(saved);
            Observe(observer,4.99f);
            Check(!Get<bool>(observer,"isInspectingCardboardBox") && observer.SuspicionProgress>.998f,
                "save/restored partial suspicion still waits until ten seconds");
            observer.Act(); Check(observer.attacks==0,"no attack at 9.99 seconds");
            Observe(observer,.0101f);
            Check(Get<bool>(observer,"isInspectingCardboardBox") && Mathf.Approximately(observer.SuspicionProgress,1),
                "attack inspection begins at ten seconds");
            observer.Act(); Check(observer.attacks==1,"in-range box now permits attack");
            Check(Get<float>(observer,"suspicionIncreasePerSecond")==legacyRate,"ordinary suspicion setting unchanged");
            // Losing sight retains existing memory/decay rather than advancing a wall clock.
            Set(observer,"isInspectingCardboardBox",false); Set(observer,"suspicionValue",maximum*.5f);
            Set(observer,"facingDirection",Vector2.left); Set(observer,"lastSuspiciousCardboardBoxSeenTime",Time.time-10);
            Observe(observer,.1f);
            Check(Mathf.Abs(observer.SuspicionValue-(maximum*.5f-2.5f))<.001f,"original out-of-sight decay retained");
            Object.Destroy(observer.gameObject); Object.Destroy(target); yield return null; yield return null;
        }
    }
}
