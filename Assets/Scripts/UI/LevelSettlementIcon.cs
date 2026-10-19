using UnityEngine;
using UnityEngine.UI;

/// <summary>Small, solid pixel silhouettes rather than tiny detailed menu illustrations.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LevelSettlementIcon : MaskableGraphic
{
    public int kind;
    public Color backingColor = new Color32(8,24,43,255);
    private static readonly string[][] Patterns = {
        new[]{"................","................","............##..","...........##...","..........##....",".........##.....","..##....##......","...##..##.......","....####........",".....##.........","................","................","................","................","................","................"},
        new[]{"................","..##............","...##...........","....##..........","....#######.....","..###.##..###...",".##..#####..##..","##...#####...##.",".##..#####..##..","..###..##.###...","....#######.....",".........##.....","..........##....","...........##...","................","................"},
        // The reference's round head and open shoulder outline, without a numeral or legs.
        new[]{"................","......####......",".....##..##.....","....##....##....","....##....##....",".....##..##.....","......####......","................",".....######.....","...###....###...","..##........##..","..##........##..",".##..........##.",".##..........##.",".##..........##.","................"}
    };
    private static readonly string[] PersonSilhouette = {
        "................","......####......",".....######.....","....########....",
        "....########....",".....######.....","......####......","................",
        ".....######.....","...##########...","..############..","..############..",
        ".##############.",".##############.",".##############.","................"
    };
    protected override void Awake() { base.Awake();raycastTarget=false; }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float unit=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/16f;
        Vector2 center=rectTransform.rect.center;
        if(kind==3)
        {
            // Reuse the exact other two glyphs, with the eye set back/up and the
            // same bust in front. Occlude its interior so the eye cannot read as
            // an eye painted on the person's face or chest.
            Color rear=color;rear.r*=.65f;rear.g*=.65f;rear.b*=.65f;
            DrawPattern(vh,Patterns[1],unit*.84f,center+new Vector2(1.2f,1.2f)*unit,rear);
            Vector2 foreground=center+new Vector2(-1.5f,-1.4f)*unit;
            Color mask=backingColor;mask.a*=color.a;
            DrawPattern(vh,PersonSilhouette,unit*.82f,foreground,mask);
            DrawPattern(vh,Patterns[2],unit*.82f,foreground,color);
            return;
        }
        DrawPattern(vh,Patterns[Mathf.Clamp(kind,0,Patterns.Length-1)],unit,center,color);
    }
    private static void DrawPattern(VertexHelper vh,string[] pattern,float unit,Vector2 center,Color tint)
    {
        for(int y=0;y<16;y++)for(int x=0;x<pattern[y].Length;x++)if(pattern[y][x]=='#')
        {
            Vector2 a=center+new Vector2(x-8,7-y)*unit;int n=vh.currentVertCount;
            vh.AddVert(a,tint,Vector2.zero);vh.AddVert(a+Vector2.up*unit,tint,Vector2.zero);
            vh.AddVert(a+Vector2.one*unit,tint,Vector2.zero);vh.AddVert(a+Vector2.right*unit,tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
