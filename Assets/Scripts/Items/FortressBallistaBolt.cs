using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Swept explosive bolt with a character-only proximity fuse.</summary>
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(CameraVisionStreamingRegion))]
public sealed class FortressBallistaBolt : MonoBehaviour
{
    public const int AttackPower = 8;
    public const float Lifetime = 4f;
    public const float ExplosionWidth = 1.6f; // Normal bomb: 2.4 x 2.4 world units.
    public const float ContactRadius = .065f;
    private const float Radius = ContactRadius;
    // Put a swept proximity contact just inside the blast, not only tangent to its trigger.
    private const float ProximityContactInset = .005f;
    private const float HitboxDuration = .12f;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
    private ContactFilter2D filter;
    private Transform launcher;
    private ZeldaCharacterData owner;
    private CameraVisionStreamingRegion retention;
    private Vector2 direction;
    private float speed, elapsed, retentionTimer;
    private bool finished;
    private Mesh arrowMesh;
    private Material arrowMaterial;

    public static bool Fire(FortressBallista source, ZeldaCharacterData user, Vector3 muzzle, Vector2 facing, float velocity)
    {
        if (source == null || user == null || user.IsDead || user.IsGhostLike || facing.sqrMagnitude < .001f ||
            float.IsNaN(velocity) || float.IsInfinity(velocity) || velocity <= 0f) return false;
        Scene scene=source.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded || ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject) != scene) return false;
        var root=new GameObject("Ballista Explosive Bolt");
        SceneManager.MoveGameObjectToScene(root,scene);
        root.transform.position=muzzle;
        root.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg-90f);
        var bolt=root.AddComponent<FortressBallistaBolt>();
        bolt.launcher=source.transform;bolt.owner=user;bolt.direction=facing.normalized;bolt.speed=velocity;
        bolt.filter=new ContactFilter2D {useTriggers=true};bolt.filter.SetLayerMask(Physics2D.AllLayers);
        bolt.ConfigureVisual();
        bolt.retention=root.GetComponent<CameraVisionStreamingRegion>();
        bolt.RefreshRetention();
        // Check both contact and character proximity at the muzzle before the first flight step.
        bolt.MoveSwept(Vector2.zero);
        return true;
    }

    private void ConfigureVisual()
    {
        // Arrow tip is at the swept origin; shaft and feathers trail behind it.
        arrowMesh=new Mesh {name="Geometric Ballista Bolt"};
        arrowMesh.vertices=new[] {
            new Vector3(-.027f,-.90f),new Vector3(.027f,-.90f),new Vector3(.027f,-.15f),new Vector3(-.027f,-.15f),
            new Vector3(-.115f,-.24f),new Vector3(.115f,-.24f),new Vector3(0,0),
            new Vector3(-.095f,-.94f),new Vector3(0,-.87f),new Vector3(0,-.70f),new Vector3(-.095f,-.79f),
            new Vector3(0,-.87f),new Vector3(.095f,-.94f),new Vector3(.095f,-.79f),new Vector3(0,-.70f)};
        arrowMesh.triangles=new[] {0,2,1,0,3,2,4,6,5,7,9,8,7,10,9,11,13,12,11,14,13};
        var colors=new Color32[15];
        for(int i=0;i<colors.Length;i++) colors[i]=i<4 ? new Color(.70f,.43f,.23f) : i<7 ? new Color(.88f,.91f,.91f) : new Color(.52f,.57f,.58f);
        arrowMesh.colors32=colors;arrowMesh.uv=new Vector2[15];arrowMesh.RecalculateBounds();
        arrowMaterial=new Material(Shader.Find("Sprites/Default"));
        GetComponent<MeshFilter>().sharedMesh=arrowMesh;
        var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=arrowMaterial;
        renderer.sortingOrder=2;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    private void RefreshRetention()
    {
        // Keep a short flight corridor and the future blast targets awake, including
        // colliders already suspended by distance loading, before doing any queries.
        retention.Configure(speed*.2f+ExplosionWidth*.71f+.2f);
        retentionTimer=.1f;
    }
    private void FixedUpdate()
    {
        if (finished) return;
        float step=Mathf.Min(Time.fixedDeltaTime,Lifetime-elapsed);
        if (step<=0f) { Explode(); return; }
        retentionTimer-=step;if(retentionTimer<=0f) RefreshRetention();
        if (!MoveSwept(direction*(speed*step))) return;
        elapsed+=step;
        if(elapsed>=Lifetime) Explode();
    }

    private bool MoveSwept(Vector2 displacement)
    {
        if (finished) return false;
        Physics2D.SyncTransforms();
        Vector2 start=transform.position;
        overlaps.Clear();Physics2D.OverlapCircle(start,Radius,filter,overlaps);
        for(int i=0;i<overlaps.Count;i++)
            if(CanHit(overlaps[i])) { Explode();return false; }

        // Match the actual world-axis-aligned 1.6 x 1.6 explosion hitbox, including corners.
        // A nearby prop/wall is NOT a fuse target; it still requires the original direct contact.
        overlaps.Clear();Physics2D.OverlapBox(start,Vector2.one*ExplosionWidth,0f,filter,overlaps);
        for(int i=0;i<overlaps.Count;i++)
            if(CanTriggerProximity(overlaps[i])) { Explode();return false; }

        float distance=displacement.magnitude;
        if(distance<=.00001f) return true;
        hits.Clear();Physics2D.CircleCast(start,Radius,displacement/distance,filter,hits,distance);
        float nearestContact=float.PositiveInfinity;
        for(int i=0;i<hits.Count;i++)
            if(CanHit(hits[i].collider) && hits[i].distance<nearestContact)
                nearestContact=hits[i].distance;

        // Sweep the fuse volume as well, so fast bolts cannot skip characters between ticks.
        hits.Clear();Physics2D.BoxCast(start,Vector2.one*ExplosionWidth,0f,displacement/distance,filter,hits,distance);
        float nearestProximity=float.PositiveInfinity;
        for(int i=0;i<hits.Count;i++)
            if(CanTriggerProximity(hits[i].collider) && hits[i].distance<nearestProximity)
                nearestProximity=hits[i].distance;
        float nearestDistance=Mathf.Min(nearestContact,nearestProximity);
        if(!float.IsPositiveInfinity(nearestDistance))
        {
            // Never step past an earlier wall contact while resolving the proximity overlap.
            if(nearestProximity<=nearestContact)
                nearestDistance=Mathf.Min(distance,Mathf.Min(nearestContact,nearestProximity+ProximityContactInset));
            transform.position+=(Vector3)(displacement/distance*nearestDistance);
            Explode();return false;
        }
        transform.position+=(Vector3)displacement;return true;
    }

    private bool CanTriggerProximity(Collider2D collider)
    {
        if(collider==null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            (launcher!=null && collider.transform.IsChildOf(launcher)) ||
            ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject)!=gameObject.scene) return false;
        var character=collider.GetComponentInParent<ZeldaCharacterData>();
        // Same damage eligibility as the blast: corpses and ghosts do not set off the fuse.
        // Do not exclude the firing body or friendly characters: both can take blast damage.
        return character!=null && !character.IsDead && character.CanTakeAttackDamage;
    }

    private bool CanHit(Collider2D collider)
        => CanProjectileHit(collider, launcher, gameObject.scene);

    // Shared contact rules/footprint for siege bolts and cannonballs; not the proximity fuse.
    public static bool CanProjectileHit(Collider2D collider, Transform launcher, Scene scene)
    {
        if(collider==null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            (launcher!=null && collider.transform.IsChildOf(launcher)) ||
            ZeldaRuntimeRegistry.GetGameplayScene(collider.gameObject)!=scene) return false;
        // Non-physical permission/interaction zones do not stop arrows. Characters,
        // breakable items and explicit block-layer triggers still count as contacts.
        if(!collider.isTrigger) return true;
        int layer=collider.gameObject.layer;
        return layer==LayerMask.NameToLayer("Blocks") || layer==LayerMask.NameToLayer("Hidden Blocks") ||
            collider.GetComponentInParent<ZeldaCharacterData>()!=null || collider.GetComponentInParent<DoorData>()!=null ||
            collider.GetComponentInParent<CardboardBoxPickupItem>()!=null || collider.GetComponentInParent<BatteryPickupItem>()!=null ||
            collider.GetComponentInParent<BatterySocket>()!=null || collider.GetComponentInParent<BombPickupItem>()!=null ||
            collider.GetComponentInParent<PlacedBomb>()!=null || collider.GetComponentInParent<StabilityCrystalPickupItem>()!=null ||
            collider.GetComponentInParent<PotionPickupItem>()!=null ||
            collider.GetComponentInParent<DollAttackShake>()!=null;
    }

    private void Explode()
    {
        if(finished) return;
        finished=true; // Latch before damage callbacks and chained explosions.
        Vector3 position=transform.position;
        Color grayWhite=new Color(.80f,.81f,.82f,.95f);
        var blast=new GameObject("Ballista Explosion Hitbox",typeof(SpriteRenderer),typeof(BoxCollider2D));
        SceneManager.MoveGameObjectToScene(blast,gameObject.scene);blast.transform.position=position;
        blast.AddComponent<CameraVisionStreamingRegion>().Configure(ExplosionWidth*.71f+.2f);
        var hitbox=blast.AddComponent<ZeldaAttackHitbox>();
        hitbox.Configure(HitboxDuration,Vector2.one*ExplosionWidth,grayWhite,AttackPower,owner,ZeldaAttackVisualShape.Box,true);
        blast.GetComponent<SpriteRenderer>().enabled=false;
        var effect=new GameObject("Ballista Gray White Explosion");
        SceneManager.MoveGameObjectToScene(effect,gameObject.scene);effect.transform.position=position;
        effect.AddComponent<CameraVisionStreamingExempt>();
        effect.AddComponent<BombExplosionVisual>().Configure(Vector2.one*ExplosionWidth,.35f,grayWhite,0,4,8);
        if(retention!=null) retention.enabled=false;
        GetComponent<MeshRenderer>().enabled=false;
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if(arrowMesh!=null) Destroy(arrowMesh);
        if(arrowMaterial!=null) Destroy(arrowMaterial);
    }
}
