$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Items/DisposableHandgunAudio.cs"
$stubs=@'
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public class Transform {public Vector3 position;}
 public class Object {public static void Destroy(GameObject g,float delay){g.destroyDelay=delay;}}
 public class AudioClip {public float length=.32f;}
 public class AudioSource {public bool playOnAwake=true,loop=true;public AudioClip clip;public float volume,pitch,spatialBlend=1,dopplerLevel=1;public int plays;public void Play(){plays++;}}
 public class GameObject {
  public static System.Collections.Generic.List<GameObject> all=new System.Collections.Generic.List<GameObject>();
  public System.Collections.Generic.List<object> components=new System.Collections.Generic.List<object>();
  public string name;public Transform transform=new Transform();public float destroyDelay;public SceneManagement.Scene scene;
  public GameObject(string n){name=n;all.Add(this);}public T AddComponent<T>() where T:new(){var c=new T();components.Add(c);return c;}
  public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T)return (T)c;return null;}
 }
 public static class Resources {public static int loads;public static AudioClip clip=new AudioClip();public static T Load<T>(string p) where T:class {if(p!="Audio/HandgunFire")throw new System.Exception("Wrong resource");loads++;return clip as T;}}
}
namespace UnityEngine.SceneManagement {
 public struct Scene {public bool valid,isLoaded;public bool IsValid()=>valid;}
 public static class SceneManager {public static void MoveGameObjectToScene(UnityEngine.GameObject g,Scene s){g.scene=s;}}
}
public class CameraVisionStreamingExempt {}
public static class AudioTests {
 static int checks;static void Check(bool b,string m){checks++;if(!b)throw new System.Exception(m);}
 public static int Run(){
  var scene=new UnityEngine.SceneManagement.Scene{valid=true,isLoaded=true};
  DisposableHandgunAudio.Preload();DisposableHandgunAudio.Preload();Check(UnityEngine.Resources.loads==1,"Clip preloaded and cached");
  DisposableHandgunAudio.Play(scene,new UnityEngine.Vector3(3,7));Check(UnityEngine.GameObject.all.Count==1,"One standalone sound object");
  var root=UnityEngine.GameObject.all[0];var source=root.GetComponent<UnityEngine.AudioSource>();
  Check(source.plays==1&&!source.loop&&!source.playOnAwake,"Single explicit playback");
  Check(source.spatialBlend==0&&source.dopplerLevel==0&&source.pitch==1,"No overhead-listener attenuation or doppler pitch");
  Check(System.Math.Abs(source.volume-.65f)<.001,"Shot volume");
  Check(root.scene.isLoaded&&root.transform.position.x==3&&root.transform.position.y==7,"Audio located in correct scene at muzzle");
  Check(root.GetComponent<CameraVisionStreamingExempt>()!=null,"Audio cannot be streamed out");
  Check(System.Math.Abs(root.destroyDelay-(source.clip.length+.08f))<.001,"Sound cleans up after its tail independently of consumed item");
  DisposableHandgunAudio.Play(new UnityEngine.SceneManagement.Scene(),new UnityEngine.Vector3());Check(UnityEngine.GameObject.all.Count==1,"Invalid scene does not play");
  typeof(DisposableHandgunAudio).GetField("shot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).SetValue(null,null);
  UnityEngine.Resources.clip=null;DisposableHandgunAudio.Play(scene,new UnityEngine.Vector3());Check(UnityEngine.GameObject.all.Count==1,"Missing resource does not create an empty audio object");
  return checks;
 }
}
'@
Add-Type -TypeDefinition ($source+"`n"+$stubs)
$count=[AudioTests]::Run()
$path=Join-Path $root 'Assets/Resources/Audio/HandgunFire.wav'
$bytes=[IO.File]::ReadAllBytes($path)
$dataOffset=0;$dataLength=0
for($offset=12;$offset+8 -le $bytes.Length;){
 $id=[Text.Encoding]::ASCII.GetString($bytes,$offset,4);$length=[BitConverter]::ToInt32($bytes,$offset+4)
 if($id -eq 'fmt '){if([BitConverter]::ToInt16($bytes,$offset+8) -ne 1 -or [BitConverter]::ToInt16($bytes,$offset+10) -ne 1 -or [BitConverter]::ToInt32($bytes,$offset+12) -ne 44100 -or [BitConverter]::ToInt16($bytes,$offset+22) -ne 16){throw 'Expected mono 44100Hz PCM16'}}
 if($id -eq 'data'){$dataOffset=$offset+8;$dataLength=$length;break}
 $offset+=8+$length+($length%2)
}
if($dataLength -le 0){throw 'No PCM data'}
$onset=-1;$peak=0
for($i=0;$i -lt $dataLength/2;$i++){$sample=[Math]::Abs([int][BitConverter]::ToInt16($bytes,$dataOffset+$i*2));$peak=[Math]::Max($peak,$sample);if($sample -gt 100 -and $onset -lt 0){$onset=$i}}
if($onset -lt 0 -or $onset -gt 88){throw "Slow onset: $onset samples"}
if($peak -ge 32767){throw 'Clipped PCM'}
$duration=$dataLength/2/44100
if($duration -lt .25 -or $duration -gt .35){throw 'Expected a short powder report with a compact decay'}
$meta=Get-Content -Raw "$path.meta"
foreach($setting in @('preloadAudioData: 1','loadInBackground: 0','compressionFormat: 0')){if(!$meta.Contains($setting)){throw "Missing $setting"}}
Write-Output "PASS: $count production one-shot lifecycle checks; PCM onset $onset samples, peak $peak, duration $duration seconds; preload settings verified."
