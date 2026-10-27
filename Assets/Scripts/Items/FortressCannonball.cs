using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Contact-only cannonball, sharing the ballista's swept contact footprint.</summary>
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(CameraVisionStreamingRegion))]
public sealed class FortressCannonball : MonoBehaviour
{
    public const int AttackPower=7;
    public const float Lifetime=5f, ExplosionRadius=3f;
    private readonly List<RaycastHit2D> hits=new List<RaycastHit2D>(16);
    private readonly List<Collider2D> overlaps=new List<Collider2D>(16);
    private readonly List<ZeldaCharacterAiBase> investigationTargets=new List<ZeldaCharacterAiBase>();
    private ContactFilter2D filter;
    private Transform launcher;
    private ZeldaCharacterData owner;
    private CameraVisionStreamingRegion retention;
    private Vector2 direction;
    private float speed, elapsed, retentionTimer;
    private float investigationRadius=7f, pulseDuration=.7f;
    private Color pulseColor=Color.white;
    private bool finished;
    private Mesh mesh;
    private Material material;
    private static BombPickupItem defaultBomb;
    private static AudioClip explosionSound;

    private void OnEnable() => AutoSaveTransientActivity.Register(this);
    private void OnDisable() => AutoSaveTransientActivity.Unregister(this);

    public static bool Fire(FortressCannon source,ZeldaCharacterData user,Vector3 muzzle,Vector2 facing,float velocity)
    {
        if(source==null || user==null || user.IsDead || user.IsGhostLike || facing.sqrMagnitude<.001f ||
            float.IsNaN(velocity) || float.IsInfinity(velocity) || velocity<=0) return false;
        var scene=source.gameObject.scene;
        if(!scene.IsValid() || !scene.isLoaded || ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject)!=scene) return false;
        var root=new GameObject("Cannonball"); SceneManager.MoveGameObjectToScene(root,scene); root.transform.position=muzzle;
        var ball=root.AddComponent<FortressCannonball>();
        ball.launcher=source.transform; ball.owner=user; ball.direction=facing.normalized; ball.speed=velocity;
        ball.filter=new ContactFilter2D {useTriggers=true}; ball.filter.SetLayerMask(Physics2D.AllLayers);
        if(defaultBomb==null) defaultBomb=Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem")?.GetComponent<BombPickupItem>();
        if(defaultBomb!=null)
        {
            ball.investigationRadius=defaultBomb.InvestigationRadius;
            ball.pulseDuration=defaultBomb.InvestigationPulseDuration;
            ball.pulseColor=defaultBomb.InvestigationPulseColor;
        }
        ball.ConfigureVisual(); ball.retention=root.GetComponent<CameraVisionStreamingRegion>(); ball.RefreshRetention();
        ball.MoveSwept(Vector2.zero); return true;
    }
    private void ConfigureVisual()
    {
        // A compact faceted iron sphere, readable without a trail or neon outline.
        var vertices=new List<Vector3>(); var colors=new List<Color>(); var triangles=new List<int>();
        AddDisc(vertices,colors,triangles,Vector2.zero,.16f,new Color(.16f,.20f,.24f),0);
        AddDisc(vertices,colors,triangles,new Vector2(-.02f,.02f),.115f,new Color(.49f,.55f,.59f),-.001f);
        AddDisc(vertices,colors,triangles,new Vector2(-.052f,.052f),.045f,new Color(.82f,.86f,.88f),-.002f);
        mesh=new Mesh {name="Geometric Cannonball"}; mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles,0);
        mesh.uv=new Vector2[vertices.Count]; mesh.RecalculateBounds();
        material=new Material(Shader.Find("Sprites/Default")); GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=GetComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=2;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
    }
    private static void AddDisc(List<Vector3> v,List<Color> c,List<int> t,Vector2 center,float radius,Color color,float z)
    {
        int start=v.Count;
        for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; v.Add(new Vector3(center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius,z)); c.Add(color); }
        for(int i=1;i<7;i++) { t.Add(start); t.Add(start+i+1); t.Add(start+i); }
    }
    private void RefreshRetention()
    {
        retention.Configure(speed*.2f+Mathf.Max(ExplosionRadius,investigationRadius)+.2f); retentionTimer=.1f;
    }
    private void FixedUpdate()
    {
        if(finished) return;
        float step=Mathf.Min(Time.fixedDeltaTime,Lifetime-elapsed);
        if(step<=0) { Explode(); return; }
        retentionTimer-=step; if(retentionTimer<=0) RefreshRetention();
        if(!MoveSwept(direction*(speed*step))) return;
        elapsed+=step; if(elapsed>=Lifetime) Explode();
    }
    private bool MoveSwept(Vector2 displacement)
    {
        if(finished) return false;
        Physics2D.SyncTransforms(); Vector2 start=transform.position;
        overlaps.Clear(); Physics2D.OverlapCircle(start,FortressBallistaBolt.ContactRadius,filter,overlaps);
        foreach(var collider in overlaps) if(FortressBallistaBolt.CanProjectileHit(collider,launcher,gameObject.scene)) { Explode(); return false; }
        float distance=displacement.magnitude; if(distance<=.00001f) return true;
        hits.Clear(); Physics2D.CircleCast(start,FortressBallistaBolt.ContactRadius,displacement/distance,filter,hits,distance);
        float nearest=float.PositiveInfinity;
        foreach(var hit in hits) if(hit.distance<nearest && FortressBallistaBolt.CanProjectileHit(hit.collider,launcher,gameObject.scene)) nearest=hit.distance;
        if(!float.IsPositiveInfinity(nearest)) { transform.position+=(Vector3)(displacement/distance*nearest); Explode(); return false; }
        transform.position+=(Vector3)displacement; return true;
    }
    private void Explode()
    {
        if(finished) return; finished=true;
        Vector3 position=transform.position; var scene=gameObject.scene;
        // Wake suspended victims and listeners before notifying AI or the next damage physics step.
        retention.Configure(Mathf.Max(ExplosionRadius,investigationRadius)+.2f);
        var blast=new GameObject("Cannon Explosion Hitbox"); SceneManager.MoveGameObjectToScene(blast,scene); blast.transform.position=position;
        blast.AddComponent<CameraVisionStreamingRegion>().Configure(Mathf.Max(ExplosionRadius,investigationRadius)+.2f);
        var hitbox=blast.AddComponent<ZeldaAttackHitbox>();
        Color tint=new Color(.88f,.88f,.82f,.95f);
        hitbox.Configure(.12f,Vector2.one*(ExplosionRadius*2),tint,AttackPower,owner,ZeldaAttackVisualShape.Box,true);
        // Keep the ballista/bomb damage resolver (one hit per target), but honor the requested circular radius.
        blast.GetComponent<BoxCollider2D>().enabled=false;
        var circle=blast.AddComponent<CircleCollider2D>(); circle.isTrigger=true; circle.radius=.5f;
        blast.GetComponent<SpriteRenderer>().enabled=false;
        var effect=new GameObject("Cannon Explosion"); SceneManager.MoveGameObjectToScene(effect,scene); effect.transform.position=position;
        effect.AddComponent<CameraVisionStreamingExempt>();
        effect.AddComponent<BombExplosionVisual>().Configure(Vector2.one*(ExplosionRadius*2),.45f,tint,0,12);
        if(investigationRadius>0)
        {
            foreach(var ai in ZeldaRuntimeRegistry.AiCharacters)
                if(ai!=null && ai.isActiveAndEnabled && ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject)==scene &&
                    ((Vector2)ai.transform.position-(Vector2)position).sqrMagnitude<=investigationRadius*investigationRadius)
                    investigationTargets.Add(ai);
            foreach(var ai in investigationTargets) if(ai!=null) ai.InvestigatePosition(position);
            var pulse=new GameObject("Cannon Investigation Pulse"); SceneManager.MoveGameObjectToScene(pulse,scene); pulse.transform.position=position;
            pulse.AddComponent<CameraVisionStreamingExempt>();
            pulse.AddComponent<ZeldaInvestigationPulse>().Configure(investigationRadius,pulseDuration,pulseColor,0,11);
        }
        if(explosionSound==null) explosionSound=Resources.Load<AudioClip>("Audio/BombExplosion");
        if(explosionSound!=null)
        {
            var audioRoot=new GameObject("Cannon Explosion Sound"); SceneManager.MoveGameObjectToScene(audioRoot,scene); audioRoot.transform.position=position;
            audioRoot.AddComponent<CameraVisionStreamingExempt>(); var audio=audioRoot.AddComponent<AudioSource>();
            audio.playOnAwake=false; audio.clip=explosionSound; audio.volume=1; audio.spatialBlend=.85f;
            audio.rolloffMode=AudioRolloffMode.Logarithmic; audio.minDistance=1; audio.maxDistance=24; audio.dopplerLevel=0; audio.Play();
            Destroy(audioRoot,explosionSound.length+.1f);
        }
        retention.enabled=false; GetComponent<MeshRenderer>().enabled=false; Destroy(gameObject);
    }
    private void OnDestroy() { if(mesh!=null) Destroy(mesh); if(material!=null) Destroy(material); }
}
