# Exercises production transition and audio methods with Unity data stand-ins.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
function Method($signature) {
    $start=$source.IndexOf($signature)
    if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$enum=[regex]::Match($source,'(?s)public enum ZeldaAiState\s*\{.*?\}').Value
$stubs=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
    public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
    public static float Distance(Vector2 a,Vector2 b){float x=a.x-b.x,y=a.y-b.y;return (float)Math.Sqrt(x*x+y*y);}
}
public class Transform {public Vector2 position;}
public static class Mathf {
    public static float Max(float a,float b){return Math.Max(a,b);}
    public static float Clamp01(float a){return Math.Max(0,Math.Min(1,a));}
}
public static class Application {public static bool isPlaying=true;}
public static class GameSaveSystem {public static bool IsLoading;}
public class AudioClip {}
public class AudioSource {
    public bool playOnAwake=true,loop=true;public float spatialBlend=1,dopplerLevel=1;
    public int priority,calls;public AudioClip lastClip;public float lastVolume;
    public void PlayOneShot(AudioClip clip,float gain){calls++;lastClip=clip;lastVolume=gain;}
}
public class GameObject {
    public string scene="Level1";public Transform transform=new Transform();public List<AudioSource> sources=new List<AudioSource>();
    public T AddComponent<T>() where T:new(){var result=new T();if(result is AudioSource)sources.Add(result as AudioSource);return result;}
}
public class ZeldaFourWayMover {
    public GameObject gameObject=new GameObject();public Transform transform {get{return gameObject.transform;}}
}
public static class ZeldaRuntimeRegistry {
    public static string active="Level1";public static ZeldaFourWayMover player=new ZeldaFourWayMover();
    public static ZeldaFourWayMover GetControlledMover(){return player;}
    public static string GetGameplayScene(GameObject o){return o.scene=="DontDestroyOnLoad"?active:o.scene;}
}
public static class Resources {
    public static int loads;public static string lastPath;public static AudioClip clip=new AudioClip();
    public static T Load<T>(string path) where T:class {loads++;lastPath=path;return clip as T;}
}
public class AwarenessHarness {
    public GameObject gameObject=new GameObject();public Transform transform {get{return gameObject.transform;}}
    public bool isActiveAndEnabled=true,enableAwarenessSound=true,IsIgnoringPlayer;
    public bool IsStunned {get{return currentState==ZeldaAiState.Stunned;}}
    public AudioClip awarenessSound;public float awarenessSoundVolume=.55f,awarenessSoundMaxDistance=18;
    private AudioSource awarenessAudioSource;private static AudioClip defaultAwarenessSound;
    public ZeldaAiState currentState;
    bool retainLoadedUntilIdle,hasConsumedUnawareFirstHit;float hostileIndicatorTimer,hostileIndicatorDuration=.7f;
    void OnStateExited(ZeldaAiState a,ZeldaAiState b){} void OnStateEntered(ZeldaAiState a,ZeldaAiState b){}
    void InvalidateNavigationPlan(){}
    void ResetHostileAttackCycle(){} // Combat state is covered by TestHostileAttackCycle.
    public int Calls {get{return awarenessAudioSource==null?0:awarenessAudioSource.calls;}}
    public AudioSource Source {get{return awarenessAudioSource;}}
    public static bool Gate(ZeldaAiState a,ZeldaAiState b){return ShouldPlayAwarenessCue(a,b);}
    public void Enter(ZeldaAiState s,bool cue=true){ChangeState(s,cue);}
'@
$production=(Method '    protected virtual void ChangeState')+(Method '    private static bool ShouldPlayAwarenessCue')+(Method '    private void PlayAwarenessCue')
$tests=@'
}
public static class AwarenessTests {
    static int checks;
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    public static int Run(){
        foreach(ZeldaAiState old in Enum.GetValues(typeof(ZeldaAiState)))
        foreach(ZeldaAiState next in Enum.GetValues(typeof(ZeldaAiState))){
            bool expected=old!=next&&(next==ZeldaAiState.Alert||next==ZeldaAiState.Hostile)&&!(old==ZeldaAiState.Alert&&next==ZeldaAiState.Hostile);
            Check(AwarenessHarness.Gate(old,next)==expected,"Transition rule "+old+" -> "+next);
            var ai=new AwarenessHarness {currentState=old};ai.Enter(next);
            Check(ai.Calls==(expected&&old!=ZeldaAiState.Stunned?1:0),"Production ChangeState "+old+" -> "+next);
        }
        var npc=new AwarenessHarness();npc.Enter(ZeldaAiState.Alert);
        for(int i=0;i<100;i++)npc.Enter(ZeldaAiState.Alert);
        Check(npc.Calls==1,"Repeated warning ticks only play once");
        npc.Enter(ZeldaAiState.Hostile);Check(npc.Calls==1,"Escalation is silent");
        for(int i=0;i<100;i++)npc.Enter(ZeldaAiState.Hostile);
        Check(npc.Calls==1,"Repeated hostile ticks are silent");
        npc.Enter(ZeldaAiState.Search);npc.Enter(ZeldaAiState.Hostile);
        Check(npc.Calls==2,"New detection after losing player plays again");
        Check(npc.gameObject.sources.Count==1,"NPC reuses a single source");
        Check(!npc.Source.loop&&!npc.Source.playOnAwake&&npc.Source.spatialBlend==0&&npc.Source.dopplerLevel==0,"One-shot 2D configuration");
        var second=new AwarenessHarness();second.Enter(ZeldaAiState.Hostile);
        Check(second.Calls==1&&npc.Calls==2,"Different NPCs have independent transitions");
        Check(Resources.loads==1&&Resources.lastPath=="Audio/NpcAwarenessAlert","Default resource loaded once and shared");
        var saved=new AwarenessHarness();saved.Enter(ZeldaAiState.Hostile,false);
        Check(saved.Calls==0&&saved.currentState==ZeldaAiState.Hostile,"Silent save restoration still restores state");
        saved.Enter(ZeldaAiState.Hostile);Check(saved.Calls==0,"No delayed alert after silent restore");
        GameSaveSystem.IsLoading=true;var loading=new AwarenessHarness();loading.Enter(ZeldaAiState.Alert);
        Check(loading.Calls==0,"Loading suppresses perception-time audio");GameSaveSystem.IsLoading=false;
        var disabled=new AwarenessHarness {enableAwarenessSound=false};disabled.Enter(ZeldaAiState.Alert);
        Check(disabled.Calls==0,"Inspector disable respected");
        var zero=new AwarenessHarness {awarenessSoundVolume=0};zero.Enter(ZeldaAiState.Hostile);Check(zero.Calls==0,"Zero volume allocates no source");
        var ignored=new AwarenessHarness {IsIgnoringPlayer=true};ignored.Enter(ZeldaAiState.Alert);
        Check(ignored.Calls==0&&ignored.currentState==ZeldaAiState.Idle,"Blocked transition is silent");
        var stopped=new AwarenessHarness {isActiveAndEnabled=false};stopped.Enter(ZeldaAiState.Hostile);Check(stopped.Calls==0,"Disabled AI is silent");
        var custom=new AwarenessHarness {awarenessSound=new AudioClip(),awarenessSoundVolume=.4f};custom.Enter(ZeldaAiState.Alert);
        Check(custom.Source.lastClip==custom.awarenessSound&&Math.Abs(custom.Source.lastVolume-.4f)<.001,"Custom sound and volume override");
        var far=new AwarenessHarness();far.transform.position=new Vector2(18,0);far.Enter(ZeldaAiState.Alert);Check(far.Calls==0,"Out of range silent");
        var edge=new AwarenessHarness();edge.transform.position=new Vector2(16,0);edge.Enter(ZeldaAiState.Alert);
        Check(edge.Calls==1&&edge.Source.lastVolume>0&&edge.Source.lastVolume<.55,"Outer band fades volume");
        var foreign=new AwarenessHarness();foreign.gameObject.scene="Level2";foreign.Enter(ZeldaAiState.Hostile);Check(foreign.Calls==0,"Other additive scene silent");
        ZeldaRuntimeRegistry.player.gameObject.scene="DontDestroyOnLoad";
        var traveler=new AwarenessHarness();traveler.Enter(ZeldaAiState.Alert);Check(traveler.Calls==1,"Traveling persistent player still hears alerts");
        ZeldaRuntimeRegistry.player=null;var noPlayer=new AwarenessHarness();noPlayer.Enter(ZeldaAiState.Hostile);Check(noPlayer.Calls==0,"No player handled safely");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs.Replace('public struct Vector2', $enum+"`npublic struct Vector2")+$production+$tests) -CompilerOptions '/nowarn:0414,0649'
$count=[AwarenessTests]::Run()
if(!(Method '    public virtual void ApplySaveState').Contains('ChangeState(state.state, false);')){throw 'Save restore must explicitly suppress audio'}
$wav=Join-Path $root 'Assets/Resources/Audio/NpcAwarenessAlert.wav'
$bytes=[IO.File]::ReadAllBytes($wav)
if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($bytes,8,4) -ne 'WAVE'){throw 'Invalid default audio asset'}
if(!(Test-Path "$wav.meta")){throw 'Missing audio import metadata'}
Write-Output "PASS: $count production transition/audio checks; default WAV, saved-state silence and resource wiring verified."
