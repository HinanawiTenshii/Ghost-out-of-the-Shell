using UnityEngine;

/// <summary>Small three-segment read-only status display, opt-in for the lab scene.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1100)]
public sealed class VisualStyleLabMechanismSignal : MonoBehaviour
{
    public enum DisplayState { Unpowered, Standby, Working, Complete }
    [SerializeField] private BatterySocket socket;
    [SerializeField] private WaterwayGateController controller;
    [SerializeField] private WaterwayGate gate;
    [SerializeField] private LeverData lever;
    [SerializeField] private DoubleSlidingDoor door;
    [SerializeField] private Material displayMaterial;
    [SerializeField] private Vector2 localPosition;
    [SerializeField] private int sortingOrder = 16;
    [SerializeField, Min(.02f)] private float segmentSize = .065f;
    private Mesh mesh;
    private MeshRenderer visual;
    private GameObject node;
    private Color[] colors = new Color[12];
    private bool initialized, wasMoving, wasOn;
    private int lastCharge;
    private float flashUntil, nextTick;
    public DisplayState State { get; private set; }
    private static readonly Color Off = new Color(.08f,.13f,.17f,1f);
    private static readonly Color Cyan = new Color(.27f,.75f,.82f,1f);
    private static readonly Color Amber = new Color(.87f,.55f,.20f,1f);
    private void OnEnable() { initialized = false; nextTick = 0; flashUntil = 0; if(node != null) node.SetActive(true); }
    private void Build()
    {
        node=new GameObject("Lab6 Status segments"){hideFlags=HideFlags.HideAndDontSave};node.transform.SetParent(transform,false);
        node.transform.localPosition=localPosition;node.layer=gameObject.layer;
        mesh=new Mesh{name="Lab6 Three signal segments",hideFlags=HideFlags.HideAndDontSave};
        var vertices=new Vector3[12];var triangles=new int[18];
        for(int i=0;i<3;i++)
        {
            float x=(i-1)*segmentSize*1.4f,w=segmentSize*.5f,h=segmentSize*.35f;int n=i*4,t=i*6;
            vertices[n]=new Vector3(x-w,-h);vertices[n+1]=new Vector3(x+w,-h);
            vertices[n+2]=new Vector3(x+w,h);vertices[n+3]=new Vector3(x-w,h);
            triangles[t]=n;triangles[t+1]=n+1;triangles[t+2]=n+2;triangles[t+3]=n;triangles[t+4]=n+2;triangles[t+5]=n+3;
        }
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors=colors;mesh.RecalculateBounds();
        node.AddComponent<MeshFilter>().sharedMesh=mesh;visual=node.AddComponent<MeshRenderer>();
        visual.sharedMaterial=displayMaterial;visual.sortingOrder=sortingOrder;
        visual.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;visual.receiveShadows=false;
    }
    private void LateUpdate()
    {
        if(Time.time < nextTick)return;nextTick=Time.time+.08f;
        if(node==null)Build();
        bool powered=socket!=null?socket.HasPower:controller!=null?controller.HasBatteryPower:lever!=null?lever.HasBatteryPower:true;
        bool on=gate!=null?gate.IsOpenRequested:controller!=null?controller.IsOpenRequested:door!=null?door.IsOpenRequested:lever!=null&&lever.IsOn;
        bool moving=(gate!=null&&gate.IsMoving)||(controller!=null&&(controller.IsTransitionLocked||(controller.LinkedGate!=null&&controller.LinkedGate.IsMoving)))||(door!=null&&door.IsMoving);
        int charge=socket!=null?socket.RemainingCharge:0;
        if(initialized && powered && ((wasMoving&&!moving)||(on!=wasOn&&!moving)||(socket!=null&&charge!=lastCharge)))flashUntil=Time.time+.24f;
        State=!powered?DisplayState.Unpowered:moving?DisplayState.Working:Time.time<flashUntil?DisplayState.Complete:DisplayState.Standby;
        initialized=true;wasMoving=moving;wasOn=on;lastCharge=charge;
        if(!SceneVisualWorkBudget.IsVisible(transform,new Vector2(2,2)))return;
        int step=Mathf.FloorToInt(Time.time*8f)%3;
        for(int i=0;i<3;i++)
        {
            Color color=State==DisplayState.Unpowered?Off:State==DisplayState.Complete?new Color(.8f,1f,.95f):
                State==DisplayState.Working?(i==step?Cyan:Off):i==0?(on||socket!=null?Cyan:Amber):Off;
            for(int j=0;j<4;j++)colors[i*4+j]=color;
        }
        mesh.colors=colors;
    }
    private void OnDisable(){if(node!=null)node.SetActive(false);}
    private void OnDestroy(){if(mesh!=null)Destroy(mesh);if(node!=null)Destroy(node);}
}
