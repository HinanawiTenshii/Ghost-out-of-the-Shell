$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$production=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateAudio.cs"
$stubs=@'
namespace UnityEngine {
 public class DisallowMultipleComponent:System.Attribute{}
 public class RangeAttribute:System.Attribute{public RangeAttribute(float a,float b){}}
 public class MinAttribute:System.Attribute{public MinAttribute(float x){}}
 public class RuntimeInitializeOnLoadMethodAttribute:System.Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType x){}}
 public enum RuntimeInitializeLoadType{SubsystemRegistration}
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public static Vector2 zero=>new Vector2();public float sqrMagnitude=>x*x+y*y;
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator *(Vector2 a,float n)=>new Vector2(a.x*n,a.y*n);
  public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
  public static float Distance(Vector2 a,Vector2 b)=>(float)System.Math.Sqrt((a-b).sqrMagnitude);
  public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
 }
 public struct Vector3 {public float x,y,z; public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public class Component{public GameObject gameObject;public Transform transform=>gameObject.transform;}
 public class MonoBehaviour:Component{}
 public class Transform:Component{public Vector3 position;}
 public class GameObject {
  public bool activeInHierarchy=true;public Transform transform;
  public SceneManagement.Scene scene=new SceneManagement.Scene{value=1};
  public System.Collections.Generic.List<AudioSource> sources=new System.Collections.Generic.List<AudioSource>();
  public GameObject(){transform=new Transform{gameObject=this};}
  public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};if(c is AudioSource)sources.Add(c as AudioSource);return c;}
 }
 public class AudioClip{}
 public class AudioSource:Component {
  public bool playOnAwake,loop,ignoreListenerPause,isPlaying,wasPaused;public float spatialBlend,dopplerLevel,volume,panStereo;public int priority,plays,stops,pauses,resumes;
  public AudioClip clip;
  public void Play(){isPlaying=true;wasPaused=false;plays++;}
  public void Stop(){isPlaying=false;wasPaused=false;stops++;}
  public void Pause(){wasPaused=isPlaying;isPlaying=false;pauses++;}
  public void UnPause(){isPlaying=wasPaused;wasPaused=false;resumes++;}
 }
 public class Camera:Component{public static Camera main;}
 public static class Resources{public static int loads;public static T Load<T>(string path) where T:class {loads++;return new AudioClip() as T;}}
 public static class Time{public static float timeScale=1,unscaledDeltaTime=.2f,unscaledTime=1;}
 public static class Mathf {
  public static float Max(float a,float b)=>System.Math.Max(a,b);
  public static float Clamp(float x,float a,float b)=>System.Math.Max(a,System.Math.Min(x,b));
  public static float Clamp01(float x)=>Clamp(x,0,1);
  public static float MoveTowards(float a,float b,float d)=>System.Math.Abs(b-a)<=d?b:a+System.Math.Sign(b-a)*d;
 }
}
namespace UnityEngine.SceneManagement {
 public struct Scene {public int value;public static bool operator ==(Scene a,Scene b)=>a.value==b.value;public static bool operator !=(Scene a,Scene b)=>a.value!=b.value;
  public override bool Equals(object o)=>o is Scene&&((Scene)o).value==value;public override int GetHashCode()=>value;}
 public static class SceneManager {public static Scene GetActiveScene()=>new Scene{value=1};}
}
public class ZeldaFourWayMover:UnityEngine.Component{}
public static class ZeldaRuntimeRegistry {
 public static ZeldaFourWayMover controlled;
 public static ZeldaFourWayMover GetControlledMover()=>controlled;
 public static UnityEngine.SceneManagement.Scene GetGameplayScene(UnityEngine.GameObject o)=>o.scene;
}
public static class GameSaveSystem{public static bool IsLoading;}
'@
$tests=@'
public static class ElectricAudioTests {
 static int checks;
 static void Check(bool b,string m){checks++;if(!b)throw new System.Exception(m);}
 static void Tick(ElectricGateAudio a){UnityEngine.Time.unscaledTime+=.2f;typeof(ElectricGateAudio).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(a,null);}
 public static int Run(){
  for(int i=0;i<=2000;i++){
   float distance=i*.01f;
   float gain=ElectricGateAudio.DistanceGain(distance,1.5f,16f);
   Check(gain>=0&&gain<=1,"Gain out of range");
   if(i>0)Check(gain<=ElectricGateAudio.DistanceGain(distance-.01f,1.5f,16f)+.00001f,"Gain not monotonic");
  }
  Check(ElectricGateAudio.DistanceGain(1.5f,1.5f,16f)==1,"Near field volume");
  Check(ElectricGateAudio.DistanceGain(16,1.5f,16)==0,"Far sound must be fully silent");
  var near=ElectricGateAudio.NearestPoint(new UnityEngine.Vector2(9,1),new UnityEngine.Vector2(0,0),new UnityEngine.Vector2(10,0));
  Check(near.x==9&&near.y==0,"Long gate must use nearest segment position");
  near=ElectricGateAudio.NearestPoint(new UnityEngine.Vector2(12,2),new UnityEngine.Vector2(0,0),new UnityEngine.Vector2(10,0));
  Check(near.x==10,"Endpoint clamp");
  near=ElectricGateAudio.NearestPoint(new UnityEngine.Vector2(3,2),new UnityEngine.Vector2(1,1),new UnityEngine.Vector2(1,1));
  Check(near.x==1&&near.y==1,"Degenerate pair safe");
  var go=new UnityEngine.GameObject();var a=go.AddComponent<ElectricGateAudio>();
  var first=new UnityEngine.GameObject();var second=new UnityEngine.GameObject();second.transform.position=new UnityEngine.Vector3(10,0);
  ZeldaRuntimeRegistry.controlled=new ZeldaFourWayMover{gameObject=new UnityEngine.GameObject()};
  ZeldaRuntimeRegistry.controlled.transform.position=new UnityEngine.Vector3(9,1,99);
  a.Initialize(first.transform,second.transform);
  Check(go.sources.Count==3&&UnityEngine.Resources.loads==3,"Three preloaded sources per pair");
  var hum=go.sources[0];var warning=go.sources[1];var strike=go.sources[2];
  foreach(var source in go.sources)Check(!source.playOnAwake&&source.spatialBlend==0&&source.dopplerLevel==0,"Unexpected native 3D attenuation");
  Check(hum.loop&&warning.loop&&!strike.loop,"Loop roles");
  a.SetState(false,false);Tick(a);Check(!hum.isPlaying&&!warning.isPlaying,"Unpowered silent");
  a.SetState(true,false);Tick(a);Check(hum.isPlaying&&!warning.isPlaying,"Powered idle hum only");
  Check(System.Math.Abs(hum.volume-a.humVolume)<.00001f,"XY distance must ignore Z and use segment");
  for(int i=0;i<10;i++)Tick(a);Check(hum.plays==1,"Hum restarted each frame");
  a.SetState(true,true);Tick(a);Check(warning.isPlaying&&warning.plays==1,"Warning layer enters once");
  for(int i=0;i<10;i++)Tick(a);Check(warning.plays==1,"Warning restarted each tick");
  a.SetState(true,false);Tick(a);Check(hum.isPlaying&&!warning.isPlaying,"Warning clears without stopping powered hum");
  a.PlayStrike(new UnityEngine.Vector2(9,0));Check(strike.plays==1&&strike.isPlaying,"Attack immediate playback");
  a.PlayStrike(new UnityEngine.Vector2(9,0));Check(strike.plays==1,"Simultaneous arcs/victims do not double audio");
  a.SetState(false,false);Tick(a);Check(!hum.isPlaying&&strike.isPlaying,"Last-charge strike tail survives power off");
  a.SetState(true,true);Tick(a);UnityEngine.Time.timeScale=0;Tick(a);Check(!hum.isPlaying&&!warning.isPlaying&&!strike.isPlaying,"Pause freezes all voices");
  a.PlayStrike(new UnityEngine.Vector2(9,0));Check(strike.plays==1,"Paused attack suppressed");
  UnityEngine.Time.timeScale=1;Tick(a);Check(hum.isPlaying&&warning.isPlaying&&strike.isPlaying,"Resume existing voices");
  GameSaveSystem.IsLoading=true;Tick(a);Check(!hum.isPlaying,"Loading suspends voices");GameSaveSystem.IsLoading=false;Tick(a);Check(hum.isPlaying,"Loading recovery");
  ZeldaRuntimeRegistry.controlled.transform.position=new UnityEngine.Vector3(9,20);Tick(a);Check(!hum.isPlaying&&!warning.isPlaying&&strike.volume==0,"Out of range all silent");
  a.PlayStrike(new UnityEngine.Vector2(9,0));Check(strike.plays==1,"Distant attacks do not allocate/start voices");
  ZeldaRuntimeRegistry.controlled.transform.position=new UnityEngine.Vector3(9,1);Tick(a);Check(hum.isPlaying,"Return to range resumes loop");
  ZeldaRuntimeRegistry.controlled.gameObject.scene=new UnityEngine.SceneManagement.Scene{value=2};Tick(a);Check(!hum.isPlaying&&!strike.isPlaying,"No sound leakage across scenes");
  ZeldaRuntimeRegistry.controlled.gameObject.scene=new UnityEngine.SceneManagement.Scene{value=1};Tick(a);
  second.activeInHierarchy=false;Tick(a);Check(!hum.isPlaying&&!warning.isPlaying&&!strike.isPlaying,"Missing receiver stops sound");
  second.activeInHierarchy=true;a.SetState(true,true);Tick(a);
  typeof(ElectricGateAudio).GetMethod("OnDisable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(a,null);
  Check(!hum.isPlaying&&!warning.isPlaying&&!strike.isPlaying,"Disposal silences all audio");
  var other=new UnityEngine.GameObject().AddComponent<ElectricGateAudio>();other.Initialize(first.transform,second.transform);
  Check(UnityEngine.Resources.loads==3,"Clips shared across pairs");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($production+"`n"+$stubs+"`n"+$tests)
$checks=[ElectricAudioTests]::Run()
$network=Get-Content -Raw "$root/Assets/Scripts/Interaction/ElectricGateNetwork.cs"
foreach($fragment in 'audio.Initialize(a.transform, b.transform)','audio.SetState(active, warning)','audio.PlayStrike(current)') {
 if(!$network.Contains($fragment)){throw "Missing integration: $fragment"};$checks++
}
foreach($name in 'CurrentLoop','WarningLoop','Discharge') {
 $path="$root/Assets/Resources/Audio/ElectricGate/$name.wav"
 $meta=Get-Content -Raw ($path+'.meta')
 foreach($setting in 'loadType: 0','compressionFormat: 0','forceToMono: 1','preloadAudioData: 1','loadInBackground: 0') {
  if(!$meta.Contains($setting)){throw "Bad import: $name $setting"};$checks++
 }
 $reader=[IO.BinaryReader]::new([IO.File]::OpenRead($path))
 try {
  if([Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'RIFF'){throw 'Not WAV'}
  $reader.ReadInt32()|Out-Null;$reader.ReadBytes(4)|Out-Null;$samples=$null
  while($reader.BaseStream.Position -lt $reader.BaseStream.Length){
   $chunk=[Text.Encoding]::ASCII.GetString($reader.ReadBytes(4));$size=$reader.ReadInt32();$next=$reader.BaseStream.Position+$size+($size%2)
   if($chunk -eq 'fmt '){$format=$reader.ReadInt16();$channels=$reader.ReadInt16();$rate=$reader.ReadInt32();$reader.ReadInt32()|Out-Null;$reader.ReadInt16()|Out-Null;$bits=$reader.ReadInt16()}
   if($chunk -eq 'data'){$samples=$reader.ReadBytes($size)}
   $reader.BaseStream.Position=$next
  }
  if($format -ne 1 -or $channels -ne 1 -or $rate -ne 44100 -or $bits -ne 16 -or !$samples){throw "Bad encoding: $name"};$checks++
  $peak=0;$firstLoud=-1
  for($i=0;$i -lt $samples.Length;$i+=2){$v=[Math]::Abs([int][BitConverter]::ToInt16($samples,$i));if($v -gt $peak){$peak=$v};if($firstLoud -lt 0 -and $v -gt 327){$firstLoud=$i/2}}
  if($peak -lt 1000 -or $peak -gt 30000){throw "Silent/clipped asset: $name"};$checks++
  if($name -eq 'Discharge'){
   if($firstLoud/$rate -gt .003){throw 'Attack onset delay'}
   if($samples.Length/2/$rate -gt .5){throw 'Attack tail too long'};$checks+=2
  }else{
   $seam=[Math]::Abs([int][BitConverter]::ToInt16($samples,0)-[int][BitConverter]::ToInt16($samples,$samples.Length-2))/32768.0
   if($seam -gt .08){throw "Large loop seam: $name $seam"};$checks++
  }
 } finally {$reader.Dispose()}
}
Write-Output "PASS: $checks production audio behavior, attenuation, wiring, PCM/onset and loop-seam checks (stubs; not Play Mode listening)."
