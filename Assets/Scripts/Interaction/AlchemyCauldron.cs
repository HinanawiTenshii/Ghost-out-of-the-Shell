using System;
using UnityEngine;

/// <summary>Three order-independent herb submissions, followed by one atomic bottle exchange.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Alchemy Cauldron (炼药锅)")]
public sealed class AlchemyCauldron : MonoBehaviour
{
    [Serializable] public struct RuntimeState
    {
        public int red, green, blue;
        public Color displayedColor, transitionFrom, transitionTarget;
        public float transitionRemaining;
    }
    [SerializeField, Tooltip("与实际锅液模型绑定；未指定时自动查找自身或子物体。")]
    private GeometricAlchemyDecoration cauldronVisual;
    [SerializeField, Min(.1f)] private float interactionDistance = 1.25f;
    [SerializeField, Min(.05f)] private float colorTransitionDuration = 1f;
    [SerializeField] private Font promptFont;
    private int red, green, blue;
    private Color displayedColor = AlchemyRecipeCatalog.UnbrewedColor;
    private Color transitionFrom = AlchemyRecipeCatalog.UnbrewedColor, transitionTarget = AlchemyRecipeCatalog.UnbrewedColor;
    private float transitionRemaining;
    private bool transferring;
    private TextMesh prompt;
    private Material promptMaterial;
    public int RedCount => red;
    public int GreenCount => green;
    public int BlueCount => blue;
    public int IngredientCount => red + green + blue;
    public bool IsReady => IngredientCount == 3;
    public Color DisplayedColor => displayedColor;
    public AlchemyPotionKind BrewedKind => AlchemyRecipeCatalog.Resolve(red,green,blue);
    private bool InputBlocked => Time.timeScale <= 0f || GameSaveSystem.IsLoading || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput;
    public RuntimeState CaptureRuntimeState() => new RuntimeState {
        red=red, green=green, blue=blue, displayedColor=displayedColor,
        transitionFrom=transitionFrom, transitionTarget=transitionTarget, transitionRemaining=transitionRemaining
    };
    public void ApplyRuntimeState(RuntimeState state)
    {
        red=Mathf.Clamp(state.red,0,3); green=Mathf.Clamp(state.green,0,3-red); blue=Mathf.Clamp(state.blue,0,3-red-green);
        transitionTarget=IsReady ? AlchemyRecipeCatalog.GetColor(BrewedKind) : AlchemyRecipeCatalog.UnbrewedColor;
        transitionRemaining=float.IsNaN(state.transitionRemaining) || float.IsInfinity(state.transitionRemaining)
            ? 0f : Mathf.Clamp(state.transitionRemaining,0,colorTransitionDuration);
        transitionFrom=ValidColor(state.transitionFrom) ? state.transitionFrom : transitionTarget;
        displayedColor=transitionRemaining>0 && ValidColor(state.displayedColor) ? state.displayedColor : transitionTarget;
        ApplyLiquidColor();
    }
    private static bool ValidColor(Color color) => color.a>0 && !float.IsNaN(color.r+color.g+color.b) && !float.IsInfinity(color.r+color.g+color.b);
    private void Awake()
    {
        ZeldaRequirementWindow.AttachSkillRequirement(this, CharacterSkillRequirements.Alchemy);
        MapPointOfInterest.AttachMechanism(this);
    }
    private void OnEnable() => ApplyLiquidColor();
    private void ApplyLiquidColor()
    {
        if (cauldronVisual == null) cauldronVisual=GetComponentInChildren<GeometricAlchemyDecoration>(true);
        if (cauldronVisual != null) cauldronVisual.SetLiquidColor(Application.isPlaying ? displayedColor : AlchemyRecipeCatalog.UnbrewedColor);
    }
    private void Update()
    {
        if (!Application.isPlaying) { ApplyLiquidColor(); return; }
        if (!GameSaveSystem.IsLoading && transitionRemaining>0 && Time.timeScale>0)
        {
            transitionRemaining=Mathf.Max(0,transitionRemaining-Time.deltaTime);
            float t=1f-transitionRemaining/Mathf.Max(.05f,colorTransitionDuration);
            displayedColor=Color.Lerp(transitionFrom,transitionTarget,t*t*(3f-2f*t)); ApplyLiquidColor();
        }
        var mover=InputBlocked || transferring ? null : Nearby();
        if(mover==null){ShowPrompt(false);return;}
        ZeldaInteractionArbiter.OfferInteraction(this,mover,KeyCode.E,transform.position,ShowPrompt);
        if(Input.GetKeyDown(KeyCode.E)) ZeldaInteractionArbiter.Submit(this,mover,KeyCode.E,transform.position,Interact);
    }
    private ZeldaFourWayMover Nearby()
    {
        var mover=ZeldaRuntimeRegistry.GetControlledMover();
        if(mover==null || !mover.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject)!=gameObject.scene) return null;
        var actor=mover.GetComponent<ZeldaCharacterData>();
        if(actor==null || actor.IsDead || actor.IsGhostLike) return null;
        var body=GetComponent<Collider2D>();
        Vector2 nearest=body!=null && body.enabled ? body.ClosestPoint(mover.transform.position) : (Vector2)transform.position;
        return Vector2.Distance(nearest,mover.transform.position)<=interactionDistance ? mover : null;
    }
    private void Interact()
    {
        TryInteract(out string message);
        if(!string.IsNullOrEmpty(message)) ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(message);
    }
    public bool TryInteract(out string message)
    {
        message=null;
        if(!Application.isPlaying || !isActiveAndEnabled || InputBlocked || transferring) return false;
        var mover=Nearby(); if(mover==null || mover.IsAttacking) return false;
        if (!CharacterSkillRequirements.Has(mover.CharacterData, CharacterSkillRequirements.Alchemy))
        { message = CharacterSkillRequirements.FailureMessage(CharacterSkillRequirements.Alchemy); return false; }
        var inventory=PersistentInventory.Instance;
        var slot=inventory!=null ? inventory.GetSlot(inventory.SelectedSlotIndex) : null;
        if(slot==null || slot.IsEmpty) { message=StatusText()+"\n请选择"+(IsReady ? "玻璃瓶承接药水" : "一份草药粉");return false; }
        transferring=true;
        try
        {
            bool success;
            if(IsReady)
            {
                var kind=BrewedKind;
                if(slot.ItemId!=GlassBottlePickupItem.DefaultItemId){message="已炼成"+AlchemyRecipeCatalog.GetName(kind)+"，请选择玻璃瓶承接";return false;}
                success=inventory.TryExchangeSelectedItem(GlassBottlePickupItem.DefaultItemId,AlchemyRecipeCatalog.GetItemId(kind),()=>{
                    red=green=blue=0; BeginColorTransition(AlchemyRecipeCatalog.UnbrewedColor);
                });
                message=success ? "获得"+AlchemyRecipeCatalog.GetName(kind) : "物品栏空间不足，药水和玻璃瓶均已保留";
            }
            else
            {
                string id=slot.ItemId;
                if(!AlchemyRecipeCatalog.IsIngredient(id)){message=StatusText()+"\n请选择红、绿或蓝色草药粉";return false;}
                success=inventory.TryConsumeSelectedItem(id,()=>{
                    if(id==AlchemyRecipeCatalog.RedHerbId)red++; else if(id==AlchemyRecipeCatalog.GreenHerbId)green++; else blue++;
                    if(IsReady) BeginColorTransition(AlchemyRecipeCatalog.GetColor(BrewedKind));
                });
                message=success ? StatusText() : "未能提交草药，物品已保留";
            }
            if(success)mover.TryBeginItemAttack(.24f);
            return success;
        }
        finally{transferring=false;}
    }
    public string StatusText() => IsReady ? "已炼成："+AlchemyRecipeCatalog.GetName(BrewedKind)+"\n按[E]用玻璃瓶承接"
        : "草药：红"+red+" 绿"+green+" 蓝"+blue+"（"+IngredientCount+"/3）\n按[E]提交选中的草药";
    private void BeginColorTransition(Color target)
    { transitionFrom=displayedColor; transitionTarget=target; transitionRemaining=Mathf.Max(.05f,colorTransitionDuration); }
    private void ShowPrompt(bool visible)
    {
        var mover=visible && !InputBlocked ? Nearby() : null;
        Font font=promptFont!=null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if(mover==null || font==null){if(prompt!=null)prompt.gameObject.SetActive(false);return;}
        if(prompt==null)
        {
            var go=new GameObject("Alchemy Cauldron Prompt",typeof(TextMesh)){hideFlags=HideFlags.HideAndDontSave};
            go.transform.SetParent(transform,false);prompt=go.GetComponent<TextMesh>();
            prompt.font=font;prompt.fontSize=72;prompt.characterSize=.028f;prompt.anchor=TextAnchor.MiddleCenter;
            prompt.alignment=TextAlignment.Center;prompt.color=ZeldaUiPalette.Primary;
            promptMaterial=new Material(font.material){hideFlags=HideFlags.HideAndDontSave};
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=promptMaterial;
            renderer.sortingOrder=short.MaxValue-2;ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        prompt.text=CharacterSkillRequirements.Has(mover.CharacterData,CharacterSkillRequirements.Alchemy) ? StatusText() : "能力值不足";
        font.RequestCharactersInTexture(prompt.text,72,FontStyle.Normal);promptMaterial.mainTexture=font.material.mainTexture;
        prompt.transform.position=mover.GetOverheadWorldPosition(new Vector2(0,1.05f));prompt.transform.rotation=Quaternion.identity;
        var scale=transform.lossyScale;
        prompt.transform.localScale=new Vector3(1f/Mathf.Max(.001f,Mathf.Abs(scale.x)),1f/Mathf.Max(.001f,Mathf.Abs(scale.y)),1f/Mathf.Max(.001f,Mathf.Abs(scale.z)));
        prompt.gameObject.SetActive(true);
    }
    private void OnDisable(){if(prompt!=null)prompt.gameObject.SetActive(false);}
    private void OnDestroy(){Release(prompt!=null ? prompt.gameObject : null);Release(promptMaterial);}
    private static void Release(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    private void OnValidate(){interactionDistance=Mathf.Max(.1f,interactionDistance);colorTransitionDuration=Mathf.Max(.05f,colorTransitionDuration);}
}
