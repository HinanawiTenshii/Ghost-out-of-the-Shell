using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Interrupted breach: swallow, internal blast, gravity fall, exposed stun and harmless burial.</summary>
public sealed partial class SandwormEruptionAttack
{
    [Header("Swallowed Explosives / 吞入爆炸物")]
    [SerializeField, Min(0f), Tooltip("坠地后完整露出、停止移动并显示眩晕图标的秒数。")]
    private float swallowedStunDuration = 5f;
    [SerializeField, Min(.1f), Tooltip("从空中坠落至出入土点水平线的时间。")]
    private float fallDuration = .8f;
    [SerializeField, Range(15f, 75f), Tooltip("坠落末端的弯曲角度：身体中部朝下，头尾上翘。")]
    private float fallBendAngle = 55f;
    [SerializeField, Tooltip("体内爆炸按吞入物原爆炸伤害之和的两倍扣血，只伤害沙虫，不产生地面连锁爆炸。")]
    private bool internalBlastDealsDamage = true;

    private const float ImpactSettleDuration = .55f;

    public int SwallowedExplosiveCount { get; private set; }
    public int SwallowedAttackPower { get; private set; }
    public int RecoveryBurialPulses { get; private set; }
    public int GroundImpactPulses { get; private set; }
    public bool IsExplosiveRecovery => Phase == AttackPhase.Falling || Phase == AttackPhase.Stunned ||
        Phase == AttackPhase.ReturningToBurrow;
    public float StunRemaining => Phase == AttackPhase.RoarStunned ? Mathf.Max(0f, roarStunDuration-elapsed) :
        Phase == AttackPhase.Stunned ? Mathf.Max(0f, swallowedStunDuration-elapsed) : 0f;
    public float SwallowedStunDuration => swallowedStunDuration;
    public Vector2 InternalBlastPoint { get; private set; }
    public Vector2 FallGroundPoint { get; private set; }
    private float InternalBlastDistance => (arcLength + spacing*(parts.Length-1))*.5f;
    private readonly List<Collider2D> swallowOverlaps = new List<Collider2D>(32);
    private readonly HashSet<IConsumableExplosive> swallowCandidates = new HashSet<IConsumableExplosive>();
    private readonly List<Vector2> recoveryPath = new List<Vector2>(512);
    private readonly List<SandwormEruptionSite> impactSites = new List<SandwormEruptionSite>(3);
    private Vector2[] fallingFrom, fallingTo, fallingBent, fallPose, returnPoseOffsets;
    private Quaternion[] fallingRotations;
    private Quaternion[] returnPoseRotations;
    private float recoveryBurrowDistance, impactAge, returnPoseBlendDuration, returnPoseBlendAge;
    private ZeldaAiStateIndicator stunIndicator;

    private void ResetSwallowedExplosives()
    {
        ReleaseRecoveryVisuals();
        SwallowedExplosiveCount = SwallowedAttackPower = RecoveryBurialPulses = GroundImpactPulses = 0;
        InternalBlastPoint = FallGroundPoint = Vector2.zero;
        fallingFrom = fallingTo = fallingBent = fallPose = returnPoseOffsets = null;
        fallingRotations = returnPoseRotations = null;
        impactAge = returnPoseBlendDuration = returnPoseBlendAge = 0f;
        recoveryPath.Clear();
    }

    private void SwallowExplosivesAtEmergence()
    {
        Physics2D.SyncTransforms();
        swallowCandidates.Clear(); swallowOverlaps.Clear();
        Physics2D.OverlapCircle(LockedAttackPoint, DamageRadius,
            new ContactFilter2D { useTriggers = true }, swallowOverlaps);
        foreach (var collider in swallowOverlaps)
        {
            if (collider == null || !collider.gameObject.activeInHierarchy ||
                ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject) != gameObject.scene) continue;
            var explosive = collider.GetComponentInParent<IConsumableExplosive>();
            if (explosive == null || !swallowCandidates.Add(explosive) ||
                !explosive.TryConsumeExplosive(out int power)) continue;
            SwallowedExplosiveCount++;
            SwallowedAttackPower += Mathf.Max(0, power);
        }
        if (SwallowedExplosiveCount > 0 && recoveryPath.Count == 0) PrepareExplosiveRecovery();
    }

    private void PrepareExplosiveRecovery()
    {
        // Prepare while the tail is still emerging, well before the air burst.
        // Mid-body reaches the arc apex with EVERY section already above ground.
        float middle = (parts.Length-1)*.5f;
        Vector2 center = Sample(arcLength*.5f);
        float side = Mathf.Sign(LandingPoint.x-LockedAttackPoint.x);
        FallGroundPoint = new Vector2(center.x, LockedAttackPoint.y);
        fallingTo = new Vector2[parts.Length];
        fallingBent = new Vector2[parts.Length]; fallPose = new Vector2[parts.Length];
        // Equal arc-distance samples preserve segment spacing while the body
        // curls into a U; directly projecting a parabola would stretch the joints.
        float bend = Mathf.Clamp(fallBendAngle,15f,75f)*Mathf.Deg2Rad;
        float radius = Mathf.Max(spacing,middle*spacing)/bend;
        float nearestMiddle = parts.Length%2==0 ? spacing*.5f : 0f;
        float centerLift = radius*(1f-Mathf.Cos(nearestMiddle/radius));
        for (int i=0;i<parts.Length;i++)
        {
            fallingTo[i] = FallGroundPoint + Vector2.right*(side*(middle-i)*spacing);
            float angle = (middle-i)*spacing/radius;
            fallingBent[i] = FallGroundPoint + new Vector2(side*radius*Mathf.Sin(angle),
                radius*(1f-Mathf.Cos(angle))-centerLift);
        }

        // Flatten into a full-length body, never stack all projected segments on
        // top of each other. A shallow curve then turns the head down into the
        // original entrance, matching the existing buried departure's tangent.
        Vector2 start = fallingTo[0];
        float run = Vector2.Distance(start, LandingPoint);
        Vector2 p1 = start + Vector2.right*(side*run*.55f);
        Vector2 p2 = LandingPoint + Vector2.up*Mathf.Min(spacing*.7f,run*.5f);
        recoveryPath.Clear(); recoveryBurrowDistance = 0f;
        for (int i=0;i<=48;i++)
        {
            float t=i/48f, u=1f-t;
            Vector2 point=u*u*u*start+3f*u*u*t*p1+3f*u*t*t*p2+t*t*t*LandingPoint;
            if (i>0) recoveryBurrowDistance += Vector2.Distance(recoveryPath[i-1],point);
            recoveryPath.Add(point);
        }
        for (int i=1;i<departure.Count;i++) recoveryPath.Add(departure[i]);
        // Reuse the already validated departure. Its remaining length is identical
        // to a normal breach, so the next roaming route can be searched in advance.
        movement.PrepareRoamingContinuation(recoveryPath, recoveryBurrowDistance+spacing*(parts.Length-1)+.01f);
    }

    private void BeginExplosiveFall()
    {
        if (recoveryPath.Count < 2) PrepareExplosiveRecovery();
        float middle=(parts.Length-1)*.5f;
        InternalBlastPoint=Sample(distance-middle*spacing);
        fallingFrom=new Vector2[parts.Length]; fallingRotations=new Quaternion[parts.Length];
        for(int i=0;i<parts.Length;i++)
        { fallingFrom[i]=parts[i].position; fallingRotations[i]=parts[i].rotation; }
        Phase=AttackPhase.Falling; elapsed=0f;
        if (emergenceSite != null) emergenceSite.Finish();
        if (landingSite != null) landingSite.Release();
        landingSite=SandwormEruptionSite.Create(gameObject.scene,LandingPoint,DamageRadius*.6f,false);
        landingSite.gameObject.name="Sandworm Harmless Recovery Burrow";
        CreateInternalBlast();
        if (internalBlastDealsDamage && SwallowedAttackPower>0)
            character.TakeDamage((int)System.Math.Min(int.MaxValue,(long)SwallowedAttackPower*2L));
        if (character.IsDead) CancelAttack();
    }

    private void CreateInternalBlast()
    {
        var root=new GameObject("Sandworm Internal Explosion");
        SceneManager.MoveGameObjectToScene(root,gameObject.scene);
        root.transform.position=InternalBlastPoint;
        root.AddComponent<CameraVisionStreamingExempt>();
        root.AddComponent<BombExplosionVisual>().Configure(Vector2.one*DamageRadius*2.5f,.65f,
            new Color(1f,.3f,.08f,1f),0,16,16);
        // The dedicated blast/tumble replaces (rather than doubles) the old bomb sound.
        var sound=GetComponent<SandwormAudio>();
        if (sound != null && sound.PlayBlastFall(InternalBlastPoint)) return;
        var clip=Resources.Load<AudioClip>("Audio/BombExplosion");
        if (clip != null)
        {
            var audioRoot=new GameObject("Sandworm Internal Explosion Audio");
            SceneManager.MoveGameObjectToScene(audioRoot,gameObject.scene);
            audioRoot.transform.position=InternalBlastPoint;
            audioRoot.AddComponent<CameraVisionStreamingExempt>();
            var source=audioRoot.AddComponent<AudioSource>();
            source.playOnAwake=false; source.clip=clip; source.spatialBlend=.85f;
            source.rolloffMode=AudioRolloffMode.Logarithmic; source.minDistance=1f; source.maxDistance=30f;
            source.dopplerLevel=0f; source.Play(); Destroy(audioRoot,clip.length+.1f);
        }
    }

    private void AdvanceExplosiveRecovery(float deltaTime)
    {
        movement.AdvancePreparedRoaming(deltaTime);
        // Carry leftover time across transitions: only the authored stun stops motion.
        float remaining=deltaTime;
        if (Phase == AttackPhase.Falling)
        {
            float step=Mathf.Min(remaining,Mathf.Max(0f,fallDuration-elapsed));
            elapsed+=step; remaining-=step;
            float t=Mathf.Clamp01(elapsed/fallDuration);
            float bend=Mathf.SmoothStep(0f,1f,t/.75f);
            float height=(InternalBlastPoint.y-FallGroundPoint.y)*(1f-t*t);
            for(int i=0;i<parts.Length;i++)
            {
                fallPose[i]=new Vector2(Mathf.Lerp(fallingFrom[i].x,fallingBent[i].x,bend),
                    FallGroundPoint.y+height+Mathf.Lerp(fallingFrom[i].y-InternalBlastPoint.y,
                        fallingBent[i].y-FallGroundPoint.y,bend));
                states.SetSegmentExposed(visuals[i],true);
            }
            ApplyArticulatedFallPose(Mathf.SmoothStep(0f,1f,t/.35f));
            if (t<1f) return;
            states.EnterAboveGround(); states.EndSegmentedPassage();
            CreateFallImpact();
            Phase=AttackPhase.Stunned; elapsed=0f;
            ShowStunIndicator(true);
        }
        if (Phase == AttackPhase.Stunned)
        {
            float step=Mathf.Min(remaining,Mathf.Max(0f,swallowedStunDuration-elapsed));
            elapsed+=step; remaining-=step;
            impactAge+=step;
            ApplyGroundImpactPose();
            ClearGroundedCharacterOverlaps();
            EmitDelayedGroundImpacts();
            PositionStunIndicator();
            if (elapsed<swallowedStunDuration) return;
            BeginRecoveryReturn();
        }
        if (Phase != AttackPhase.ReturningToBurrow || remaining<=0f) return;
        impactAge+=remaining; returnPoseBlendAge+=remaining;
        EmitDelayedGroundImpacts();
        float end=arcLength+spacing*(parts.Length-1)+.01f;
        float requested=distance+movement.WorldMoveSpeed*remaining;
        distance=Mathf.Min(end,requested);
        for(int i=0;i<parts.Length;i++)
        {
            if (RecoveryBurialPulses==i && distance>=arcLength+i*spacing)
            { RecoveryBurialPulses++; landingSite.Pulse(false); }
        }
        ApplyPassagePose();
        ClearGroundedCharacterOverlaps();
        if (distance>=end)
        {
            FinishPassage();
            if (Phase == AttackPhase.Settling)
                movement.Advance(Mathf.Max(0f,requested-end)/movement.WorldMoveSpeed);
        }
    }

    private void BeginRecoveryReturn()
    {
        ShowStunIndicator(false);
        if (!states.BeginSegmentedPassage(true)) { CancelAttack(); return; }
        // Very short/zero stuns must not snap the still-curled body into its
        // travel pose. Finish settling during movement, not as an extra pause.
        returnPoseBlendDuration=Mathf.Max(0f,ImpactSettleDuration-impactAge);
        returnPoseBlendAge=0f;
        if (returnPoseBlendDuration>0f)
        {
            returnPoseOffsets=new Vector2[parts.Length]; returnPoseRotations=new Quaternion[parts.Length];
            for(int i=0;i<parts.Length;i++)
            { returnPoseOffsets[i]=(Vector2)parts[i].position-fallingTo[i]; returnPoseRotations[i]=parts[i].rotation; }
        }
        Phase=AttackPhase.ReturningToBurrow; elapsed=distance=0f;
        history.Clear();
        history.AddRange(fallingTo);
        Vector2 backward=(fallingTo[parts.Length-1]-fallingTo[0]).normalized;
        history.Add(fallingTo[parts.Length-1]+backward*spacing*2f);
        passage.Clear(); lengths.Clear();
        foreach(var point in recoveryPath) AddPassagePoint(point);
        arcLength=recoveryBurrowDistance;
        ApplyPassagePose();
        ClearGroundedCharacterOverlaps();
    }

    private void CreateFallImpact()
    {
        GetComponent<SandwormAudio>()?.PlayGroundImpact(FallGroundPoint);
        impactAge=0f; GroundImpactPulses=0;
        // The low middle touches first; head/tail dust is emitted only when
        // those ends subsequently slap down. No warning/damage or blast flash.
        EmitGroundImpact(FallGroundPoint,.8f);
    }

    private void EmitGroundImpact(Vector2 point,float radiusScale)
    {
        var site=SandwormEruptionSite.Create(gameObject.scene,point,DamageRadius*radiusScale,false,false);
        site.gameObject.name="Sandworm Body Landing Dust";
        site.Pulse(true); site.Finish(); impactSites.Add(site); GroundImpactPulses++;
    }

    private float GroundContactTime(int index)
    {
        float fraction=index/(float)Mathf.Max(1,parts.Length-1);
        return .24f+.06f*Mathf.Abs(fraction*2f-1f)+.045f*fraction;
    }

    private void EmitDelayedGroundImpacts()
    {
        if (GroundImpactPulses==1 && impactAge>=GroundContactTime(0)) EmitGroundImpact(fallingTo[0],.5f);
        if (GroundImpactPulses==2 && impactAge>=GroundContactTime(parts.Length-1)) EmitGroundImpact(fallingTo[parts.Length-1],.5f);
    }

    private void ApplyGroundImpactPose()
    {
        for(int i=0;i<parts.Length;i++)
        {
            float contact=GroundContactTime(i);
            float fraction=i/(float)Mathf.Max(1,parts.Length-1);
            float endWeight=Mathf.Abs(fraction*2f-1f);
            float drop=Mathf.Clamp01((impactAge-.035f*endWeight)/(contact-.035f*endWeight));
            float bounce=Mathf.Clamp01((impactAge-contact)/(ImpactSettleDuration-contact));
            float lift=spacing*.12f*endWeight*Mathf.Sin(Mathf.PI*bounce)*(1f-bounce)*(1f-bounce);
            fallPose[i]=new Vector2(Mathf.Lerp(fallingBent[i].x,fallingTo[i].x,Mathf.SmoothStep(0f,1f,drop)),
                Mathf.Lerp(fallingBent[i].y,fallingTo[i].y,drop*drop)+lift);
        }
        ApplyArticulatedFallPose(1f);
    }

    private void ApplyArticulatedFallPose(float rotationBlend)
    {
        for(int i=0;i<parts.Length;i++)
        {
            Vector2 tangent=fallPose[Mathf.Max(0,i-1)]-fallPose[Mathf.Min(parts.Length-1,i+1)];
            if (parts[i].localScale.x<0f) tangent=-tangent;
            var rotation=Quaternion.Euler(0,0,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);
            parts[i].position=new Vector3(fallPose[i].x,fallPose[i].y,parts[i].position.z);
            parts[i].rotation=Quaternion.Slerp(fallingRotations[i],rotation,rotationBlend);
        }
    }

    private void BlendResidualLandingPose()
    {
        if (returnPoseBlendDuration<=0f || returnPoseOffsets==null) return;
        float t=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(returnPoseBlendAge/returnPoseBlendDuration));
        for(int i=0;i<parts.Length;i++)
        {
            parts[i].position+=(Vector3)(returnPoseOffsets[i]*(1f-t));
            parts[i].rotation=Quaternion.Slerp(returnPoseRotations[i],parts[i].rotation,t);
        }
        if (t>=1f) returnPoseBlendDuration=0f;
    }

    private void ShowStunIndicator(bool visible)
    {
        if (visible && stunIndicator == null)
        {
            var root=new GameObject("Sandworm Stun Indicator");
            root.transform.SetParent(transform,false);
            stunIndicator=root.AddComponent<ZeldaAiStateIndicator>();
            root.transform.localScale=Vector3.one*.8f;
        }
        if (stunIndicator == null) return;
        PositionStunIndicator();
        stunIndicator.SetState(visible?ZeldaAiState.Stunned:ZeldaAiState.Idle,0f,0f,false);
    }

    private void PositionStunIndicator()
    {
        if (stunIndicator == null) return;
        stunIndicator.transform.position=parts[0].position+Vector3.up*(DamageRadius+.4f*Scale);
        stunIndicator.transform.rotation=Quaternion.identity;
    }

    private void AdvanceImpactVisuals(float deltaTime)
    {
        for(int i=impactSites.Count-1;i>=0;i--)
        {
            var site=impactSites[i];
            if (site != null) site.AdvanceVisuals(deltaTime);
            if (site == null || site.IsFinished)
            { if (site != null) site.Release(); impactSites.RemoveAt(i); }
        }
    }

    private void ReleaseRecoveryVisuals()
    {
        foreach(var site in impactSites) if (site != null) site.Release();
        impactSites.Clear();
        if (stunIndicator != null)
        {
            stunIndicator.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(stunIndicator.gameObject); else DestroyImmediate(stunIndicator.gameObject);
            stunIndicator=null;
        }
    }
}
