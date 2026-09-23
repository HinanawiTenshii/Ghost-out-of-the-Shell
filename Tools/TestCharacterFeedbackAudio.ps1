# Execute production damage/death/audio methods with Unity data stubs, not Play Mode.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
function Method($signature){
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$stubs=@'
using System;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;}
public class Transform {public Vector3 position;}
public class GameObject {
    public static List<GameObject> all=new List<GameObject>();
    public string name,scene="Level1";public Transform transform=new Transform();
    public bool destroyed;public float destroyDelay=-1;public List<object> components=new List<object>();
    public GameObject(string name="actor"){this.name=name;all.Add(this);}
    public T AddComponent<T>() where T:new(){var c=new T();components.Add(c);var a=c as AudioSource;if(a!=null)a.gameObject=this;return c;}
    public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return c as T;return null;}
}
namespace UnityEngine.SceneManagement {
    public static class SceneManager {public static void MoveGameObjectToScene(GameObject o,string s){o.scene=s;}}
}
public class CameraVisionStreamingExempt {}
public class AudioClip {public float length;public AudioClip(float duration){length=duration;}}
public enum AudioRolloffMode {Logarithmic}
public class AudioSource {
    public GameObject gameObject;public bool playOnAwake=true,loop=true,playing;
    public float pitch=1,spatialBlend,maxDistance,minDistance,dopplerLevel=1,volume;
    public AudioRolloffMode rolloffMode;public AudioClip clip,lastOneShot;public int oneShots,plays,stops;
    public void PlayOneShot(AudioClip c,float v){oneShots++;lastOneShot=c;volume=v;playing=true;}
    public void Play(){plays++;playing=true;}
    public void Stop(){stops++;playing=false;}
}
public static class Resources {
    public static AudioClip damage=new AudioClip(.211f),death=new AudioClip(.265f);public static int damageLoads,deathLoads;
    public static T Load<T>(string p) where T:class {
        if(p=="Audio/CharacterDamage"){damageLoads++;return damage as T;}
        if(p=="Audio/CharacterDeath"){deathLoads++;return death as T;}
        throw new Exception("Unexpected resource "+p);
    }
}
public static class Mathf {
    public static float Clamp(float x,float a,float b){return Math.Max(a,Math.Min(b,x));}
    public static float Clamp01(float x){return Clamp(x,0,1);}
    public static int Max(int x,int y){return Math.Max(x,y);}
    public static float Max(float x,float y){return Math.Max(x,y);}
    public static float Abs(float x){return Math.Abs(x);}
}
public enum SendMessageOptions {DontRequireReceiver}
public static class Debug {public static void Log(string m,object o){}}
public class CardboardBoxWearState {public bool blocks;public bool TryBlockAttack(ZeldaCharacterData a){return blocks;}}
public class ZeldaFourWayMover {public CardboardBoxWearState ActiveCardboardBox;public int interruptions;public void InterruptPossessionByDamage(){interruptions++;}}
public class ZeldaCharacterAiBase {public int multiplier=1;public int ResolveIncomingAttackDamage(int d){return d*multiplier;}public static void NotifyPlayerAttackWitnesses(ZeldaCharacterData a,ZeldaCharacterData v){}}
public static class ZeldaRuntimeRegistry {public static string GetGameplayScene(GameObject o){return o.scene=="DontDestroyOnLoad"?"Level1":o.scene;}}
public static class DominoSkillRuntime {
    public static int shares,deaths;public static Action<ZeldaCharacterData> deathCallback;
    public static void ShareDamage(ZeldaCharacterData o,int d,ZeldaCharacterData a){shares++;}
    public static void ShareDeath(ZeldaCharacterData o){deaths++;if(deathCallback!=null)deathCallback(o);}
}
public class ZeldaCharacterData {
    public GameObject gameObject=new GameObject();public string name {get{return gameObject.name;}}
    public Transform transform {get{return gameObject.transform;}}
    public int currentHealth=6,health=6;public bool isDead,soulDetonationDeath;
    public bool IsGhostLike;private bool silentGhostPossessionDeath;
    public AudioClip damageSound,deathSound;public float damageSoundVolume=1,deathSoundVolume=1;
    public float damageSoundPitch=1,deathSoundPitch=1,damageSoundSpatialBlend=.75f,deathSoundSpatialBlend=.75f,damageSoundMaxDistance=16,deathSoundMaxDistance=16;
    private AudioSource damageAudioSource;private static AudioClip defaultDamageSound,defaultDeathSound;
    public Action ValuesChanged;float damageFeedbackTimer,damageFeedbackDuration=.18f;public int explosions,notifications;
    T GetComponent<T>() where T:class {return gameObject.GetComponent<T>();}
    void CaptureDamageFeedbackBaseVisual(){} void SpawnDeathExplosion(){explosions++;}
    void BroadcastMessage(string m,object o,SendMessageOptions s){notifications++;}
    void Destroy(GameObject o){o.destroyed=true;} void Destroy(GameObject o,float delay){o.destroyDelay=delay;}
    public void Damage(int amount,bool shared=false){ApplyDamage(amount,null,shared);}
    public void Kill(){Die();}
    public AudioSource DamageSource {get{return damageAudioSource;}}
'@
$production=''
foreach($signature in @('    private void PlayDamageSound()','    private void PlayDeathSound()',
    '    private void ApplyDamage(', '    protected virtual void Die()', '    public void DestroyAfterSuccessfulPossession()',
    '    private void EnsureDamageAudioSource()', '    private static void InitializeAudioSource(',
    '    private static void ConfigureAudioSource(')){$production+=(Method $signature)+"`n"}
$tests=@'
}
public static class FeedbackTests {
    static int checks;
    static void Check(bool ok,string m){checks++;if(!ok)throw new Exception(m);}
    static AudioSource LastDeath(){for(int i=GameObject.all.Count-1;i>=0;i--)if(GameObject.all[i].name.EndsWith(" Death Sound"))return GameObject.all[i].GetComponent<AudioSource>();return null;}
    static int DeathCount(){int n=0;foreach(var o in GameObject.all)if(o.name.EndsWith(" Death Sound"))n++;return n;}
    public static int Run(){
        var c=new ZeldaCharacterData();c.Damage(1);
        Check(c.currentHealth==5&&c.DamageSource.oneShots==1,"Nonfatal hit emits one damage sound");
        Check(c.DamageSource.lastOneShot==Resources.damage,"Empty slot uses default CRT clip");
        Check(!c.DamageSource.playOnAwake&&!c.DamageSource.loop&&c.DamageSource.dopplerLevel==0,"Damage source one-shot settings");
        c.Damage(1);Check(c.DamageSource.oneShots==2&&c.gameObject.components.Count==1,"Repeated hits reuse source");
        c.Damage(4);Check(c.isDead&&c.currentHealth==0,"Fatal damage still kills");
        Check(c.DamageSource.oneShots==2&&c.DamageSource.stops==1,"Fatal hit skips hurt and stops previous hurt tail");
        var death=LastDeath();Check(death.plays==1&&death.clip==Resources.death,"Death emits one default sound");
        Check(c.gameObject.destroyed&&!death.gameObject.destroyed&&death.gameObject!=c.gameObject,"Death sound survives body destruction");
        Check(death.gameObject.GetComponent<CameraVisionStreamingExempt>()!=null,"Death sound cannot be distance-suspended");
        Check(Math.Abs(death.gameObject.destroyDelay-(Resources.death.length+.1f))<.001,"Audio root cleaned after clip ends");
        Check(!death.playOnAwake&&!death.loop,"No automatic/repeating death playback");
        int count=DeathCount();c.Kill();c.Damage(10);Check(DeathCount()==count&&c.DamageSource.oneShots==2,"Dead character does not play again");
        var zero=new ZeldaCharacterData();zero.Damage(0);zero.Damage(-2);Check(zero.DamageSource==null&&zero.currentHealth==6,"Zero/negative damage silent");
        var box=new ZeldaCharacterData();var mover=box.gameObject.AddComponent<ZeldaFourWayMover>();mover.ActiveCardboardBox=new CardboardBoxWearState {blocks=true};
        box.Damage(2);Check(box.DamageSource==null&&box.currentHealth==6&&mover.interruptions==0,"Blocked hit silent");
        box.Damage(2,true);Check(box.DamageSource.oneShots==1&&box.currentHealth==4,"Shared damage follows existing bypass semantics");
        var lethal=new ZeldaCharacterData();lethal.gameObject.AddComponent<ZeldaCharacterAiBase>().multiplier=3;lethal.Damage(2);
        Check(lethal.isDead&&lethal.DamageSource==null,"Resolved fatal sneak damage only plays death");
        var custom=new ZeldaCharacterData {damageSound=new AudioClip(.1f),deathSound=new AudioClip(1.2f),damageSoundVolume=.3f,deathSoundVolume=.4f,deathSoundPitch=2};
        custom.Damage(1);Check(custom.DamageSource.lastOneShot==custom.damageSound&&Math.Abs(custom.DamageSource.volume-.3f)<.001,"Custom damage preserved");
        custom.Kill();death=LastDeath();Check(death.clip==custom.deathSound&&Math.Abs(death.volume-.4f)<.001,"Custom death preserved");
        Check(Math.Abs(death.gameObject.destroyDelay-.7f)<.001,"Cleanup accounts for custom pitch");
        var mute=new ZeldaCharacterData {damageSoundVolume=0,deathSoundVolume=0};count=DeathCount();mute.Damage(1);mute.Kill();
        Check(mute.DamageSource==null&&DeathCount()==count,"Volume zero mutes defaults too");
        var traveler=new ZeldaCharacterData();traveler.gameObject.scene="DontDestroyOnLoad";traveler.Kill();
        Check(LastDeath().gameObject.scene=="Level1","Persistent player death sound belongs to active level");
        var other=new ZeldaCharacterData();other.gameObject.scene="Level2";other.Kill();Check(LastDeath().gameObject.scene=="Level2","Ordinary scene ownership retained");
        var chained=new ZeldaCharacterData();count=DeathCount();DominoSkillRuntime.deathCallback=x=>x.Kill();chained.Kill();DominoSkillRuntime.deathCallback=null;
        Check(DeathCount()==count+1,"Reentrant shared death still plays once");
        var ghost=new ZeldaCharacterData {IsGhostLike=true,deathSound=new AudioClip(1)};count=DeathCount();ghost.DestroyAfterSuccessfulPossession();
        Check(DeathCount()==count,"Ghost possession suppresses default and custom death audio");
        Check(ghost.isDead&&ghost.gameObject.destroyed&&ghost.notifications==1&&ghost.explosions==1,"Silent ghost transfer preserves notification, explosion and cleanup");
        ghost.Kill();ghost.DestroyAfterSuccessfulPossession();Check(DeathCount()==count,"Ghost transfer reentry stays silent");
        var body=new ZeldaCharacterData();body.DestroyAfterSuccessfulPossession();Check(DeathCount()==count+1,"Non-ghost possession keeps death audio");
        var actualDeath=new ZeldaCharacterData {IsGhostLike=true};actualDeath.Kill();Check(DeathCount()==count+2,"Ghost deaths unrelated to possession still play audio");
        Check(Resources.damageLoads==1&&Resources.deathLoads==1,"Default clips cached across characters");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs+$production+$tests) -CompilerOptions '/nowarn:0414,0649'
$count=[FeedbackTests]::Run()
foreach($name in 'CharacterDamage','CharacterDeath'){
    $wav=Join-Path $root "Assets/Resources/Audio/$name.wav"
    $bytes=[IO.File]::ReadAllBytes($wav)
    if([Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($bytes,8,4) -ne 'WAVE'){throw "Invalid audio $name"}
    if(!(Test-Path "$wav.meta")){throw "Missing audio meta $name"}
}
Write-Output "PASS: $count production damage/death/audio lifecycle checks and both WAV resource assets."
