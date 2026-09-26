$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorData.cs"
function Method($signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
public struct Bounds {public Vector3 center;public void Encapsulate(Bounds b){}}
public class Transform {public Vector3 position;public void SetParent(Transform t,bool b){}}
public class GameObject {
 public static List<GameObject> all=new List<GameObject>();public string name,scene="Level2";public Transform transform=new Transform();
 public bool destroyed,exempt;public float cleanup;public AudioSource audio;
 public GameObject(string name){this.name=name;all.Add(this);}
 public T AddComponent<T>() where T:new(){var v=new T();if(v is AudioSource)audio=v as AudioSource;if(v is CameraVisionStreamingExempt)exempt=true;return v;}
}
public class SpriteRenderer {public object sprite=new object();public bool forceRenderingOff;public Bounds bounds=new Bounds{center=new Vector3(4,5)};}
public class Collider2D {public bool enabled=true;public Bounds bounds;}
public class CameraVisionStreamingExempt {}
public class ZeldaCharacterData {}
public static class CameraCircularVision {public static void NotifyBlockersChanged(){}public static void NotifyBlockersChanged(Bounds b){}}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void MoveGameObjectToScene(GameObject o,string s){o.scene=s;}}}
public class AudioClip {public float length;public AudioClip(float n){length=n;}}
public static class Resources {
 public static int loads;public static bool missing;public static AudioClip wood=new AudioClip(1.334f),glass=new AudioClip(1.253f);
 public static T Load<T>(string path) where T:class{loads++;if(missing)return null;if(path=="Audio/DoorBreak")return wood as T;if(path=="Audio/GlassBreak")return glass as T;throw new Exception(path);}
}
public enum AudioRolloffMode {Linear}
public class AudioSource {public bool playOnAwake=true,loop=true;public float dopplerLevel=1,minDistance,maxDistance,spatialBlend,volume,pitch;public AudioClip clip;public AudioRolloffMode rolloffMode;public int plays;public void Play(){plays++;}}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp01(float a)=>Math.Max(0,Math.Min(1,a));}
public class DoorData {
 public enum BreakMaterial{Wood,Glass}
 public BreakMaterial breakMaterial;public AudioClip breakSound;public float breakSoundVolume=.7f,breakSoundSpatialBlend=.8f,breakSoundMaxDistance=22;
 private static AudioClip defaultWoodBreakSound,defaultGlassBreakSound;
 public GameObject gameObject=new GameObject("Door");public string name=>gameObject.name;public Transform transform=>gameObject.transform;
 public SpriteRenderer spriteRenderer=new SpriteRenderer(),shakeVisual;public bool isDestroyed,isShaking,originalForceRenderingOff;
 public int durability=2,fragments,pulses,notifications;public float shakeTimer,shakeDuration=.28f,shakeElapsed;
 public Collider2D[] colliders={new Collider2D(),new Collider2D()};
 public T[] GetComponentsInChildren<T>(bool inactive)=>colliders as T[];
 void StopShake(){isShaking=false;}void NotifyNearbyAi(Vector3 p,ZeldaCharacterData s){notifications++;}void SpawnInvestigationPulse(Vector3 p){pulses++;}void SpawnFragments(){fragments++;}
 void Destroy(GameObject o){o.destroyed=true;}void Destroy(GameObject o,float delay){o.cleanup=delay;}
 public void Preload(){ResolveBreakSound();}public static void ResetCache(){defaultWoodBreakSound=defaultGlassBreakSound=null;}
'@
foreach($sig in @('public void ReceiveAttack(int attackPower)',("public void ReceiveAttack(`r`n"),'private void BreakDoor(','private AudioClip ResolveBreakSound()','private void PlayBreakSound(')){
 # Source line endings may be LF or CRLF after Unity imports.
 if($sig.EndsWith("`r`n") -and !$source.Contains($sig)){$sig=$sig.Replace("`r`n","`n")}
 $code+=Method $sig
}
$code+=@'
}
public static class DestructionChecks {
 static int count;static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 static int Voices(){int n=0;foreach(var g in GameObject.all)if(g.audio!=null)n++;return n;}
 static GameObject Last(){for(int i=GameObject.all.Count-1;i>=0;i--)if(GameObject.all[i].audio!=null)return GameObject.all[i];return null;}
 public static int Run(){
  var door=new DoorData();door.Preload();Check(Resources.loads==1&&Voices()==0,"Preloaded, no startup sound");
  door.ReceiveAttack(1);Check(Voices()==0&&!door.isDestroyed&&door.isShaking,"Nonbreaking hit only shakes");
  door.ReceiveAttack(2);var voice=Last();Check(voice.audio.clip==Resources.wood&&voice.audio.plays==1,"Wood destruction plays once");
  Check(door.gameObject.destroyed&&!voice.destroyed,"Voice survives destroyed door");
  Check(door.fragments==1&&door.pulses==1&&door.notifications==1,"Fragment and AI paths preserved");
  Check(!door.colliders[0].enabled&&!door.colliders[1].enabled,"Collision removed immediately");
  Check(voice.scene==door.gameObject.scene&&voice.exempt,"Scene-local, streaming exempt");
  Check(voice.transform.position.x==4&&voice.transform.position.y==5,"Sound at visible destruction center");
  Check(!voice.audio.playOnAwake&&!voice.audio.loop&&voice.audio.dopplerLevel==0,"No accidental repeats or Doppler");
  Check(Math.Abs(voice.cleanup-1.434f)<.001,"Cleanup follows clip length");
  Check(voice.audio.volume==.7f&&voice.audio.spatialBlend==.8f,"Configured mix retained");
  int voices=Voices();door.ReceiveAttack(100);Check(Voices()==voices&&door.fragments==1,"Deferred Destroy cannot replay");
  var another=new DoorData();another.Preload();Check(Resources.loads==1,"Shared wood cache");
  var glass=new DoorData{breakMaterial=DoorData.BreakMaterial.Glass,durability=0};glass.Preload();glass.ReceiveAttack(1);
  Check(Last().audio.clip==Resources.glass&&Resources.loads==2,"Glass material fallback");
  Check(Math.Abs(Last().cleanup-1.353f)<.001,"Glass tail outlives head object");
  var custom=new AudioClip(.2f);var configured=new DoorData{breakSound=custom};configured.Preload();configured.ReceiveAttack(3);
  Check(Last().audio.clip==custom&&Resources.loads==2,"Custom clip wins without extra loads");
  voices=Voices();var mute=new DoorData{breakSoundVolume=0};mute.ReceiveAttack(4);Check(Voices()==voices&&mute.isDestroyed,"Mute still destroys");
  var legacy=new DoorData{breakSoundVolume=5,breakSoundSpatialBlend=2,breakSoundMaxDistance=0};legacy.ReceiveAttack(4);
  Check(Last().audio.volume==1&&Last().audio.spatialBlend==1&&Last().audio.maxDistance>3,"Legacy settings clamped safely");
  DoorData.ResetCache();Resources.missing=true;voices=Voices();var missing=new DoorData();missing.ReceiveAttack(4);
  Check(missing.isDestroyed&&missing.fragments==1&&Voices()==voices,"Missing asset never blocks destruction");Resources.missing=false;
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([DestructionChecks]::Run()) production destruction/audio checks with Unity stubs."
$glass=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Glass.prefab"
if(!$glass.Contains('breakMaterial: 1') -or !$glass.Contains('e29d6d35122645fdaca034b6e11b8966')){throw 'Glass prefab not configured'}
if((Get-Content -Raw "$root/Assets/Scenes/SampleScene.unity").Contains('93bf4cef59a02b9468783c28dc705be7')){throw 'Sample glass override still uses previous sound'}
foreach($name in @('DoorBreak','GlassBreak')) {
 $path="$root/Assets/Resources/Audio/$name.wav"
 $info=(& ffprobe -v error -show_entries stream=codec_name,sample_rate,channels -of json $path | ConvertFrom-Json).streams[0]
 if($info.codec_name -ne 'pcm_s16le' -or $info.sample_rate -ne '44100' -or $info.channels -ne 1){throw "Invalid PCM: $name"}
 $duration=[double]::Parse((& ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $path),[Globalization.CultureInfo]::InvariantCulture)
 if($duration -lt 1.2 -or $duration -gt 1.5){throw "Destruction tail should remain audible, without a long trailing wait: $name ($duration seconds)"}
}
Write-Output 'PASS: prefab, sample-scene overrides and mono PCM assets.'
