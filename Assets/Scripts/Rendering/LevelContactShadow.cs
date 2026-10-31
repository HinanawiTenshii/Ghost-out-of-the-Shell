using UnityEngine;

/// <summary>Small shared-mesh contact shadow; no bounds-shaped extrusion or silhouette outline.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1100)]
public sealed class LevelContactShadow : MonoBehaviour
{
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private bool actor;
    private SpriteRenderer source;
    private ZeldaCharacterData character;
    private MeshRenderer shadow;
    private float nextTick;
    public void Configure(Mesh shape, Material ink, bool isActor)
    { mesh=shape; material=ink; actor=isActor; }
    void OnEnable() { source=null; nextTick=0; character=GetComponent<ZeldaCharacterData>(); }
    void OnDisable() { if(shadow) shadow.enabled=false; }
    void LateUpdate()
    {
        if(Time.unscaledTime<nextTick)return;
        nextTick=Time.unscaledTime+.125f;
        if(!mesh || !material || !SceneVisualWorkBudget.IsVisible(transform, new Vector2(4,4)))return;
        if(!source || !source.enabled)
        {
            var visual=actor ? transform.Find("Visual") : null;
            source=visual ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
            if(!source || !source.enabled) source=GetComponentInChildren<SpriteRenderer>();
        }
        bool show=source && source.enabled && source.gameObject.activeInHierarchy && source.color.a>.05f &&
            (!character || (!character.IsDead && !character.IsGhostLike));
        if(!show) { if(shadow)shadow.enabled=false; return; }
        if(!shadow)
        {
            var node=new GameObject("Terminal Contact Shadow") { hideFlags=HideFlags.DontSave };
            node.transform.SetParent(transform,false);
            node.layer=gameObject.layer;
            node.AddComponent<MeshFilter>().sharedMesh=mesh;
            shadow=node.AddComponent<MeshRenderer>(); shadow.sharedMaterial=material;
            shadow.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; shadow.receiveShadows=false;
        }
        shadow.enabled=true;
        shadow.sortingLayerID=source.sortingLayerID;
        shadow.sortingOrder=Mathf.Min(-5,source.sortingOrder-1);
        var bounds=source.bounds;
        Vector3 center=actor ? transform.position+new Vector3(0,-.12f,0) : new Vector3(bounds.center.x,bounds.min.y+.04f,transform.position.z);
        shadow.transform.position=center;
        float width=actor?.66f:Mathf.Clamp(bounds.size.x*.82f,.18f,2.8f);
        shadow.transform.rotation=Quaternion.identity;
        var parentScale=transform.lossyScale;
        shadow.transform.localScale=new Vector3(width/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),
            (actor?.23f:Mathf.Clamp(width*.23f,.08f,.38f))/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1);
    }
}
