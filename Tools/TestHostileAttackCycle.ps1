$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
$hitbox=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
$mover=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaFourWayMover.cs"
function Extract($source,$signature){
    $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
    while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    return $source.Substring($start,$end-$start)
}
# Exercise the production state machine and feedback code, with only Unity navigation/physics stubbed.
$stub=@'
using System;
public class Object {}
public struct Vector2 {
    public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
    public static Vector2 zero => new Vector2(0,0);
    public float magnitude => (float)Math.Sqrt(x*x+y*y);
    public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
    public static float Angle(Vector2 a,Vector2 b)=>0;
}
public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
public class ZeldaCharacterData:Object {public bool CanAttack=true;public float AttackDuration=.25f;}
public class ZeldaFourWayMover:Object {
    public ZeldaCharacterData Data=new ZeldaCharacterData();public Body transform=new Body();
    public T GetComponent<T>() where T:class => Data as T;
}
public class Body {public Vector2 position;}
public class CardboardBoxPickupItem:Object {public bool IsDestroyed;public bool isActiveAndEnabled=true;public Vector2 WorldCenter;}
public enum ZeldaAiState {Hostile,Idle}
public class FakeMover {
    public int swings;public ZeldaAttackHitbox box;
    public ZeldaAttackHitbox.AttackOutcome LastAttackOutcome;
    public bool TryPerformAttack(Vector2 direction,Object target){
        swings++;box=new ZeldaAttackHitbox();LastAttackOutcome=box.ObserveTarget(target);return true;
    }
}
public class ZeldaAttackHitbox {
    private AttackOutcome observedOutcome;
    public void Contact(Object target){RecordTargetContact(target);}
    public void End(){OnDisable();}
'@
foreach($sig in @('    public sealed class AttackOutcome','    public AttackOutcome ObserveTarget(', '    private void RecordTargetContact(', '    private void OnDisable()')){$stub+=Extract $hitbox $sig}
$stub+=@'
}
public class Probe {
    private Object attackCycleTarget; private ZeldaAttackHitbox.AttackOutcome pendingAttackOutcome;
    private bool attackChainConnected,pursueAfterMiss;private float missedAttackPursuitTimer;
    private float attackDistance=1.1f,hostileAttackStartRangeMultiplier=1.4f,missedAttackPursuitDuration=.22f;
    private float attackCooldown=.35f,attackCooldownTimer,attackFacingTolerance=20,gridWaypointTolerance=.15f;
    private Vector2 facingDirection,moveDirection;private Body rb=new Body();
    private ZeldaCharacterData characterData=new ZeldaCharacterData();
    private ZeldaFourWayMover targetMover=new ZeldaFourWayMover();
    private CardboardBoxPickupItem hostileCardboardBox;
    private ZeldaAiState currentState=ZeldaAiState.Hostile;
    private FakeMover mover=new FakeMover();
    private void SetFacingDirection(Vector2 v){facingDirection=v;}
    private void InvalidateNavigationPlan(){}
    private Vector2 PlanDirectionTo(Vector2 v,float tolerance)=>v-rb.position;
    private Vector2 ResolveSharedFollowPosition(Object o,Vector2 v,float r)=>v;
    private void Tick(float dt){attackCooldownTimer=Math.Max(0,attackCooldownTimer-dt);TickHostile(dt);}
    private static int checks;
    private static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static int Run(){
        var p=new Probe();p.targetMover.transform.position=new Vector2(1.6f,0);p.Tick(.01f);
        Check(p.mover.swings==0 && p.moveDirection.magnitude>0,"Chase beyond initiation range");
        p.targetMover.transform.position=new Vector2(1.4f,0);p.Tick(.01f);
        Check(p.mover.swings==1,"Start beyond old 1.1 radius, within new 1.54 radius");
        p.mover.box.Contact(new ZeldaCharacterData());p.Tick(.5f);
        Check(p.mover.swings==1 && !p.attackChainConnected,"Unfinished hitbox cannot resolve early");
        p.mover.box.End();p.Tick(.01f);
        Check(p.pursueAfterMiss && !p.attackChainConnected,"Incidental target contact is not intended-target hit");
        Check(p.moveDirection.magnitude>0 && p.mover.swings==1,"Miss chases even inside starting range");
        p.Tick(.1f);Check(p.mover.swings==1,"Minimum pursuit before retry");
        p.Tick(.13f);Check(p.mover.swings==2,"Retry after pursuit");
        p.mover.box.End();p.Tick(.35f);p.Tick(.1f);
        Check(p.mover.swings==2 && p.moveDirection.magnitude>0,"Consecutive miss repeats pursuit");
        p.Tick(.13f);Check(p.mover.swings==3,"Consecutive miss retries again");
        p.mover.box.Contact(p.targetMover.Data);p.mover.box.End();p.Tick(.25f);
        Check(p.attackChainConnected && !p.pursueAfterMiss && p.moveDirection.magnitude==0,"Hit holds position");
        Check(p.mover.swings==3,"Successful combo respects attack cooldown");
        p.Tick(.11f);Check(p.mover.swings==4,"Hit continues attacking");
        p.mover.box.Contact(p.targetMover.Data);p.mover.box.End();p.targetMover.transform.position=new Vector2(4,0);
        p.Tick(.36f);Check(p.mover.swings==5,"Successful chain attempts next swing even when target leaves");
        p.mover.box.End();p.Tick(.36f);
        Check(!p.attackChainConnected && p.pursueAfterMiss && p.moveDirection.magnitude>0,"Escaped target whiff resumes pursuit");
        p.Tick(.4f);Check(p.mover.swings==5,"No repeated long-distance swings");
        p.targetMover=new ZeldaFourWayMover();p.targetMover.transform.position=new Vector2(1,0);p.Tick(.01f);
        Check(p.mover.swings==6 && !p.pursueAfterMiss,"New target clears old retry state");
        var oldBox=p.mover.box;p.ResetHostileAttackCycle();oldBox.Contact(p.targetMover.Data);oldBox.End();
        p.AdvanceHostileAttackCycle(.3f);
        Check(!p.attackChainConnected && p.pendingAttackOutcome==null,"Late result cannot revive reset cycle");
        var boxProbe=new Probe();boxProbe.hostileCardboardBox=new CardboardBoxPickupItem{WorldCenter=new Vector2(1,0)};
        boxProbe.Tick(.01f);boxProbe.mover.box.Contact(boxProbe.hostileCardboardBox);boxProbe.mover.box.End();boxProbe.Tick(.26f);
        Check(boxProbe.attackChainConnected,"Cardboard target contact can sustain chain");
        boxProbe.hostileCardboardBox.IsDestroyed=true;boxProbe.Tick(.1f);
        Check(boxProbe.pendingAttackOutcome==null && !boxProbe.attackChainConnected,"Destroyed box resets cycle");
        var slow=new Probe();slow.characterData.AttackDuration=1;slow.targetMover.transform.position=new Vector2(1,0);
        slow.Tick(.01f);slow.mover.box.End();slow.Tick(.1f);slow.Tick(.23f);
        Check(slow.mover.swings==1 && slow.moveDirection.magnitude>0,"Pursuit delay never bypasses longer attack cooldown");
        return checks;
    }
'@
foreach($sig in @('    protected virtual void TickHostile(', '    private bool AdvanceHostileAttackCycle(', '    private void ResetHostileAttackCycle()', '    protected virtual void TryAttackTarget(')){$stub+=Extract $ai $sig}
$stub+='}'
Add-Type -TypeDefinition $stub
$count=[Probe]::Run()
if(!$mover.Contains('LastAttackOutcome = hitbox.ObserveTarget(observedTarget);')){throw 'Mover feedback not wired'}
if(!$hitbox.Contains('RecordTargetContact(target);') -or !$hitbox.Contains('RecordTargetContact(cardboardBox);')){throw 'Contact feedback not wired'}
foreach($sig in @('    protected virtual void OnEnable()', '    protected virtual void OnDisable()', '    protected virtual void ChangeState(', '    public virtual void ApplySaveState(')){
    if(!(Extract $ai $sig).Contains('ResetHostileAttackCycle();')){throw "Missing reset: $sig"}
}
Write-Output "PASS: $count production hostile-cycle scenarios, contact wiring and lifecycle resets. Physics/nav are stubbed; Play Mode is still required."
