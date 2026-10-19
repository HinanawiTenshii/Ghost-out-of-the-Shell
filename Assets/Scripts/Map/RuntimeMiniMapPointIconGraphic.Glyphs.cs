using UnityEngine;
using UnityEngine.UI;

public sealed partial class RuntimeMiniMapPointIconGraphic
{
    // Compact solid pictograms on a common dark diamond badge. No texture or
    // per-frame sprite generation; the same UI mesh code serves both maps.
    private static bool DrawMechanismGlyph(VertexHelper h, MapPointIconShape shape, Vector2 c, float r, Color tint)
    {
        switch (shape)
        {
            // Legacy saves lack lock type. Use a neutral padlock until the
            // corresponding scene's door publishes its actual configuration.
            case MapPointIconShape.LockedDoor:
                AddFan(h,c+new Vector2(0,.43f)*r,.5f*r,12,tint,0);
                AddFan(h,c+new Vector2(0,.43f)*r,.28f*r,12,new Color32(15,33,60,255),0);
                Box(h,c,r,0,-.32f,.66f,.52f,tint);
                Box(h,c,r,0,-.32f,.11f,.22f,new Color32(15,33,60,255));
                return true;
            case MapPointIconShape.KeyLockedDoor:
                Stroke(h,c,r,-.24f,.18f,.63f,-.69f,.3f,tint);
                Stroke(h,c,r,.63f,-.69f,.9f,-.42f,.25f,tint);
                Stroke(h,c,r,.31f,-.37f,.58f,-.1f,.25f,tint);
                AddFan(h,c+new Vector2(-.4f,.4f)*r,.48f*r,12,tint,0);
                AddFan(h,c+new Vector2(-.4f,.4f)*r,.22f*r,12,new Color32(15,33,60,255),0);
                return true;
            case MapPointIconShape.PasswordLockedDoor:
                Box(h,c,r,0,0,.65f,.87f,tint);
                Box(h,c,r,0,0,.45f,.65f,new Color32(15,33,60,255));
                Box(h,c,r,0,.43f,.34f,.12f,tint);
                Box(h,c,r,-.24f,-.08f,.13f,.13f,tint); Box(h,c,r,.24f,-.08f,.13f,.13f,tint);
                Box(h,c,r,-.24f,-.48f,.13f,.13f,tint); Box(h,c,r,.24f,-.48f,.13f,.13f,tint);
                return true;
            case MapPointIconShape.SceneTransition:
                Box(h,c,r,-.65f,0,.18f,1.25f,tint);
                Box(h,c,r,-.12f,1.08f,.65f,.17f,tint);
                Box(h,c,r,-.12f,-1.08f,.65f,.17f,tint);
                Box(h,c,r,.2f,0,.65f,.22f,tint);
                AddPolygon(h,c,new[]{new Vector2(.6f,.65f)*r,new Vector2(1.3f,0)*r,new Vector2(.6f,-.65f)*r},tint);
                return true;
            case MapPointIconShape.Lever:
                Box(h,c,r,0,-.67f,.9f,.24f,tint);
                Stroke(h,c,r,-.24f,-.5f,.45f,.6f,.36f,tint);
                AddFan(h,c+new Vector2(.49f,.68f)*r,.33f*r,8,tint,0);
                return true;
            case MapPointIconShape.ElectricGate:
                Box(h,c,r,-.78f,0,.16f,.75f,tint); Box(h,c,r,.78f,0,.16f,.75f,tint);
                Stroke(h,c,r,.18f,.9f,-.3f,.02f,.29f,tint);
                Stroke(h,c,r,-.3f,.02f,.3f,.02f,.29f,tint);
                Stroke(h,c,r,.3f,.02f,-.18f,-.9f,.29f,tint);
                return true;
            case MapPointIconShape.Cauldron:
                AddPolygon(h,c,new[] {new Vector2(-.85f,.25f)*r,new Vector2(-.66f,-.65f)*r,
                    new Vector2(0,-.88f)*r,new Vector2(.66f,-.65f)*r,new Vector2(.85f,.25f)*r},tint);
                Box(h,c,r,0,.33f,.98f,.13f,tint);
                Box(h,c,r,-.55f,-.86f,.13f,.19f,tint); Box(h,c,r,.55f,-.86f,.13f,.19f,tint);
                AddFan(h,c+new Vector2(-.34f,.82f)*r,.2f*r,8,tint,0);
                AddFan(h,c+new Vector2(.3f,.66f)*r,.14f*r,8,tint,0);
                return true;
            case MapPointIconShape.BatterySocket:
                Box(h,c,r,0,0,.65f,.82f,tint); Box(h,c,r,0,.94f,.28f,.14f,tint);
                Box(h,c,r,0,0,.4f,.59f,new Color32(15,33,60,255));
                Box(h,c,r,0,.25f,.27f,.11f,tint); Box(h,c,r,0,.25f,.1f,.25f,tint);
                Box(h,c,r,0,-.33f,.27f,.11f,tint);
                return true;
            case MapPointIconShape.WaterwayGate:
                Box(h,c,r,0,.76f,.95f,.15f,tint);
                for (int i=-1;i<=1;i++) Box(h,c,r,i*.62f,.08f,.12f,.65f,tint);
                Stroke(h,c,r,-.95f,-.64f,-.45f,-.9f,.18f,tint);
                Stroke(h,c,r,-.45f,-.9f,.15f,-.64f,.18f,tint);
                Stroke(h,c,r,.15f,-.64f,.65f,-.9f,.18f,tint);
                Stroke(h,c,r,.65f,-.9f,.95f,-.73f,.18f,tint);
                return true;
            case MapPointIconShape.WaterwayController:
                AddFan(h,c,.87f*r,16,tint,0);
                AddFan(h,c,.61f*r,16,new Color32(15,33,60,255),0);
                Box(h,c,r,0,0,.95f,.13f,tint); Box(h,c,r,0,0,.13f,.95f,tint);
                AddFan(h,c,.25f*r,8,tint,0);
                return true;
            case MapPointIconShape.Ballista:
                Stroke(h,c,r,-.95f,.15f,-.52f,.5f,.22f,tint);
                Stroke(h,c,r,-.52f,.5f,.52f,.5f,.22f,tint);
                Stroke(h,c,r,.52f,.5f,.95f,.15f,.22f,tint);
                Box(h,c,r,0,-.25f,.14f,.65f,tint);
                AddPolygon(h,c+new Vector2(0,.6f)*r,new[]{new Vector2(-.35f,0)*r,new Vector2(0,.5f)*r,new Vector2(.35f,0)*r},tint);
                Stroke(h,c,r,-.45f,-.8f,0,-.42f,.18f,tint); Stroke(h,c,r,0,-.42f,.45f,-.8f,.18f,tint);
                return true;
            case MapPointIconShape.BreakableGlass:
                Box(h,c,r,0,.8f,.75f,.12f,tint); Box(h,c,r,0,-.8f,.75f,.12f,tint);
                Box(h,c,r,-.75f,0,.12f,.8f,tint); Box(h,c,r,.75f,0,.12f,.8f,tint);
                Stroke(h,c,r,.12f,.8f,-.25f,.18f,.18f,tint);
                Stroke(h,c,r,-.25f,.18f,.26f,-.13f,.18f,tint);
                Stroke(h,c,r,.26f,-.13f,-.12f,-.8f,.18f,tint);
                Stroke(h,c,r,-.25f,.18f,-.75f,-.13f,.15f,tint);
                return true;
            default: return false;
        }
    }

    private static void Box(VertexHelper h, Vector2 c, float r, float x, float y, float w, float height, Color tint)
        => AddQuad(h,c+new Vector2(x,y)*r,new Vector2(w,height)*r,tint);

    private static void Stroke(VertexHelper h, Vector2 c, float r, float ax, float ay, float bx, float by, float width, Color tint)
    {
        Vector2 a=new Vector2(ax,ay)*r, b=new Vector2(bx,by)*r;
        Vector2 d=(b-a).normalized, n=new Vector2(-d.y,d.x)*width*r*.5f;
        AddPolygon(h,c+(a+b)*.5f,new[]{(a-b)*.5f+n,(b-a)*.5f+n,(b-a)*.5f-n,(a-b)*.5f-n},tint);
    }
}
