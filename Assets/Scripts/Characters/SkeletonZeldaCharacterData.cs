using System;
using UnityEngine;

/// <summary>Coarse, neckless bone character with six non-physical reassembling remains.</summary>
public sealed class SkeletonZeldaCharacterData : ZeldaCharacterData
{
    [Header("Unarmed Combat / 空手战斗")]
    [SerializeField] private GameObject attackPrefab;
    [SerializeField, Min(1)] private int attackPower = 2;
    [SerializeField, Min(.1f)] private float attackDuration = .32f;
    [Header("Reassembly / 骨骼重组")]
    [SerializeField, Min(.1f)] private float scatterDuration = .45f;
    [SerializeField, Min(0f)] private float revivalDelay = 4f;
    [SerializeField, Min(.1f)] private float assembleDuration = 1.2f;
    [SerializeField, Min(.1f)] private float scatterDistance = 1.25f;
    [SerializeField, Min(.1f)] private float remainsFadeDuration = 1.2f;

    public EnergyCrystal BoundCrystal { get; private set; }
    public bool CanReassemble => BoundCrystal != null && BoundCrystal.Powers(this);
    public Vector3 LinkCenter => bodyRenderer != null ? bodyRenderer.bounds.center :
        GetComponent<SpriteRenderer>() != null ? GetComponent<SpriteRenderer>().bounds.center : transform.position;
    internal void SetEnergyCrystal(EnergyCrystal crystal)
    {
        BoundCrystal = crystal;
        if (crystal == null) OnCrystalPowerLost();
    }
    private void OnEnable() => EnergyCrystal.ConnectExistingSkeleton(this);
    public void OnCrystalPowerLost()
    {
        if (IsDead && reassembling && !fadingRemains)
        { fadingRemains = true; fadeElapsed = -Mathf.Max(0, scatterDuration - elapsed) - .25f; }
    }

    [Serializable] private struct RemainsState { public float elapsed; public int direction; public bool fading; public float fadeElapsed; }
    private readonly Sprite[] sprites = new Sprite[16];
    private readonly Transform[] pieces = new Transform[6];
    private readonly SpriteRenderer[] pieceRenderers = new SpriteRenderer[6];
    private readonly Vector3[] origins = new Vector3[6];
    private readonly Vector3[] offsets = new Vector3[6];
    private float elapsed;
    private int direction;
    private bool reassembling;
    private bool fadingRemains;
    private float fadeElapsed;
    private GameObject remainsRoot;
    private SpriteRenderer bodyRenderer;
    private BoxCollider2D bodyCollider;
    private Rigidbody2D body;
    private ZeldaFourWayMover mover;
    private ZeldaCharacterAiBase ai;
    private CameraVisionStreamingExempt ownedRetention;
    private static Sprite fistSprite;

    public override bool CanAttack => !IsDead && !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => new Vector2(0, .48f);
    public override Vector2 AttackSize => new Vector2(.65f, .65f);
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.BoneFist;
    public override Color AttackVisualTint => Color.white;
    public override bool CanTakeAttackDamage => !IsDead && base.CanTakeAttackDamage;
    public override bool CanBeDetectedByAi => !IsDead && base.CanBeDetectedByAi;
    public override bool ParticipatesInCharacterCollision => !IsDead && base.ParticipatesInCharacterCollision;

    // 16 x 20, 16 PPU, same short-legged silhouette/pivot as existing characters.
    // Broad ribs end at row 11; a slim spine joins the single-row pelvis at row 14.
    private static readonly string[] Front = {
        "................", "................", ".....HHHHHH.....", "....HBBBBBBH....",
        "....BDDBBDDB....", "....BDDBBDDB....", ".....BBDBBB.....", ".....BSBBSB.....",
        "...BBHBBBBHBB...", "...SB.BSSB.BS...", "...BB.BBBB.BB...", "...SB.BSSB.BS...",
        "...BB..BB..BB...", ".......BB.......", ".....BBBBBB.....", ".....BS..SB.....",
        ".....BB..BB.....", ".....BB..BB.....", "................", "................"
    };
    private static readonly string[] Back = {
        "................", "................", ".....HHHHHH.....", "....HBBBBBBH....",
        "....BBBBBBBB....", "....SBBBBBBS....", ".....SBBBBS.....", ".....BBBBBB.....",
        "...BBHBBBBHBB...", "...SB.BSSB.BS...", "...BB.SBBS.BB...", "...SB.BSSB.BS...",
        "...BB..BB..BB...", ".......BB.......", ".....BBBBBB.....", ".....BS..SB.....",
        ".....BB..BB.....", ".....BB..BB.....", "................", "................"
    };
    private static readonly string[] Side = {
        "................", "................", ".....HHHHH......", "....HBBBBBH.....",
        "....DDBBBBS.....", "....DDBBBBS.....", "....BDBBBS......", ".....BBBBS......",
        ".....HBBBB......", ".....SBBSB......", ".....BBABB......", ".....SBASB......",
        ".......AAB......", "........BB......", ".....BBBBB......", ".....BS.SB......",
        ".....BB.BB......", ".....BB.BB......", "................", "................"
    };

    private static Color BoneColor(char c)
    {
        switch (c)
        {
            case 'H': return new Color(.97f, .98f, .94f, 1);
            case 'B': case 'A': return new Color(.82f, .86f, .8f, 1);
            case 'S': return new Color(.43f, .51f, .51f, 1);
            case 'D': return new Color(.09f, .13f, .19f, 1);
            default: return Color.clear;
        }
    }
    private static char Symbol(int facing, int x, int row) =>
        (facing == 0 ? Front : facing == 1 ? Back : Side)[row][facing == 3 ? 15 - x : x];
    private static int PartAt(int facing, int x, int row)
    {
        if (row <= 7) return 0;
        if (row >= 15) return x < 8 ? 4 : 5;
        if (facing >= 2) return Symbol(facing, x, row) == 'A' ? 2 : 1;
        if (x <= 4) return 2;
        if (x >= 11) return 3;
        return 1;
    }
    private static char PoseSymbol(int facing, int x, int row, int pose)
    {
        if (pose == 3 && PartAt(facing,x,row) == 2) return '.';
        if ((pose == 1 || pose == 2) && row == 17 && ((pose == 1) == (x < 8))) return '.';
        return Symbol(facing,x,row);
    }
    private Sprite CreateSprite(int facing, int part, out Vector3 center, int pose=0)
    {
        int minX=16, maxX=-1, minY=20, maxY=-1;
        for (int y=0; y<20; y++) for (int x=0; x<16; x++)
            if (BoneColor(Symbol(facing,x,y)).a > 0 && (part < 0 || PartAt(facing,x,y)==part))
            { minX=Mathf.Min(minX,x); maxX=Mathf.Max(maxX,x); minY=Mathf.Min(minY,y); maxY=Mathf.Max(maxY,y); }
        center=Vector3.zero;
        if (maxX < minX) return null; // Far arm is occluded in side view.
        if (part < 0) { minX=0; maxX=15; minY=0; maxY=19; }
        int width=maxX-minX+1, height=maxY-minY+1;
        var tex=new Texture2D(width,height,TextureFormat.RGBA32,false) {
            name="Skeleton " + facing + " Part " + part, filterMode=FilterMode.Point,
            wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.HideAndDontSave };
        for (int y=minY; y<=maxY; y++) for (int x=minX; x<=maxX; x++)
            tex.SetPixel(x-minX,maxY-y,part<0 ? BoneColor(PoseSymbol(facing,x,y,pose)) :
                PartAt(facing,x,y)==part ? BoneColor(Symbol(facing,x,y)) : Color.clear);
        tex.Apply();
        Vector2 pivot=part<0 ? new Vector2(.5f,.2f) : Vector2.one*.5f;
        var sprite=Sprite.Create(tex,new Rect(0,0,width,height),pivot,16f);
        sprite.name=tex.name; sprite.hideFlags=HideFlags.HideAndDontSave;
        if(part>=0) center=new Vector3(((minX+maxX+1)*.5f-8)/16f,(20-(minY+maxY+1)*.5f-4)/16f,0);
        return sprite;
    }
    public Sprite GetPreviewSprite(int facing=0, int pose=0)
    {
        facing=Mathf.Clamp(facing,0,3);
        int index=facing*4+Mathf.Clamp(pose,0,3);
        if(sprites[index]==null) sprites[index]=CreateSprite(facing,-1,out _,pose);
        return sprites[index];
    }
    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        if(renderer==null || IsDead) return;
        direction=Mathf.Abs(facing.x)>Mathf.Abs(facing.y) ? (facing.x<0 ? 2:3) : (facing.y>0 ? 1:0);
        int pose=attacking ? 3 : moving ? 1+Mathf.FloorToInt(Time.time*7)%2 : 0;
        renderer.sprite=GetPreviewSprite(direction,pose);
        renderer.color=GetDamageFeedbackTint(Color.white);
    }
    public static Sprite GetFistSprite()
    {
        if(fistSprite!=null) return fistSprite;
        string[] rows={"........","..HHHH..",".HBBBBH.",".BBSSBB.","..BBBB..","...BB...","...SS...","........"};
        var tex=new Texture2D(8,8,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)tex.SetPixel(x,7-y,BoneColor(rows[y][x]));
        tex.Apply();fistSprite=Sprite.Create(tex,new Rect(0,0,8,8),new Vector2(.5f,.25f),8);
        fistSprite.hideFlags=HideFlags.HideAndDontSave;return fistSprite;
    }

    protected override bool TryHandleDeathRemains()
    {
        BeginReassembly(0f);
        return true;
    }
    private void BeginReassembly(float restoredElapsed)
    {
        CleanupPieces();
        if(gameObject.scene.name=="DontDestroyOnLoad" && transform.parent==null)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject,
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        mover=GetComponent<ZeldaFourWayMover>(); ai=GetComponent<ZeldaCharacterAiBase>();
        body=GetComponent<Rigidbody2D>(); bodyCollider=GetComponent<BoxCollider2D>();
        bodyRenderer=GetComponent<SpriteRenderer>();
        var visual=transform.Find("Visual");
        if(visual!=null && visual.GetComponent<SpriteRenderer>()!=null) bodyRenderer=visual.GetComponent<SpriteRenderer>();
        // Death notification has already spawned a ghost for a player-controlled body.
        mover.CancelAttackForStun(); mover.enabled=false; ai.enabled=false;
        body.velocity=Vector2.zero;body.angularVelocity=0;body.simulated=false;bodyCollider.enabled=false;
        if(GetComponent<CameraVisionStreamingExempt>()==null) ownedRetention=gameObject.AddComponent<CameraVisionStreamingExempt>();
        remainsRoot=new GameObject("Skeleton Bone Remains"){hideFlags=HideFlags.DontSave,layer=gameObject.layer};
        remainsRoot.transform.SetParent(transform,false);
        remainsRoot.transform.localPosition=bodyRenderer.transform==transform ? Vector3.zero : bodyRenderer.transform.localPosition;
        for(int i=0;i<6;i++)
        {
            var sprite=CreateSprite(direction,i,out origins[i]);if(sprite==null)continue;
            var go=new GameObject("Bone Part "+i){hideFlags=HideFlags.DontSave,layer=gameObject.layer};
            go.transform.SetParent(remainsRoot.transform,false);pieces[i]=go.transform;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;pieceRenderers[i]=renderer;
            renderer.sharedMaterial=bodyRenderer.sharedMaterial;renderer.sortingLayerID=bodyRenderer.sortingLayerID;
            renderer.sortingOrder=bodyRenderer.sortingOrder;renderer.maskInteraction=bodyRenderer.maskInteraction;
            // No collider or rigidbody: fragments are purely visual.
            float angle=(90+i*61)*Mathf.Deg2Rad;
            offsets[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*scatterDistance*(.8f+i*.045f);
        }
        bodyRenderer.enabled=false;reassembling=true;elapsed=Mathf.Max(0,restoredElapsed);
        fadingRemains=false;fadeElapsed=0;
        AnimateRemains();
    }
    public static float ScatterFraction(float time,float scatter,float wait,float assemble)
    {
        if(time<scatter){float t=Mathf.Clamp01(time/Mathf.Max(.1f,scatter));return 1-(1-t)*(1-t);}
        float u=Mathf.Clamp01((time-scatter-wait)/Mathf.Max(.1f,assemble));
        return 1-u*u*(3-2*u);
    }
    private void AnimateRemains()
    {
        float fraction=ScatterFraction(elapsed,scatterDuration,revivalDelay,assembleDuration);
        for(int i=0;i<pieces.Length;i++)if(pieces[i]!=null)
        {
            pieces[i].localPosition=origins[i]+offsets[i]*fraction;
            pieces[i].localRotation=Quaternion.Euler(0,0,(i%2==0 ? 1:-1)*(80+i*19)*fraction);
        }
    }
    protected override void Update()
    {
        base.Update();
        if(!IsDead || GameSaveSystem.IsLoading) return;
        // Also handles legacy saves of a downed skeleton without a custom snapshot.
        if(!reassembling) BeginReassembly(0);
        if(!CanReassemble) OnCrystalPowerLost();
        if(fadingRemains)
        {
            // Finish the outward scatter, or freeze a half-completed assembly in place.
            if(elapsed<scatterDuration) elapsed=Mathf.Min(scatterDuration,elapsed+Time.deltaTime);
            fadeElapsed+=Time.deltaTime;AnimateRemains();
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(fadeElapsed/Mathf.Max(.1f,remainsFadeDuration)));
            for(int i=0;i<pieceRenderers.Length;i++) if(pieceRenderers[i]!=null)
                pieceRenderers[i].color=new Color(1,1,1,alpha);
            if(fadeElapsed>=remainsFadeDuration) Destroy(gameObject);
            return;
        }
        elapsed+=Time.deltaTime;AnimateRemains();
        if(elapsed<scatterDuration+revivalDelay+assembleDuration) return;
        CleanupPieces();reassembling=false;
        RestoreAfterDeath();body.simulated=true;bodyCollider.enabled=true;
        bodyRenderer.enabled=true;ApplyCharacterVisual(bodyRenderer,Vector2.down,false,false);
        ai.enabled=true; // Resurrection never steals control back from the player's ghost.
        (ai as SkeletonCharacterAi)?.ResumeAfterReassembly();
        if(ownedRetention!=null){Destroy(ownedRetention);ownedRetention=null;}
    }
    public override RuntimeState CaptureRuntimeState()
    {
        var state=base.CaptureRuntimeState();
        state.characterSpecificState=JsonUtility.ToJson(new RemainsState{elapsed=elapsed,direction=direction,fading=fadingRemains,fadeElapsed=fadeElapsed});return state;
    }
    public override void ApplyRuntimeState(RuntimeState state)
    {
        base.ApplyRuntimeState(state);
        var saved=string.IsNullOrEmpty(state.characterSpecificState) ? default : JsonUtility.FromJson<RemainsState>(state.characterSpecificState);
        direction=Mathf.Clamp(saved.direction,0,3);
        if(IsDead) { BeginReassembly(saved.elapsed); fadingRemains=saved.fading;fadeElapsed=saved.fadeElapsed; }
        else
        {
            CleanupPieces();reassembling=false;
            if(body!=null)body.simulated=true;
            if(bodyCollider!=null)bodyCollider.enabled=true;
            if(bodyRenderer!=null)bodyRenderer.enabled=true;
            if(ownedRetention!=null){Destroy(ownedRetention);ownedRetention=null;}
        }
    }
    private void CleanupPieces()
    {
        for(int i=0;i<pieces.Length;i++)if(pieces[i]!=null){ReleaseSprite(pieces[i].GetComponent<SpriteRenderer>().sprite);pieces[i]=null;pieceRenderers[i]=null;}
        if(remainsRoot!=null){remainsRoot.SetActive(false);Destroy(remainsRoot);remainsRoot=null;}
    }
    private static void ReleaseSprite(Sprite sprite)
    {
        if(sprite==null)return;var texture=sprite.texture;
        if(Application.isPlaying){Destroy(sprite);Destroy(texture);}else{DestroyImmediate(sprite);DestroyImmediate(texture);}
    }
    private void OnDestroy(){CleanupPieces();foreach(var sprite in sprites)ReleaseSprite(sprite);}
    private void OnDrawGizmos()
    {
        if(Application.isPlaying)return;
        var renderer=GetComponent<SpriteRenderer>();
        if(renderer!=null){renderer.sprite=GetPreviewSprite();renderer.color=Color.white;}
        var matrix=Gizmos.matrix;var color=Gizmos.color;Gizmos.matrix=transform.localToWorldMatrix;
        Gizmos.color=new Color(.35f,.9f,1,.8f);Gizmos.DrawWireCube(new Vector3(0,.375f,0),new Vector3(1,1.25f,0));
        Gizmos.matrix=matrix;Gizmos.color=color;
    }
    protected override void OnValidate()
    {
        base.OnValidate();scatterDuration=Mathf.Max(.1f,scatterDuration);revivalDelay=Mathf.Max(0,revivalDelay);
        assembleDuration=Mathf.Max(.1f,assembleDuration);scatterDistance=Mathf.Max(.1f,scatterDistance);
    }
}
