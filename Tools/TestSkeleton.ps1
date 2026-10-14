$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$data=Get-Content -Raw "$root/Assets/Scripts/Characters/SkeletonZeldaCharacterData.cs"
$base=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/SkeletonCharacterAi.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"};$b=$source.IndexOf('{',$start);$e=$b+1;$depth=1
 while($depth){if($source[$e] -eq '{'){$depth++};if($source[$e] -eq '}'){$depth--};$e++};$source.Substring($start,$e-$start)
}
$code=@'
using System;
public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Clamp01(float a)=>Math.Min(1,Math.Max(0,a));}
public static class Time{public static float deltaTime;}
public static class GameSaveSystem{public static bool IsLoading;}
public static class DominoSkillRuntime{public static void ShareDamage(object a,int b,object c){}public static void RemoveMark(object a){}}
public static class Debug{public static void Log(string a,object b){}}
public enum SendMessageOptions{DontRequireReceiver}
public class Audio{public void Stop(){}}
public struct Vector2{public static Vector2 down=>new Vector2();}
public class Body{public bool simulated=true;}
public class Enabled{public bool enabled=true;}
public class SkeletonCharacterAi:Enabled{public int resumes;public void ResumeAfterReassembly(){resumes++;}}
public class Base{
 protected bool isDead,soulDetonationDeath,silentGhostPossessionDeath;
 protected int currentHealth=5,health=5,controlTransferDamage;protected float damageFeedbackTimer;
 protected Audio damageAudioSource;public string name="skeleton";public object gameObject=new object();
 public Action ValuesChanged;public int sounds,notifications,explosions,destroyed;
 public bool IsDead=>isDead;public int HP=>currentHealth;
 protected void PlayDeathSound(){sounds++;}protected void SpawnDeathExplosion(){explosions++;}
 protected void BroadcastMessage(string s,object a,SendMessageOptions o){notifications++;}
 protected void Destroy(object o){destroyed++;}
 protected virtual void Update(){}
 public void Kill(){Die();}
 DEATH
 RESTORE
 protected virtual bool TryHandleDeathRemains(){return false;}
}
public class Skeleton:Base{
 float elapsed,scatterDuration=.45f,revivalDelay=4,assembleDuration=1.2f;bool reassembling;
 Body body=new Body();Enabled bodyCollider=new Enabled(),bodyRenderer=new Enabled(),ai=new SkeletonCharacterAi();object ownedRetention;
 public int Resumes=>((SkeletonCharacterAi)ai).resumes;
 public int cleanup,poses;public bool Collides=>bodyCollider.enabled;public bool Thinks=>ai.enabled;
 void BeginReassembly(float e){elapsed=e;reassembling=true;bodyCollider.enabled=false;body.simulated=false;bodyRenderer.enabled=false;ai.enabled=false;ownedRetention=new object();}
 void CleanupPieces(){cleanup++;}void AnimateRemains(){poses++;}
 void ApplyCharacterVisual(Enabled r,Vector2 f,bool m,bool a){}
 public void Step(float dt){Time.deltaTime=dt;Update();}
 public void RestoreDowned(float progress){isDead=true;BeginReassembly(progress);}
 HOOK
 UPDATE
 FRACTION
}
public static class Tests{
 static int count;static void Check(bool b,string s){count++;if(!b)throw new Exception(s);}
 public static int Run(){
  var normal=new Base();normal.Kill();Check(normal.IsDead&&normal.notifications==1&&normal.explosions==1&&normal.destroyed==1,"Ordinary death unchanged");
  var s=new Skeleton();
  for(int cycle=0;cycle<3;cycle++){
   s.Kill();s.Kill();Check(s.IsDead&&!s.Collides&&!s.Thinks,"Downed skeleton inert and no duplicate death");
   Check(s.notifications==cycle+1&&s.sounds==cycle+1&&s.explosions==0,"Normal notification/audio retained, no generic death explosion");
   s.Step(.45f);s.Step(4);Check(s.IsDead&&!s.Collides,"Wait complete still dead until assembly finishes");
   s.Step(1);Check(s.IsDead,"Do not revive halfway through assembly");s.Step(.21f);
   Check(!s.IsDead&&s.HP==5&&s.Collides&&s.Thinks,"Assembly restores health, body and AI");
   Check(s.cleanup==cycle+1,"Remains cleaned once per resurrection");
   Check(s.Resumes==cycle+1,"Finished assembly requests recovery/target evaluation once per revival");
  }
  s=new Skeleton();s.Kill();s.Step(0);Check(s.IsDead,"Paused time does not advance");
  GameSaveSystem.IsLoading=true;s.Step(20);Check(s.IsDead,"Loading does not advance");GameSaveSystem.IsLoading=false;
  s.RestoreDowned(5.5f);s.Step(.2f);Check(!s.IsDead,"Remaining saved time resumed");
  Check(Skeleton.ScatterFraction(0,.45f,4,1.2f)==0,"Starts assembled");
  Check(Skeleton.ScatterFraction(.45f,.45f,4,1.2f)==1,"Full scatter");
  Check(Skeleton.ScatterFraction(3,.45f,4,1.2f)==1,"Stays scattered during wait");
  Check(Skeleton.ScatterFraction(6,.45f,4,1.2f)==0,"Returns exactly to original offsets");
  float last=0;for(int i=0;i<=45;i++){float f=Skeleton.ScatterFraction(i*.01f,.45f,4,1.2f);Check(f>=last&&f>=0&&f<=1,"Scatter monotonic");last=f;}
  last=1;for(int i=0;i<=120;i++){float f=Skeleton.ScatterFraction(4.45f+i*.01f,.45f,4,1.2f);Check(f<=last+.000001f&&f>=0,"Assemble monotonic");last=f;}
  return count;
 }
}
'@
$code=$code.Replace(' DEATH',(Extract $base '    protected virtual void Die()')).Replace(' RESTORE',(Extract $base '    protected void RestoreAfterDeath()')).Replace(' HOOK',(Extract $data '    protected override bool TryHandleDeathRemains()')).Replace(' UPDATE',(Extract $data '    protected override void Update()')).Replace(' FRACTION',(Extract $data '    public static float ScatterFraction('))
Add-Type -TypeDefinition $code
$count=[Tests]::Run()
$maps=@{}
foreach($name in @('Front','Back','Side')){
 $body=[regex]::Match($data,"(?s)string\[\] $name = \{(.*?)\};").Groups[1].Value
 $rows=@([regex]::Matches($body,'"([.HBSDA]+)"')|ForEach-Object {$_.Groups[1].Value})
 if($rows.Count -ne 20 -or @($rows|Where-Object {$_.Length -ne 16}).Count){throw "Invalid 16x20 map: $name"};$maps[$name]=$rows
 if($rows[17] -ne $rows[16]){throw "Feet must stay flush with the two-pixel legs: $name"}
 if($name -ne 'Side'){
  foreach($y in 9..11){if($rows[$y].Substring(6,4).Contains('.')){throw "Rib cage must remain broad: $name row $y"}}
  foreach($y in 12..13){if($rows[$y].Substring(5,6) -ne '..BB..'){throw "Waist must narrow to a connected two-pixel spine: $name row $y"}}
  if($rows[14] -ne '.....BBBBBB.....'){throw "Pelvis must be one full row connecting both legs: $name"}
 }else{
  if($rows[13] -ne '........BB......' -or $rows[14] -ne '.....BBBBB......'){throw 'Side spine and pelvis must match the slim waist silhouette'}
  if($rows[12].Substring(7,2) -ne 'AA'){throw 'Keep the visible side hand in the middle of the body'}
 }
 foreach($x in 0..15){if($rows[15][$x] -ne '.' -and $rows[14][$x] -eq '.'){throw "Leg must connect directly to pelvis: $name column $x"}}
}
$begin=Extract $data '    private void BeginReassembly('
if($begin -match 'AddComponent<(?:BoxCollider2D|CircleCollider2D|PolygonCollider2D|Rigidbody2D)>'){throw 'Fragments cannot have physics'}
foreach($guard in @('body.simulated=false','bodyCollider.enabled=false','mover.enabled=false','ai.enabled=false','CameraVisionStreamingExempt')){if(!$begin.Contains($guard)){throw "Missing remains guard: $guard"}}
if(!$data.Contains('state.characterSpecificState=JsonUtility.ToJson') -or !$data.Contains('BeginReassembly(saved.elapsed)')){throw 'Reassembly save progress missing'}
if(!$ai.Contains('candidate.GetComponent<SkeletonZeldaCharacterData>() == null')){throw 'Skeleton friendship filter missing'}
$hit=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
if(!$hit.Contains('target is SkeletonZeldaCharacterData) return;')){throw 'Friendly melee suppression missing'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2Npcs/Skeleton-level2.prefab"
if($prefab.Contains('armed_golem') -or $prefab.Contains('hostilityActivated:')){throw 'Unwanted automaton pickup/activation behavior'}
foreach($guid in @('50e9a6bebf3b4b178492c80c41c8e326','5ea01236f74b419ba20618f4a46e007e')){if(!$prefab.Contains($guid)){throw 'Missing skeleton component'}}
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(900,320);$g=[Drawing.Graphics]::FromImage($bitmap);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
$font=[Drawing.Font]::new('Arial',12)
$colors=@{H=@(247,250,240);B=@(209,219,204);A=@(209,219,204);S=@(110,130,130);D=@(23,33,48)}
for($d=0;$d -lt 4;$d++){
 $map=$maps[@('Front','Back','Side','Side')[$d]];$ox=35+$d*220
 for($y=0;$y -lt 20;$y++){for($x=0;$x -lt 16;$x++){
  $ix=if($d -eq 3){15-$x}else{$x};$c=$colors[[string]$map[$y][$ix]];if(!$c){continue}
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,$c[0],$c[1],$c[2]));$g.FillRectangle($brush,$ox+$x*10,15+$y*10,10,10);$brush.Dispose()
 }}
 $g.DrawString(@('Front','Back','Left','Right')[$d],$font,[Drawing.Brushes]::White,$ox+45,240)
}
$path=Join-Path $root '.codex-temp/Skeleton-preview.png';$bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png);$font.Dispose();$g.Dispose();$bitmap.Dispose()
Write-Output "PASS: $count production death/revival timing checks, four-direction maps, friendly melee, inert fragments and save wiring. Unity services substituted; preview: $path"
