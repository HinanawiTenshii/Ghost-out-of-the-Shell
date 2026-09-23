$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Items/PlacedBomb.cs"
function Method($signature) {
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    $source.Substring($start,$end-$start)
}
$stub=@'
using System;
using System.Collections.Generic;
public struct Vector3 {public float x,y,z;}
public class Transform {public Vector3 position;}
public class GameObject {
    public static List<GameObject> all=new List<GameObject>();
    public string name,scene="Level2";public Transform transform=new Transform();
    public bool destroyed;public float delay;public AudioSource audio;public bool exempt;
    public GameObject(string n){name=n;all.Add(this);}
    public T AddComponent<T>() where T:new(){var c=new T();if(c is AudioSource)audio=c as AudioSource;if(c is CameraVisionStreamingExempt)exempt=true;return c;}
}
public class CameraVisionStreamingExempt {}
namespace UnityEngine.SceneManagement {public static class SceneManager {public static void MoveGameObjectToScene(GameObject o,string s){o.scene=s;}}}
public class AudioClip {public float length;public AudioClip(float l){length=l;}}
public static class Resources {
    public static int loads;public static AudioClip clip=new AudioClip(.54f);
    public static T Load<T>(string p) where T:class {if(p!="Audio/BombExplosion")throw new Exception(p);loads++;return clip as T;}
}
public enum AudioRolloffMode {Logarithmic}
public class AudioSource {
    public bool playOnAwake=true,loop=true;public float dopplerLevel=1,minDistance,maxDistance,spatialBlend,pitch,volume;
    public AudioRolloffMode rolloffMode;public AudioClip clip;public int plays;public void Play(){plays++;}
}
public static class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}public static float Abs(float x){return Math.Abs(x);}}
public class PlacedBomb {
    public GameObject gameObject=new GameObject("Bomb");public Transform transform {get{return gameObject.transform;}}
    private static AudioClip defaultExplosionSound;private AudioClip explosionSound;
    public float explosionSoundVolume=1,explosionSoundPitch=1,explosionSoundSpatialBlend=.85f,explosionSoundMaxDistance=24;
    private bool exploded,configured=true;public int visuals,hitboxes,notifications,refreshes;
    public void Setup(AudioClip c=null){explosionSound=ResolveExplosionSound(c);}
    private void RefreshStreamingRetention(){refreshes++;}
    private void CreateAttackHitbox(Vector3 p){hitboxes++;} private void CreateExplosionVisual(Vector3 p){visuals++;}
    private void NotifyNearbyAi(Vector3 p){notifications++;} private void CreateInvestigationPulse(Vector3 p){}
    private void Destroy(GameObject o){o.destroyed=true;} private void Destroy(GameObject o,float t){o.delay=t;}
'@
$production=''
foreach($sig in '    private static AudioClip ResolveExplosionSound(', '    private void PlayExplosionSound(', '    private void Explode()', '    public void DetonateImmediately()') {$production+=(Method $sig)+"`n"}
$tests=@'
}
public static class BombAudioTests {
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static GameObject LastSound(){for(int i=GameObject.all.Count-1;i>=0;i--)if(GameObject.all[i].name=="Bomb Explosion Sound")return GameObject.all[i];return null;}
    static int SoundCount(){int n=0;foreach(var o in GameObject.all)if(o.audio!=null)n++;return n;}
    public static int Run(){
        var bomb=new PlacedBomb();bomb.Setup();Check(Resources.loads==1&&SoundCount()==0,"Preload at placement, not playback");
        bomb.DetonateImmediately();var sound=LastSound();
        Check(sound.audio.clip==Resources.clip&&sound.audio.plays==1,"Default clip plays once");
        Check(bomb.visuals==1&&bomb.hitboxes==1&&bomb.notifications==1,"Visual, damage and AI still occur");
        Check(bomb.gameObject.destroyed&&!sound.destroyed,"Sound outlives bomb");
        Check(sound.scene==bomb.gameObject.scene&&sound.exempt,"Sound is scene-local and streaming exempt");
        Check(!sound.audio.playOnAwake&&!sound.audio.loop&&sound.audio.dopplerLevel==0,"No repeat or Doppler distortion");
        Check(Math.Abs(sound.delay-.64f)<.001,"Sound cleanup follows duration");
        int count=SoundCount();bomb.DetonateImmediately();Check(SoundCount()==count&&bomb.visuals==1,"Repeated detonation is silent");
        var second=new PlacedBomb();second.Setup();second.DetonateImmediately();Check(Resources.loads==1&&SoundCount()==count+1,"Other bombs reuse clip but play independently");
        var custom=new AudioClip(1.2f);var configured=new PlacedBomb {explosionSoundPitch=2,explosionSoundVolume=.3f};configured.Setup(custom);configured.DetonateImmediately();sound=LastSound();
        Check(sound.audio.clip==custom&&sound.audio.pitch==2&&Math.Abs(sound.audio.volume-.3f)<.001,"Custom audio settings preserved");
        Check(Math.Abs(sound.delay-.7f)<.001,"Custom pitch controls cleanup");
        var mute=new PlacedBomb {explosionSoundVolume=0};mute.Setup();count=SoundCount();mute.DetonateImmediately();
        Check(SoundCount()==count&&mute.visuals==1,"Mute does not disable explosion");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stub+$production+$tests)
if(!$source.Contains('explosionSound = ResolveExplosionSound(configuredExplosionSound);')){throw 'Production placement must resolve audio before detonation'}
Write-Output "PASS: $([BombAudioTests]::Run()) production bomb audio lifecycle checks plus placement preload wiring."
