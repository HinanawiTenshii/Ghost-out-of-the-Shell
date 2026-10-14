# Execute actual damage, unlinking, death and possession methods with presentation-only stubs.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$data = Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaCharacterData.cs"
$domino = Get-Content -Raw "$root/Assets/Scripts/Characters/DominoSkillRuntime.cs"
$mover = Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
function Extract($source, $signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing $signature" }
    $end = $source.IndexOf('{', $start) + 1; $depth = 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    $source.Substring($start, $end - $start)
}
$code = @'
using System;
using System.Collections.Generic;
public struct Vector2 { public static Vector2 zero => new Vector2(); }
public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); }
public enum SendMessageOptions { DontRequireReceiver }
public static class Debug { public static void Log(string s,object c){} }
public class GameObject {
    public List<object> components=new List<object>();public bool destroyed;
    public T GetComponent<T>() where T:class { foreach(var c in components)if(c is T)return (T)c;return null; }
}
public class Component {
    public GameObject gameObject=new GameObject();public bool enabled=true;
    public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
    public static void Destroy(object o) {var g=o as GameObject;if(g!=null)g.destroyed=true;}
}
public class SpriteRenderer:Component {}
public class CameraVisionStreamingExempt:Component {}
public class AudioSource { public void Stop(){} }
public class CardboardBoxWearState {public bool blocks;public bool TryBlockAttack(ZeldaCharacterData a)=>blocks;}
public class ZeldaCharacterAiBase { public int multiplier=1;public int ResolveIncomingAttackDamage(int d)=>d*multiplier;public static void NotifyPlayerAttackWitnesses(ZeldaCharacterData a,ZeldaCharacterData v){} }
public class PlayerGrowthAttributes {public static PlayerGrowthAttributes Instance=new PlayerGrowthAttributes();public void RecordPossessedCharacter(ZeldaCharacterData d){} }
public static class SoulMarkRuntime {public static void ClearAfterDirectPossession(ZeldaFourWayMover m){} }
public class QuestJournalManager { public static QuestJournalManager GetOrCreate()=>new QuestJournalManager();public void RecordControlledBehemoth(ZeldaFourWayMover m){} }
public class ZeldaFourWayMover:Component {
    public ZeldaCharacterData characterData;public CardboardBoxWearState ActiveCardboardBox;
    private Vector2 facingDirection,moveDirection;private float attackTimer;
    public static int ghostSpawns;public bool hasGhostPrefab=true;
    public void InterruptPossessionByDamage(){}public void ReceiveControl(Vector2 d){enabled=true;}
    private void ApplyFacingVisual(){}private void RejectPossessionAttempt(){}
    private bool IsValidControlSwitchTarget(ZeldaFourWayMover t)=>t!=null&&!t.characterData.IsDead;
    private void NotifyAiWitnessesOfPossession(ZeldaFourWayMover t){}
    public bool Switch(ZeldaFourWayMover t)=>TrySwitchControl(t);
    public void Died(){if(enabled&&hasGhostPrefab)ghostSpawns++;}
'@
$code += Extract $mover '    private bool TrySwitchControl('
$code += @'
}
public class ZeldaCharacterData:Component {
    public int currentHealth,health=99;private bool isDead,soulDetonationDeath,silentGhostPossessionDeath;
    private int controlTransferDamage;public bool IsGhostLike;public bool IsDead=>isDead;
    public Action ValuesChanged;private AudioSource damageAudioSource;
    private float damageFeedbackTimer,damageFeedbackDuration=.18f;public string name="test";
    public int deathCount,hitCount;public ZeldaCharacterData lastAttacker;public Action onDeath;
    public ZeldaCharacterData(int hp){currentHealth=hp;gameObject.components.Add(this);var m=new ZeldaFourWayMover{characterData=this,gameObject=gameObject};gameObject.components.Add(m);}
    private void CaptureDamageFeedbackBaseVisual(){}private void PlayDamageSound(){}private void PlayDeathSound(){}private void SpawnDeathExplosion(){}
    private void BroadcastMessage(string n,ZeldaCharacterData d,SendMessageOptions o){
        if(n=="OnCharacterDied"){deathCount++;GetComponent<ZeldaFourWayMover>().Died();if(onDeath!=null)onDeath();}
        else {hitCount++;lastAttacker=d;}
    }
    public void Hit(int d,ZeldaCharacterData a=null)=>ApplyDamage(d,a,false);
    public void Kill()=>Die();
'@
foreach ($sig in @('    private void ApplyDamage(', '    internal void TakeDominoDamage(',
    '    protected virtual bool TryHandleDeathRemains()', '    protected virtual void Die()', '    public void DestroyAfterSuccessfulPossession()',
    '    public void DieFromSoulDetonation()')) { $code += Extract $data $sig }
$code += @'
}
public class DominoSkillRuntime:Component {
    private static readonly Dictionary<ZeldaCharacterData,DominoSkillRuntime> links=new Dictionary<ZeldaCharacterData,DominoSkillRuntime>();
    private readonly List<ZeldaCharacterData> members=new List<ZeldaCharacterData>();
    private readonly List<SpriteRenderer> markers=new List<SpriteRenderer>();
    private readonly List<CameraVisionStreamingExempt> exemptions=new List<CameraVisionStreamingExempt>();
    public static void Link(params ZeldaCharacterData[] people){links.Clear();var g=new DominoSkillRuntime();foreach(var p in people){links[p]=g;g.members.Add(p);g.markers.Add(new SpriteRenderer());g.exemptions.Add(new CameraVisionStreamingExempt());}}
    public static bool Linked(ZeldaCharacterData d)=>links.ContainsKey(d);
'@
foreach ($sig in @('    public static void ShareDamage(', '    public static void RemoveMark(',
    '    private void Dissolve()', '    private void Cleanup()')) { $code += Extract $domino $sig }
$code += @'
}
public static class DominoTests {
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static ZeldaCharacterData Npc(int hp){var d=new ZeldaCharacterData(hp);d.GetComponent<ZeldaFourWayMover>().enabled=false;return d;}
    public static int Run(){
        // Every member takes the hit once, even if an earlier recipient dies or dissolves the group.
        for(int a=1;a<=8;a++)for(int b=1;b<=8;b++)for(int c=1;c<=8;c++)for(int damage=1;damage<=10;damage++){
            var x=Npc(a);var y=Npc(b);var z=Npc(c);DominoSkillRuntime.Link(x,y,z);x.Hit(damage);
            Check(x.currentHealth==Math.Max(0,a-damage)&&y.currentHealth==Math.Max(0,b-damage)&&z.currentHealth==Math.Max(0,c-damage),"Shared fatal/nonfatal damage applied exactly once");
            Check(x.IsDead==(a<=damage)&&y.IsDead==(b<=damage)&&z.IsDead==(c<=damage),"Only zero-health members die");
            Check(x.deathCount<2&&y.deathCount<2&&z.deathCount<2,"No recursive death callbacks");
            int alive=(x.IsDead?0:1)+(y.IsDead?0:1)+(z.IsDead?0:1);
            Check(DominoSkillRuntime.Linked(x)==(!x.IsDead&&alive>=2)&&DominoSkillRuntime.Linked(y)==(!y.IsDead&&alive>=2)&&DominoSkillRuntime.Linked(z)==(!z.IsDead&&alive>=2),"Survivors remain linked; lone survivor cleaned");
        }
        var source=Npc(2);var high=Npc(12);var low=Npc(9);DominoSkillRuntime.Link(source,high,low);
        source.Hit(4);Check(high.currentHealth==8&&low.currentHealth==5,"Overkill shares resolved hit, not extra death damage or source HP");
        high.Hit(2);Check(high.currentHealth==6&&low.currentHealth==3,"Survivors continue sharing after original caster died");
        // Transfer to linked or unrelated bodies: actual production TrySwitchControl path.
        foreach(bool linkedTarget in new[]{false,true})for(int hp=1;hp<=8;hp++)for(int targetHp=1;targetHp<=10;targetHp++){
            source=new ZeldaCharacterData(hp);var target=Npc(targetHp);var peer=Npc(20);var peer2=Npc(20);
            if(linkedTarget)DominoSkillRuntime.Link(source,target,peer);else DominoSkillRuntime.Link(source,peer,peer2);
            int ghosts=ZeldaFourWayMover.ghostSpawns;
            Check(source.GetComponent<ZeldaFourWayMover>().Switch(target.GetComponent<ZeldaFourWayMover>()),"Possession completes");
            Check(source.IsDead&&source.deathCount==1&&peer.currentHealth==20-hp,"Source's remaining HP shared once on transfer");
            Check(target.currentHealth==(linkedTarget?Math.Max(0,targetHp-hp):targetHp),"Only targets in source group receive transfer damage");
            Check(ZeldaFourWayMover.ghostSpawns==ghosts+(linkedTarget&&targetHp<=hp?1:0),"At most one ghost, at newly controlled body's death; none at source");
            source.DestroyAfterSuccessfulPossession();source.Kill();Check(peer.currentHealth==20-hp,"Repeated death or transfer cannot repeat shared damage");
        }
        source=Npc(3);high=Npc(15);low=Npc(11);DominoSkillRuntime.Link(source,high,low);
        source.DieFromSoulDetonation();Check(high.currentHealth==12&&low.currentHealth==8,"Soul-mark transfer uses HP captured before setting zero");
        source.DieFromSoulDetonation();Check(high.currentHealth==12&&low.currentHealth==8,"Repeated soul transfer cannot damage twice");
        source=Npc(3);high=Npc(15);low=Npc(11);DominoSkillRuntime.Link(source,high,low);
        source.onDeath=()=>{source.DestroyAfterSuccessfulPossession();source.DieFromSoulDetonation();};
        source.DestroyAfterSuccessfulPossession();Check(high.currentHealth==12&&low.currentHealth==8&&source.deathCount==1,"Reentrant transfer callbacks do not propagate again");
        source=Npc(3);high=Npc(15);low=Npc(11);DominoSkillRuntime.Link(source,high,low);
        source.Kill();Check(high.currentHealth==15&&low.currentHealth==11,"A non-hit, non-transfer death does not invent damage");
        high.Hit(1);Check(low.currentHealth==10,"Direct removal does not dissolve surviving pair");
        source=Npc(5);high=Npc(20);low=Npc(20);DominoSkillRuntime.Link(source,high,low);
        source.gameObject.components.Add(new ZeldaCharacterAiBase{multiplier=3});
        high.gameObject.components.Add(new ZeldaCharacterAiBase{multiplier=3});source.Hit(2);
        Check(high.currentHealth==14&&low.currentHealth==14,"Resolved sneak damage shared without recipient multiplier");
        source=Npc(5);high=Npc(20);DominoSkillRuntime.Link(source,high);
        source.GetComponent<ZeldaFourWayMover>().ActiveCardboardBox=new CardboardBoxWearState{blocks=true};source.Hit(2);
        Check(high.currentHealth==20&&source.currentHealth==5,"Blocked attacks do not propagate");
        DominoSkillRuntime.ShareDamage(source,0,null);DominoSkillRuntime.ShareDamage(source,-1,null);DominoSkillRuntime.ShareDamage(null,5,null);
        Check(high.currentHealth==20,"Null origin or invalid damage ignored");
        return checks;
    }
}
'@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$count = [DominoTests]::Run()
if ($domino -match 'ShareDeath|DieFromDomino|TryPossess' -or $mover -match 'DominoSkillRuntime.TryPossess') { throw 'Old forced-death path remains' }
if ($data.IndexOf('isDead = true;', $data.IndexOf('protected virtual void Die()')) -gt $data.IndexOf('if (controlTransferDamage > 0)')) { throw 'Transfer propagation must be reentry-guarded' }
Write-Output "PASS: $count production domino damage/death/possession assertions; obsolete forced-death paths absent."
