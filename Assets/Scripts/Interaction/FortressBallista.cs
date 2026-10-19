using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Rear-operated siege crossbow. Authored local +Y is the arrow's direction.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(GeometricFortressDecoration), typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Fortress Ballista")]
public sealed class FortressBallista : MonoBehaviour
{
    public const float Cooldown = 3f;
    private static readonly HashSet<FortressBallista> ActiveBallistas = new HashSet<FortressBallista>();
    private readonly List<RaycastHit2D> mountObstructions = new List<RaycastHit2D>(8);
    [SerializeField, Min(.2f), Tooltip("默认尺寸下尾部向外的操作距离，随弩炮变换缩放。")]
    private float rearReach = 1.25f;
    [SerializeField, Min(.1f)] private float rearHalfWidth = .8f;
    [SerializeField, Range(.1f, .9f)] private float facingThreshold = .6f;
    [SerializeField, Min(1f)] private float boltSpeed = 12f;
    [SerializeField] private Font promptFont;
    private BoxCollider2D body;
    private GeometricFortressDecoration geometry;
    private float nextFireTime, recoilRemaining;
    private Transform recoilRoot;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;
    private const float RecoilDuration = .3f;

    public bool IsCoolingDown => Time.time < nextFireTime;
    public Vector2 ShotDirection => ((Vector2)transform.TransformVector(Vector3.up)).normalized;
    private bool ModalInputBlocked => Time.timeScale <= 0f || DocumentReader.IsInputBlocked ||
        SignpostInteraction.BlocksInput || CharacterDialogueWindow.BlocksInput;
    private bool InputBlocked => ModalInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput;
    private Vector2 LayoutScale => new Vector2(Body.size.x / 2.2f, Body.size.y / 2f);
    private BoxCollider2D Body => body != null ? body : (body = GetComponent<BoxCollider2D>());
    public Vector3 RearWorldPosition => transform.TransformPoint(Body.offset + Vector2.Scale(new Vector2(0,-.61f),LayoutScale));
    public Vector3 MuzzleWorldPosition => transform.TransformPoint(Body.offset + new Vector2(0,Body.size.y*.5f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ActiveBallistas.Clear(); }
    private void OnEnable() { ActiveBallistas.Add(this); }

    public bool CanAcceptPuppet(ClockworkPuppetRuntime except = null)
    {
        if (!isActiveAndEnabled || !Body.enabled) return false;
        foreach (var puppet in ClockworkPuppetRuntime.ActivePuppets)
            if (puppet != null && puppet != except && !puppet.IsBroken && puppet.IsAttachedToBallista(this)) return false;
        return true;
    }

    // Restrict attachment to the tail, following the prop's rotation and scale.
    // Surface distance allows large ballistas to be reached without walking inside their collider.
    public float GetPuppetMountDistance(Collider2D source, Vector2 position, Scene scene)
    {
        if (!isActiveAndEnabled || gameObject.scene != scene || !Body.enabled) return float.MaxValue;
        Vector2 scale = LayoutScale;
        if (scale.x <= .0001f || scale.y <= .0001f) return float.MaxValue;
        Vector2 local = (Vector2)transform.InverseTransformPoint(position) - Body.offset;
        local = new Vector2(local.x / scale.x, local.y / scale.y);
        if (Mathf.Abs(local.x) > rearHalfWidth || local.y > -.55f || local.y < -1f-rearReach) return float.MaxValue;
        float distance = Vector2.Distance(position, Body.ClosestPoint(position));
        if (source != null)
        {
            var separation = source.Distance(Body);
            if (separation.isValid) distance = separation.isOverlapped ? 0f : separation.distance;
        }
        return distance;
    }

    public bool HasClearPuppetApproach(Collider2D source, Vector2 position)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.AllLayers);
        mountObstructions.Clear();
        Physics2D.Linecast(position, RearWorldPosition, filter, mountObstructions);
        foreach (var hit in mountObstructions)
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) &&
                hit.collider != source && (source == null || !hit.collider.transform.IsChildOf(source.transform)))
                return false;
        return true;
    }

    public static FortressBallista FindNearestPuppetMount(Collider2D source, Vector2 position,
        Scene scene, float reach, out float closestDistance)
    {
        FortressBallista closest = null;
        closestDistance = float.MaxValue;
        foreach (var ballista in ActiveBallistas)
        {
            if (!ballista.CanAcceptPuppet()) continue;
            float distance = ballista.GetPuppetMountDistance(source, position, scene);
            if (distance > reach || distance >= closestDistance || !ballista.HasClearPuppetApproach(source, position)) continue;
            closest = ballista; closestDistance = distance;
        }
        return closest;
    }

    private void Awake() { geometry = GetComponent<GeometricFortressDecoration>(); MapPointOfInterest.AttachMechanism(this); }
    private void OnValidate()
    {
        rearReach = Mathf.Max(.2f,rearReach); rearHalfWidth = Mathf.Max(.1f,rearHalfWidth);
        boltSpeed = Mathf.Max(1f,boltSpeed);
    }

    // Exposed separately so rotated/scaled placement can be checked without simulating input.
    public bool IsAtRearAndFacing(Vector2 worldPosition, Vector2 facing)
    {
        Vector2 scale = LayoutScale;
        if (scale.x <= .0001f || scale.y <= .0001f || facing.sqrMagnitude < .001f) return false;
        Vector2 local = (Vector2)transform.InverseTransformPoint(worldPosition) - Body.offset;
        local = new Vector2(local.x / scale.x,local.y / scale.y);
        if (Mathf.Abs(local.x) > rearHalfWidth || local.y > -.55f || local.y < -1f-rearReach) return false;
        Vector2 toHandle = (Vector2)RearWorldPosition - worldPosition;
        return toHandle.sqrMagnitude > .001f && Vector2.Dot(facing.normalized,toHandle.normalized) >= facingThreshold;
    }

    private ZeldaFourWayMover GetOperator()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled ||
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene) return null;
        var data = mover.CharacterData;
        return data != null && !data.IsDead && !data.IsGhostLike &&
            IsAtRearAndFacing(mover.transform.position,mover.FacingDirection) ? mover : null;
    }

    private void Update()
    {
        var mover = InputBlocked || IsCoolingDown ? null : GetOperator();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this,mover,KeyCode.E,RearWorldPosition,ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this,mover,KeyCode.E,RearWorldPosition,TryFireFromInput);
    }

    private void TryFireFromInput() { TryFire(); }
    public bool TryFire()
    {
        if (!isActiveAndEnabled || InputBlocked || IsCoolingDown) return false;
        var mover = GetOperator();
        if (mover == null || !mover.TryBeginItemAttack(.24f)) return false;
        return FireForOperator(mover.CharacterData);
    }

    public bool TryFireFromPuppet(ClockworkPuppetRuntime puppet, ZeldaCharacterData owner)
    {
        // Human input is blocked during remote control; only the mounted, actively controlled
        // puppet is allowed through this separate entry point. Reuse the same shot/cooldown.
        if (!isActiveAndEnabled || ModalInputBlocked || IsCoolingDown || puppet == null ||
            !puppet.isActiveAndEnabled || puppet.IsBroken || !puppet.IsUnderRemoteControl ||
            !puppet.IsAttachedToBallista(this) || puppet.gameObject.scene != gameObject.scene) return false;
        return FireForOperator(owner);
    }

    private bool FireForOperator(ZeldaCharacterData owner)
    {
        if (!FortressBallistaBolt.Fire(this,owner,MuzzleWorldPosition,ShotDirection,boltSpeed)) return false;
        nextFireTime = Time.time + Cooldown;
        recoilRemaining = RecoilDuration;
        ShowPrompt(false);
        return true;
    }

    private void LateUpdate()
    {
        if (recoilRemaining <= 0f) return;
        if (geometry == null) geometry = GetComponent<GeometricFortressDecoration>();
        recoilRoot = geometry != null ? geometry.GeneratedRoot : null;
        recoilRemaining = Mathf.Max(0f,recoilRemaining-Time.deltaTime);
        if (recoilRoot == null) return;
        float progress = 1f-recoilRemaining/RecoilDuration, decay = 1f-progress;
        Vector2 scale = LayoutScale;
        recoilRoot.localPosition = new Vector3(Mathf.Sin(progress*38f)*.025f*scale.x,
            -Mathf.Sin(progress*Mathf.PI)*.10f*scale.y,0)*decay;
        recoilRoot.localRotation = Quaternion.Euler(0,0,Mathf.Sin(progress*32f)*2.5f*decay);
        if (recoilRemaining <= 0f) ResetRecoil();
    }

    private void ResetRecoil()
    {
        if (recoilRoot != null) { recoilRoot.localPosition=Vector3.zero; recoilRoot.localRotation=Quaternion.identity; }
        recoilRemaining=0f; recoilRoot=null;
    }

    private void ShowPrompt(bool visible)
    {
        var mover = visible && !InputBlocked && !IsCoolingDown ? GetOperator() : null;
        if (mover == null || promptFont == null)
        { if (promptObject != null) promptObject.SetActive(false); return; }
        if (promptObject == null)
        {
            // Independent root keeps text readable under arbitrary prop rotation/scale.
            promptObject = new GameObject("Ballista Interaction Prompt",typeof(TextMesh)) { hideFlags=HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(promptObject,gameObject.scene);
            promptObject.AddComponent<CameraVisionStreamingExempt>();
            promptText=promptObject.GetComponent<TextMesh>();promptText.font=promptFont;
            promptText.fontSize=72;promptText.characterSize=.035f;
            promptText.anchor=TextAnchor.MiddleCenter;promptText.alignment=TextAlignment.Center;
            promptText.color=ZeldaUiPalette.Primary;promptText.text="按[E]发射弩炮";
            promptMaterial=new Material(promptFont.material) { hideFlags=HideFlags.HideAndDontSave };
            var renderer=promptObject.GetComponent<MeshRenderer>();renderer.sharedMaterial=promptMaterial;
            renderer.sortingOrder=short.MaxValue-2;ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptFont.RequestCharactersInTexture(promptText.text,72,FontStyle.Normal);
        promptMaterial.mainTexture=promptFont.material.mainTexture;
        promptObject.transform.position=mover.GetOverheadWorldPosition(new Vector2(0,.9f));
        promptObject.SetActive(true);
    }

    private void OnDisable() { ActiveBallistas.Remove(this); ShowPrompt(false); ResetRecoil(); }
    private void OnDestroy()
    {
        if (promptObject != null) Destroy(promptObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }
    private void OnDrawGizmosSelected()
    {
        if (Body == null) return;
        Matrix4x4 previous=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
        Vector2 scale=LayoutScale;
        Gizmos.color=new Color(.2f,.9f,1f,.8f);
        Gizmos.DrawWireCube(Body.offset+Vector2.Scale(new Vector2(0,(-.55f-1f-rearReach)*.5f),scale),
            Vector2.Scale(new Vector2(rearHalfWidth*2f,.45f+rearReach),scale));
        Gizmos.DrawLine(Body.offset+new Vector2(0,Body.size.y*.5f),Body.offset+new Vector2(0,Body.size.y*.5f+.7f*scale.y));
        Gizmos.matrix=previous;
    }
}
