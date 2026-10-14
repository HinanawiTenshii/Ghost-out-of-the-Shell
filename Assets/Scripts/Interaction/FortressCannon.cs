using System.Collections.Generic;
using UnityEngine;

/// <summary>Rear-operated cannon; local +Y is the muzzle direction.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(GeometricFortressDecoration), typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Fortress Cannon (加农炮)")]
public sealed class FortressCannon : MonoBehaviour
{
    public const float Cooldown = 6f;
    [SerializeField, Min(.2f)] private float rearReach = 1.25f;
    [SerializeField, Min(.1f)] private float rearHalfWidth = .8f;
    [SerializeField, Range(.1f,.9f)] private float facingThreshold = .6f;
    [SerializeField, Min(1f)] private float projectileSpeed = 12f;
    [SerializeField] private Font promptFont;
    private readonly List<RaycastHit2D> approachHits = new List<RaycastHit2D>(8);
    private BoxCollider2D body;
    private GeometricFortressDecoration geometry;
    private float nextFireTime, recoilRemaining;
    private Transform recoilRoot;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;
    private const float RecoilDuration = .35f;
    private BoxCollider2D Body => body != null ? body : (body = GetComponent<BoxCollider2D>());
    public bool IsCoolingDown => Time.time < nextFireTime;
    public float RemainingCooldown => Mathf.Max(0f,nextFireTime-Time.time);
    public Vector2 ShotDirection => ((Vector2)transform.TransformVector(Vector3.up)).normalized;
    public Vector3 RearWorldPosition => transform.TransformPoint(Body.offset + new Vector2(0,-Body.size.y*.5f));
    public Vector3 MuzzleWorldPosition => transform.TransformPoint(Body.offset + new Vector2(0,Body.size.y*.5f));
    private bool InputBlocked => Time.timeScale<=0 || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput;
    private void Awake()
    {
        geometry=GetComponent<GeometricFortressDecoration>();
        ZeldaRequirementWindow.AttachSkillRequirement(this,CharacterSkillRequirements.Cannon);
    }
    private void OnValidate()
    { rearReach=Mathf.Max(.2f,rearReach); rearHalfWidth=Mathf.Max(.1f,rearHalfWidth); projectileSpeed=Mathf.Max(1f,projectileSpeed); }

    public bool IsAtRearAndFacing(Vector2 worldPosition, Vector2 facing)
    {
        Vector2 scale=new Vector2(Body.size.x/2.4f,Body.size.y/3f);
        if(scale.x<=.0001f || scale.y<=.0001f || facing.sqrMagnitude<.001f) return false;
        Vector2 local=(Vector2)transform.InverseTransformPoint(worldPosition)-Body.offset;
        local=new Vector2(local.x/scale.x,local.y/scale.y);
        if(Mathf.Abs(local.x)>rearHalfWidth || local.y> -1.35f || local.y< -1.5f-rearReach) return false;
        Vector2 toRear=(Vector2)RearWorldPosition-worldPosition;
        return toRear.sqrMagnitude>.001f && Vector2.Dot(facing.normalized,toRear.normalized)>=facingThreshold;
    }
    private ZeldaFourWayMover GetOperator()
    {
        var mover=ZeldaRuntimeRegistry.GetControlledMover();
        if(mover==null || !mover.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject)!=gameObject.scene) return null;
        var actor=mover.CharacterData;
        if(actor==null || actor.IsDead || actor.IsGhostLike || !IsAtRearAndFacing(mover.transform.position,mover.FacingDirection)) return null;
        // No interaction through a wall behind the carriage.
        approachHits.Clear();
        var filter=new ContactFilter2D {useTriggers=false}; filter.SetLayerMask(Physics2D.AllLayers);
        Physics2D.Linecast(mover.transform.position,RearWorldPosition,filter,approachHits);
        foreach(var hit in approachHits)
            if(hit.collider!=null && !hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(mover.transform)) return null;
        return mover;
    }
    private void Update()
    {
        var mover=InputBlocked || IsCoolingDown ? null : GetOperator();
        if(mover==null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this,mover,KeyCode.E,RearWorldPosition,ShowPrompt);
        if(Input.GetKeyDown(KeyCode.E)) ZeldaInteractionArbiter.Submit(this,mover,KeyCode.E,RearWorldPosition,FireFromInput);
    }
    private void FireFromInput() => TryFire();
    public bool TryFire()
    {
        if(!isActiveAndEnabled || InputBlocked || IsCoolingDown) return false;
        var mover=GetOperator();
        if(mover==null || !CharacterSkillRequirements.Require(mover.CharacterData,CharacterSkillRequirements.Cannon) ||
            !mover.TryBeginItemAttack(.24f)) return false;
        if(!FortressCannonball.Fire(this,mover.CharacterData,MuzzleWorldPosition,ShotDirection,projectileSpeed)) return false;
        nextFireTime=Time.time+Cooldown; recoilRemaining=RecoilDuration; ShowPrompt(false); return true;
    }
    private void LateUpdate()
    {
        if(recoilRemaining<=0) return;
        if(geometry==null) geometry=GetComponent<GeometricFortressDecoration>();
        recoilRoot=geometry!=null ? geometry.GeneratedRoot : null;
        recoilRemaining=Mathf.Max(0,recoilRemaining-Time.deltaTime);
        if(recoilRoot!=null)
        {
            float progress=1f-recoilRemaining/RecoilDuration, decay=1f-progress;
            recoilRoot.localPosition=new Vector3(Mathf.Sin(progress*38)*.025f*Body.size.x/2.4f,
                -Mathf.Sin(progress*Mathf.PI)*.16f*Body.size.y/3f,0)*decay;
            recoilRoot.localRotation=Quaternion.Euler(0,0,Mathf.Sin(progress*32)*1.5f*decay);
        }
        if(recoilRemaining<=0) ResetRecoil();
    }
    private void ResetRecoil()
    {
        if(recoilRoot!=null) { recoilRoot.localPosition=Vector3.zero; recoilRoot.localRotation=Quaternion.identity; }
        recoilRemaining=0; recoilRoot=null;
    }
    private void ShowPrompt(bool visible)
    {
        var mover=visible && !InputBlocked && !IsCoolingDown ? GetOperator() : null;
        if(mover==null) { if(promptObject!=null) promptObject.SetActive(false); return; }
        var font=promptFont!=null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if(font==null) return;
        if(promptObject==null)
        {
            promptObject=new GameObject("Cannon Interaction Prompt",typeof(TextMesh)) {hideFlags=HideFlags.HideAndDontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(promptObject,gameObject.scene);
            promptObject.AddComponent<CameraVisionStreamingExempt>();
            promptText=promptObject.GetComponent<TextMesh>(); promptText.font=font; promptText.fontSize=72;
            promptText.characterSize=.035f; promptText.anchor=TextAnchor.MiddleCenter; promptText.alignment=TextAlignment.Center;
            promptText.color=ZeldaUiPalette.Primary;
            promptMaterial=new Material(font.material) {hideFlags=HideFlags.HideAndDontSave};
            var renderer=promptObject.GetComponent<MeshRenderer>(); renderer.sharedMaterial=promptMaterial;
            renderer.sortingOrder=short.MaxValue-2; ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptText.text=CharacterSkillRequirements.Has(mover.CharacterData,CharacterSkillRequirements.Cannon)
            ? "按[E]发射加农炮" : "能力值不足";
        font.RequestCharactersInTexture(promptText.text,72,FontStyle.Normal); promptMaterial.mainTexture=font.material.mainTexture;
        promptObject.transform.position=mover.GetOverheadWorldPosition(new Vector2(0,.9f)); promptObject.SetActive(true);
    }
    private void OnDisable() { ShowPrompt(false); ResetRecoil(); }
    private void OnDestroy() { if(promptObject!=null) Destroy(promptObject); if(promptMaterial!=null) Destroy(promptMaterial); }
    private void OnDrawGizmosSelected()
    {
        var previous=Gizmos.matrix; Gizmos.matrix=transform.localToWorldMatrix;
        Gizmos.color=new Color(.2f,.9f,1,.8f);
        var scale=new Vector2(Body.size.x/2.4f,Body.size.y/3f);
        Gizmos.DrawWireCube(Body.offset+Vector2.Scale(new Vector2(0,(-1.35f-1.5f-rearReach)*.5f),scale),
            Vector2.Scale(new Vector2(rearHalfWidth*2,.15f+rearReach),scale));
        Gizmos.DrawLine(Body.offset+new Vector2(0,Body.size.y*.5f),Body.offset+new Vector2(0,Body.size.y*.5f+.8f));
        Gizmos.matrix=previous;
    }
}
