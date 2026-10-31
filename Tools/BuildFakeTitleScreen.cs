using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Author in an isolated Unity project, then copy the generated assets/scene back.
// No runtime texture generation, no dependency on the concept image or editor code.
public static class BuildFakeTitleScreen
{
    const string ScenePath = "Assets/Scenes/FakeTitleScreen.unity";
    const string Art = "Assets/Art/UI/FakeTitle";
    static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    sealed class Pixels
    {
        public readonly int w, h;
        public readonly Color32[] data;
        public Pixels(int width, int height) { w=width; h=height; data=new Color32[w*h]; }
        public void Put(int x,int y,Color c) { if(x>=0&&x<w&&y>=0&&y<h)data[x+y*w]=c; }
        public Color Get(int x,int y) => x>=0&&x<w&&y>=0&&y<h ? (Color)data[x+y*w] : Color.clear;
        public void Box(int x,int y,int width,int height,Color c)
        { for(int yy=y;yy<y+height;yy++)for(int xx=x;xx<x+width;xx++)Put(xx,yy,c); }
        public void Disc(int x,int y,int r,Color c)
        { for(int yy=-r;yy<=r;yy++)for(int xx=-r;xx<=r;xx++)if(xx*xx+yy*yy<=r*r)Put(x+xx,y+yy,c); }
        public void Ridge(int[] nodes,Color c)
        {
            for(int i=0;i<nodes.Length-2;i+=2)
                for(int x=nodes[i];x<=nodes[i+2];x++)
                { int y=Mathf.RoundToInt(Mathf.Lerp(nodes[i+1],nodes[i+3],(x-nodes[i])/(float)(nodes[i+2]-nodes[i])));
                  Box(x,0,1,y,c); }
        }
        public Sprite Save(string name)
        {
            string path=Art+"/"+name+".png";
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false);t.SetPixels32(data);t.Apply();
            File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=16;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;
            imp.textureCompression=TextureImporterCompression.Uncompressed;imp.alphaIsTransparency=true;
            imp.wrapMode=TextureWrapMode.Clamp;imp.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
            imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    static Sprite Landscape()
    {
        var p=new Pixels(320,180);
        string[] sky={"c7a477","d2ac78","bdab8c","6d99bc","367fb5","1b64a2","114d8e","0b3e80","093576","082f6f","072b68","062963"};
        for(int y=0;y<180;y++)p.Box(0,y,320,1,C(sky[Mathf.Clamp((y-55)/11,0,sky.Length-1)]));
        // Quiet, flat cloud bands and a deliberately limited dawn palette.
        p.Box(12,129,48,2,C("185798"));p.Box(29,131,39,1,C("185798"));
        p.Box(252,142,48,2,C("124582"));p.Box(267,144,21,1,C("124582"));
        p.Box(18,153,28,1,C("0d3a7a"));p.Box(226,111,64,2,C("286da5"));
        p.Disc(160,80,25,C("ba9765"));p.Disc(160,80,22,C("e5b973"));
        p.Disc(160,80,19,C("ffd583"));
        p.Box(137,70,47,2,C("cda368"));p.Box(140,64,40,2,C("aa8c6a"));
        p.Ridge(new[]{0,89,9,91,26,110,32,106,49,122,56,115,73,92,89,102,111,80,126,85,144,64,161,62,177,69,194,88,207,82,228,108,240,100,260,126,276,112,284,117,305,96,319,102},C("1f60a1"));
        p.Ridge(new[]{0,75,15,85,25,81,42,100,49,94,58,104,74,89,86,93,101,73,119,69,138,53,159,52,179,61,198,69,211,85,224,80,239,99,247,91,261,101,281,81,294,89,311,77,319,82},C("124781"));
        p.Ridge(new[]{0,66,13,70,25,65,40,80,51,77,69,88,81,77,93,81,111,62,122,64,140,46,164,43,182,50,201,61,217,59,239,78,253,74,269,85,286,69,302,75,319,65},C("0b326c"));
        p.Ridge(new[]{0,54,18,59,34,56,45,66,58,61,75,68,96,51,109,54,128,37,150,32,174,33,195,39,220,51,239,48,257,62,270,56,290,62,303,56,319,60},C("062653"));
        p.Ridge(new[]{0,42,19,46,38,42,56,48,77,42,101,32,123,30,143,28,172,27,202,31,223,34,247,43,266,38,284,47,306,43,319,48},C("041c43"));
        p.Ridge(new[]{0,26,21,30,42,26,65,32,85,24,112,21,148,19,178,20,210,23,243,27,272,26,293,32,319,27},C("03152f"));
        // Small angular pines frame the view, not detailed illustration.
        foreach(var tree in new[]{new Vector2Int(18,45),new Vector2Int(27,42),new Vector2Int(294,48),new Vector2Int(305,44)})
        { p.Box(tree.x,tree.y,1,10,C("041c43"));for(int k=0;k<3;k++)
          for(int j=0;j<4;j++)p.Box(tree.x-j,tree.y+12-k*3-j,1+j*2,1,C("041c43")); }
        // A quiet foreground silhouette provides contrast for every menu row.
        p.Ridge(new[]{0,59,20,63,44,58,68,62,92,57,116,61,140,63,160,64,181,63,204,59,224,58,248,63,274,59,297,63,319,59},C("031532"));
        return p.Save("AzureDawn-320x180");
    }

    static readonly Dictionary<char,string[]> Glyphs=new Dictionary<char,string[]> {
      {'A',new[]{"00000100000","00001110000","00001110000","00011011000","00011011000","00110001100","00110001100","00110001100","01100000110","01100000110","01111111110","11000000011","11000000011","11000000011","11000000011","11000000011","11100000111"}},
      {'Z',new[]{"11111111111","11000000111","10000000110","00000001100","00000001100","00000011000","00000110000","00000110000","00001100000","00011000000","00011000000","00110000000","00110000001","01100000001","11000000011","11000000011","11111111111"}},
      {'U',new[]{"11110001111","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","01100000110","00110001100","00111011100","00011111000"}},
      {'R',new[]{"11111111000","01100001100","01100000110","01100000110","01100000110","01100000110","01100001100","01111111000","01100110000","01100011000","01100011000","01100001100","01100001100","01100000110","01100000110","01100000011","11110000111"}},
      {'E',new[]{"11111111111","01100000011","01100000001","01100000001","01100000000","01100001000","01100001000","01111111000","01100001000","01100001000","01100000000","01100000000","01100000001","01100000001","01100000011","01100000011","11111111111"}},
      {'Q',new[]{"00011111000","00110001100","01100000110","11000000011","11000000011","11000000011","11000000011","11000000011","11000000011","11000000011","11000000011","11000110011","11000011011","01100001110","00110001100","00011111110","00000000111"}},
      {'S',new[]{"00111111100","01100001110","11000000010","11000000010","11000000000","11000000000","01100000000","00111100000","00000111100","00000000110","00000000011","00000000011","10000000011","11000000011","11100000110","01110001100","00111111000"}},
      {'T',new[]{"11111111111","11001110011","10001110001","10001110001",
          "00001110000","00001110000","00001110000","00001110000",
          "00001110000","00001110000","00001110000","00001110000",
          "00001110000","00001110000","00001110000","00001110000","00011111000"}}
    };
    static Sprite Wordmark()
    {
        var p=new Pixels(208,88);
        // Wordmark only: no sword silhouette behind A / Q.
        var mask=new Pixels(208,88);
        void Word(string word,int y)
        {
            for(int i=0;i<word.Length;i++)
            {
                var g=Glyphs[word[i]];
                for(int row=0;row<g.Length;row++)for(int col=0;col<g[row].Length;col++)
                    if(g[row][col]=='1')mask.Box(13+i*37+col*3,y+(16-row)*2,3,2,Color.white);
            }
        }
        Word("AZURE",46);Word("QUEST",7);
        // Small dark extrusion, one-pixel outline; banded cyan bevel, no smooth scaling.
        for(int y=0;y<p.h;y++)for(int x=0;x<p.w;x++)if(mask.Get(x,y).a>0)
            p.Box(x-1,y-4,6,6,C("020f29"));
        for(int y=0;y<p.h;y++)for(int x=0;x<p.w;x++)if(mask.Get(x,y).a>0)
        {
            int local=y>=46?y-46:y-7;
            Color fill=local>=25?C("e4fbff"):local>=18?C("b0f1ff"):local>=10?C("5bd8f5"):C("26abd9");
            if(mask.Get(x,y+1).a==0)fill=C("f0fcff");
            else if(mask.Get(x-1,y).a==0)fill=C("baf5ff");
            else if(mask.Get(x+1,y).a==0||mask.Get(x,y-1).a==0)fill=C("13679e");
            p.Put(x,y,fill);
        }
        // Decorative glints now live in a separate low-frame-rate animation.
        return p.Save("AzureQuest-Wordmark");
    }
    static Sprite Selector()
    {
        var p=new Pixels(7,9);var blue=C("75d6f2");
        for(int y=0;y<9;y++)p.Box(0,y,5-Mathf.Abs(y-4),1,blue);
        p.Box(0,2,1,5,C("ddfaff"));return p.Save("MenuPointer");
    }
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.gameObject.layer=5;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);
        r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    static void Stretch(RectTransform r)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Color color,Sprite sprite=null)
    {var r=Rect(name,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=color;image.sprite=sprite;image.raycastTarget=false;return image;}
    static void Ref(Object target,string name,Object value)
    {var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}

    static void InstallCrispMenu(FakeTitleScreenPresentation presentation)
    {
        const string materialPath=Art+"/PixelMenuText.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null)
        {
            material=new Material(Shader.Find("UI/FakeTitlePixelText"));
            AssetDatabase.CreateAsset(material,materialPath);
        }
        var props=new SerializedObject(presentation);
        var group=(CanvasGroup)props.FindProperty("menu").objectReferenceValue;
        var root=presentation.transform;
        var existing=root.Find("Crisp Menu Canvas");
        var menuRect=existing!=null?(RectTransform)existing:Rect("Crisp Menu Canvas",root,Vector2.zero,new Vector2(1280,720));
        if(existing==null)
        {
            var canvas=menuRect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect=true;canvas.sortingOrder=200;
            canvas.sortingLayerID=SortingLayer.layers.OrderBy(l=>l.value).Last().id;
            var scaler=menuRect.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            menuRect.gameObject.AddComponent<GraphicRaycaster>();
            menuRect.gameObject.AddComponent<CRTCanvasBypass>();
        }
        var rect=(RectTransform)group.transform;Vector2 position=rect.anchoredPosition;
        rect.SetParent(menuRect,false);rect.anchoredPosition=position;
        foreach(var label in group.GetComponentsInChildren<Text>(true))
        {
            Text target=label;
            if(!(label is FakeTitlePixelLabel))
            {
                var go=label.gameObject;var font=label.font;int size=label.fontSize;string text=label.text;
                var color=label.color;var alignment=label.alignment;var style=label.fontStyle;
                var horizontal=label.horizontalOverflow;var vertical=label.verticalOverflow;
                bool rich=label.supportRichText;float spacing=label.lineSpacing;
                Object.DestroyImmediate(label);
                target=go.AddComponent<FakeTitlePixelLabel>();target.font=font;target.fontSize=size;target.text=text;
                target.color=color;target.alignment=alignment;target.fontStyle=style;target.lineSpacing=spacing;
                target.horizontalOverflow=horizontal;target.verticalOverflow=vertical;target.supportRichText=rich;
                target.raycastTarget=false;
                Ref(go.GetComponentInParent<FakeTitleMenuButton>(),"label",target);
            }
            target.material=material;
        }
        // Keep the full-screen curtain above both art and the crisp menu.
        var curtain=(CanvasGroup)props.FindProperty("blackCurtain").objectReferenceValue;
        curtain.transform.SetAsLastSibling();
        props.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(material);
    }

    static Sprite[] StarlightSprites()
    {
        var frames=new Sprite[3];
        for(int f=0;f<3;f++)
        {
            var pixels=new Pixels(9,9);var light=C("ddfbff");var edge=C("51c9ed");
            pixels.Put(4,4,light);
            if(f>0){pixels.Box(4,3,1,3,light);pixels.Box(3,4,3,1,light);}
            if(f>1)
            {
                pixels.Box(4,1,1,7,light);pixels.Box(1,4,7,1,light);
                pixels.Put(3,3,edge);pixels.Put(3,5,edge);pixels.Put(5,3,edge);pixels.Put(5,5,edge);
            }
            frames[f]=pixels.Save("Starlight-"+(f+1));
        }
        return frames;
    }

    static void InstallStarlight(FakeTitleScreenPresentation presentation)
    {
        var frames=StarlightSprites();
        var content=presentation.transform.Find("Title Canvas/Safe Area 1280 x 720");
        var old=content.Find("Low Frame Starlight");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var root=Rect("Low Frame Starlight",content,Vector2.zero,new Vector2(1280,720));
        var locations=new[]{new Vector2(-271,222),new Vector2(260,213),new Vector2(-216,33),
            new Vector2(240,47),new Vector2(-65,278),new Vector2(302,107)};
        var stars=new Image[locations.Length];
        for(int i=0;i<stars.Length;i++)
        {
            stars[i]=Image("Star "+(i+1),root,locations[i],new Vector2(27,27),Color.white,frames[2]);
            stars[i].enabled=i==0||i==4; // Two glints in the editor preview.
        }
        var animation=root.gameObject.AddComponent<FakeTitleStarlight>();
        var props=new SerializedObject(animation);
        props.FindProperty("presentation").objectReferenceValue=presentation;
        props.FindProperty("framesPerSecond").intValue=6;
        props.FindProperty("cycleFrames").intValue=36;
        var images=props.FindProperty("stars");images.arraySize=stars.Length;
        for(int i=0;i<stars.Length;i++)images.GetArrayElementAtIndex(i).objectReferenceValue=stars[i];
        var sprites=props.FindProperty("frames");sprites.arraySize=frames.Length;
        for(int i=0;i<frames.Length;i++)sprites.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];
        props.ApplyModifiedPropertiesWithoutUndo();
    }

    static void InstallAspectFrame(FakeTitleScreenPresentation presentation)
    {
        // Match the user's 1024x768 reference without enlarging/cropping the logo:
        // preserve the existing 1280x720 composition inside a 1280x960 frame.
        var root=presentation.transform;
        var crt=Camera.main.GetComponent<CRTScreenEffect>();var effect=new SerializedObject(crt);
        effect.FindProperty("constrainAspectRatio").boolValue=true;
        effect.FindProperty("presentationAspectRatio").floatValue=4f/3f;
        effect.ApplyModifiedPropertiesWithoutUndo();
        var title=root.Find("Title Canvas");var menu=root.Find("Crisp Menu Canvas");
        foreach(var canvas in new[]{title,menu})
        {
            var scaler=canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution=new Vector2(1280,960);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        }
        var surround=title.Find("Retro Aspect Surround").GetComponent<RectTransform>();
        surround.anchorMin=surround.anchorMax=new Vector2(.5f,.5f);surround.anchoredPosition=Vector2.zero;
        surround.sizeDelta=new Vector2(1280,960);
        var black=title.Find("Full Screen Black Background");
        if(black==null)
        {
            var image=Image("Full Screen Black Background",title,Vector2.zero,Vector2.zero,Color.black);
            Stretch(image.rectTransform);image.transform.SetAsFirstSibling();
        }
        // Matte after CRT, so distortion/glow cannot leak into the pure black bars.
        var old=menu.Find("4x3 Black Bars");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var bars=Rect("4x3 Black Bars",menu,Vector2.zero,Vector2.zero);Stretch(bars);
        var left=Image("Left",bars,Vector2.zero,Vector2.zero,Color.black).rectTransform;
        left.anchorMin=Vector2.zero;left.anchorMax=new Vector2(.5f,1);left.offsetMin=Vector2.zero;left.offsetMax=new Vector2(-640,0);
        var right=Image("Right",bars,Vector2.zero,Vector2.zero,Color.black).rectTransform;
        right.anchorMin=new Vector2(.5f,0);right.anchorMax=Vector2.one;right.offsetMin=new Vector2(640,0);right.offsetMax=Vector2.zero;
        var bottom=Image("Bottom",bars,Vector2.zero,Vector2.zero,Color.black).rectTransform;
        bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,.5f);bottom.offsetMin=Vector2.zero;bottom.offsetMax=new Vector2(0,-480);
        var top=Image("Top",bars,Vector2.zero,Vector2.zero,Color.black).rectTransform;
        top.anchorMin=new Vector2(0,.5f);top.anchorMax=Vector2.one;top.offsetMin=new Vector2(0,480);top.offsetMax=Vector2.zero;
    }

    static void ApplyPalette(FakeTitleScreenPresentation presentation)
    {
        // Extend the sky/foreground colour through the 4:3 padding without a
        // hard grey-blue seam. This does not change any content position or size.
        var padding=new Pixels(1,240);
        for(int y=0;y<240;y++)
        {
            string color=y<10?"020e24":y<20?"02112b":y<210?"031532":y<220?"062963":y<230?"06265f":"05225a";
            padding.Put(0,y,C(color));
        }
        var surround=presentation.transform.Find("Title Canvas/Retro Aspect Surround").GetComponent<Image>();
        surround.sprite=padding.Save("AzureFrame-Palette");surround.color=Color.white;
        foreach(var feedback in presentation.GetComponentsInChildren<FakeTitleMenuButton>(true))
        {
            var settings=new SerializedObject(feedback);
            settings.FindProperty("normal").colorValue=C("75d6f2");
            settings.FindProperty("focused").colorValue=C("ddfaff");
            settings.FindProperty("disabled").colorValue=C("3e6285");
            var label=(Text)settings.FindProperty("label").objectReferenceValue;
            settings.ApplyModifiedPropertiesWithoutUndo();
            label.color=feedback.GetComponent<Button>().interactable?C("75d6f2"):C("3e6285");
        }
    }

    // Explicit colour ramps, not a global RGB multiplier. Early palettes merge
    // many entries into a few navy colours; gold and ice-white arrive later.
    // The fifth stage always references the untouched original sprite.
    static Dictionary<Color32,Color32[]> PowerOnPalettes()
    {
        var map=new Dictionary<Color32,Color32[]>();
        void Ramp(string source,string a,string b,string c,string d)
        {map.Add((Color32)C(source),new[]{(Color32)C(a),(Color32)C(b),(Color32)C(c),(Color32)C(d)});}
        // Navy sky, foreground and 4:3 surround.
        Ramp("020e24","00020a","010610","010a19","020c20");
        Ramp("02112b","00020a","010813","010c1f","020f26");
        Ramp("031532","010610","020b1a","021027","03132e");
        Ramp("03152f","010610","020b1a","021025","03132c");
        Ramp("041c43","010610","020e21","031731","041b3d");
        Ramp("062653","020b1a","031331","041d40","05234c");
        Ramp("05225a","020b1a","031331","041b43","052052");
        Ramp("06265f","020b1a","031331","041e47","052358");
        Ramp("062963","020b1a","031331","041e49","05265b");
        Ramp("072b68","020b1a","031735","05214e","062760");
        Ramp("082f6f","020b1a","031735","052451","072b66");
        Ramp("093576","020b1a","041c3d","06295a","08316c");
        Ramp("0b3e80","031331","052244","08315f","0a3875");
        Ramp("114d8e","031331","082949","0c3967","104680");
        Ramp("1b64a2","051c40","0b3155","124b7c","185b93");
        Ramp("367fb5","051c40","113a61","1e5e8c","2d74a7");
        Ramp("6d99bc","09274b","19446a","3a749c","598eaf");
        // Clouds and mountain silhouettes.
        Ramp("0d3a7a","031331","052044","082e5b","0b3570");
        Ramp("124582","031331","082544","0c335e","103e76");
        Ramp("185798","051c40","0b2b4c","104274","164e8a");
        Ramp("286da5","051c40","0e3458","1a5180","226397");
        Ramp("1f60a1","051c40","092447","0d3d6f","17538d");
        Ramp("124781","031331","062044","0a315c","103e74");
        Ramp("0b326c","020b1a","041b3b","06274f","092e61");
        // Sunrise starts blue, passes through muted bronze, then becomes gold.
        Ramp("c7a477","09274b","233e5a","716354","aa8a63");
        Ramp("d2ac78","09274b","28435d","826b51","ba985f");
        Ramp("bdab8c","09274b","26455e","75766c","a69879");
        Ramp("ba9765","051c40","1a324d","64523c","9b774b");
        Ramp("e5b973","09274b","28435d","926f45","c99d57");
        Ramp("ffd583","09274b","2b4c65","a58050","e8b963");
        Ramp("cda368","09274b","233e5a","7a5e3d","b48b4f");
        Ramp("aa8c6a","051c40","1a324d","5a503f","8f714e");
        // Title bevel: navy -> blue -> cyan -> pale cyan -> original ice-white.
        Ramp("020f29","00020a","010813","010c1d","020e24");
        Ramp("13679e","031331","0b294d","10496e","125a8c");
        Ramp("26abd9","051c40","104170","197fa7","22a0c9");
        Ramp("5bd8f5","09274b","155384","349dbb","50c6dc");
        Ramp("b0f1ff","09274b","195a8b","51a7c9","8ed9ea");
        Ramp("baf5ff","09274b","195a8b","55aed0","97e0f0");
        Ramp("e4fbff","09274b","206594","68b5d5","a4e4f2");
        Ramp("f0fcff","09274b","206594","68b5d5","a4e4f2");
        return map;
    }

    static void InstallPaletteReveal(FakeTitleScreenPresentation presentation)
    {
        var ramps=PowerOnPalettes();
        string[] sources={"AzureDawn-320x180","AzureQuest-Wordmark","AzureFrame-Palette"};
        var properties=new SerializedObject(presentation);
        var layers=properties.FindProperty("paletteLayers");layers.arraySize=sources.Length;
        for(int i=0;i<sources.Length;i++)
        {
            string path=Art+"/"+sources[i]+".png";
            var original=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var image=presentation.GetComponentsInChildren<Image>(true).Single(x=>x.sprite==original);
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
            var pixels=texture.GetPixels32();
            var layer=layers.GetArrayElementAtIndex(i);
            layer.FindPropertyRelative("image").objectReferenceValue=image;
            var stages=layer.FindPropertyRelative("stages");stages.arraySize=FakeTitleScreenPresentation.PaletteStageCount;
            for(int stage=0;stage<FakeTitleScreenPresentation.PaletteStageCount-1;stage++)
            {
                var frame=new Pixels(texture.width,texture.height);
                for(int p=0;p<pixels.Length;p++)
                {
                    Color32 source=pixels[p];
                    if(source.a==0){frame.data[p]=source;continue;}
                    if(!ramps.TryGetValue(source,out var ramp))
                        throw new Exception("Missing power-on palette entry: "+ColorUtility.ToHtmlStringRGBA(source));
                    var colour=ramp[stage];colour.a=source.a;frame.data[p]=colour;
                }
                stages.GetArrayElementAtIndex(stage).objectReferenceValue=frame.Save(sources[i]+"-PowerOn-"+(stage+1));
            }
            stages.GetArrayElementAtIndex(FakeTitleScreenPresentation.PaletteStageCount-1).objectReferenceValue=original;
            Object.DestroyImmediate(texture);
        }
        properties.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void UpgradePowerOn()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Use isolated clone");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            var rects=presentation.GetComponentsInChildren<RectTransform>(true).Where(r=>r.GetComponent<Canvas>()==null).ToArray();
            var poses=rects.ToDictionary(r=>r.GetInstanceID(),RectPose);
            var cameraSettings=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>());
            InstallPaletteReveal(presentation);
            if(rects.Any(r=>poses[r.GetInstanceID()]!=RectPose(r))||cameraSettings!=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>()))
                throw new Exception("Power-on migration changed layout or CRT settings");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_POWER_ON: five baked colour palettes, then menu, then stars; layout and original art preserved");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    static void InstallTitleEntry(FakeTitleScreenPresentation presentation)
    {
        var properties=new SerializedObject(presentation);
        var menu=(CanvasGroup)properties.FindProperty("menu").objectReferenceValue;
        var options=menu.GetComponentsInChildren<FakeTitleMenuButton>(true)
            .OrderByDescending(x=>((RectTransform)x.transform).anchoredPosition.y).ToArray();
        var rows=properties.FindProperty("menuOptions");rows.arraySize=options.Length;
        for(int i=0;i<options.Length;i++)rows.GetArrayElementAtIndex(i).objectReferenceValue=options[i];
        var canvas=presentation.transform.Find("Crisp Menu Canvas");
        var prompt=canvas.Find("Press Enter Prompt");
        if(prompt==null)
        {
            var rect=Rect("Press Enter Prompt",canvas,new Vector2(0,-180),new Vector2(540,48));
            var group=rect.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
            var labelRect=Rect("Label",rect,Vector2.zero,new Vector2(170,14));labelRect.localScale=Vector3.one*3f;
            var label=labelRect.gameObject.AddComponent<FakeTitlePixelLabel>();
            label.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
            label.fontSize=12;label.text="按 ENTER 开始";label.color=C("75d6f2");label.alignment=TextAnchor.MiddleCenter;
            label.material=AssetDatabase.LoadAssetAtPath<Material>(Art+"/PixelMenuText.mat");label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;
            var shadow=label.gameObject.AddComponent<Shadow>();shadow.effectColor=C("050d19");shadow.effectDistance=new Vector2(1,-1);
            rect.SetSiblingIndex(canvas.Find("4x3 Black Bars").GetSiblingIndex());
            prompt=rect;
        }
        properties.FindProperty("enterPrompt").objectReferenceValue=prompt.GetComponent<CanvasGroup>();
        var musicTransform=presentation.transform.Find("Fake Title Music - Adventure's Spirit");
        if(musicTransform==null)
        {
            musicTransform=new GameObject("Fake Title Music - Adventure's Spirit").transform;
            musicTransform.SetParent(presentation.transform,false);
        }
        var source=musicTransform.GetComponent<AudioSource>();
        if(source==null)source=musicTransform.gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=true;source.volume=.18f;source.spatialBlend=0;source.dopplerLevel=0;
        source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BGMS/Domi/AdventuresSpirit.wav");
        if(source.clip==null)throw new Exception("Previously selected Adventure's Spirit title music is missing");
        var player=musicTransform.GetComponent<LoopingBackgroundMusicPlayer>();
        if(player==null)player=musicTransform.gameObject.AddComponent<LoopingBackgroundMusicPlayer>();
        var musicProperties=new SerializedObject(player);
        musicProperties.FindProperty("musicClip").objectReferenceValue=source.clip;
        musicProperties.FindProperty("volume").floatValue=.18f;
        musicProperties.FindProperty("loop").boolValue=true;
        musicProperties.FindProperty("playOnSceneStart").boolValue=false; // Presentation starts it at the first colour stage.
        musicProperties.ApplyModifiedPropertiesWithoutUndo();
        properties.FindProperty("music").objectReferenceValue=player;
        properties.ApplyModifiedPropertiesWithoutUndo();
        InstallMenuSounds(presentation);
    }

    static void InstallMenuSounds(FakeTitleScreenPresentation presentation)
    {
        AudioClip ImportCue(string name)
        {
            string path="Assets/SoundEffect/FakeTitle/"+name+".wav";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer==null)throw new Exception("Generate title UI audio first: "+path);
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.PCM;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;
            importer.forceToMono=true;importer.preloadAudioData=true;importer.loadInBackground=false;
            var audioProperties=new SerializedObject(importer);
            var normalize=audioProperties.FindProperty("m_Normalize")??audioProperties.FindProperty("normalize");
            if(normalize==null)throw new Exception("Cannot disable normalization for quiet title cue");
            normalize.boolValue=false;audioProperties.ApplyModifiedPropertiesWithoutUndo();
            importer.userData="Original synthesis - Tools/GenerateFakeTitleAudio.py; no source recordings.";
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        var enter=ImportCue("TitleEnter");var confirm=ImportCue("TitleConfirm");
        var soundRoot=presentation.transform.Find("Fake Title Menu Feedback");
        if(soundRoot==null)
        {
            soundRoot=new GameObject("Fake Title Menu Feedback").transform;
            soundRoot.SetParent(presentation.transform,false);
        }
        var source=soundRoot.GetComponent<AudioSource>();
        if(source==null)source=soundRoot.gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.volume=.3f;source.spatialBlend=0;
        source.dopplerLevel=0;source.pitch=1;source.reverbZoneMix=0;source.priority=64;source.clip=null;
        var properties=new SerializedObject(presentation);
        properties.FindProperty("feedbackSource").objectReferenceValue=source;
        properties.FindProperty("enterSound").objectReferenceValue=enter;
        properties.FindProperty("confirmSound").objectReferenceValue=confirm;
        properties.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void UpgradeMenuSounds()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Use isolated clone");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            InstallMenuSounds(presentation);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_SOUND: original enter/confirm chiptune cues, quiet single voice separate from BGM");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    public static void UpgradeEntry()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Use isolated clone");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            var rects=presentation.GetComponentsInChildren<RectTransform>(true).Where(r=>r.GetComponent<Canvas>()==null).ToArray();
            var poses=rects.ToDictionary(r=>r.GetInstanceID(),RectPose);
            var cameraSettings=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>());
            InstallTitleEntry(presentation);
            if(rects.Any(r=>poses[r.GetInstanceID()]!=RectPose(r))||cameraSettings!=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>()))
                throw new Exception("Entry migration changed existing layout or CRT settings");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_ENTRY: Adventure's Spirit at 0.18 volume, Enter prompt, six short staggered menu steps");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    // Palette-only update: do not rebuild the scene, animations or input setup.
    static string RectPose(RectTransform r) => r.name+"/"+r.parent.GetInstanceID()+"/"+
        r.anchoredPosition.ToString("F4")+"/"+r.sizeDelta.ToString("F4")+"/"+r.localScale.ToString("F4")+
        "/"+r.anchorMin.ToString("F4")+"/"+r.anchorMax.ToString("F4")+"/"+r.pivot.ToString("F4");
    static Color32[] ReadPixels(string name)
    {
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(Art+"/"+name+".png"));
        var colors=texture.GetPixels32();Object.DestroyImmediate(texture);return colors;
    }
    static void CheckPaletteOnly(Color32[] before,Color32[] after)
    {
        if(before.Length!=after.Length)throw new Exception("Recolor changed image dimensions");
        var mapping=new Dictionary<Color32,Color32>();
        for(int i=0;i<before.Length;i++)
        {
            if(before[i].a!=after[i].a)throw new Exception("Recolor changed image silhouette");
            if(mapping.TryGetValue(before[i],out var expected)&&!expected.Equals(after[i]))
                throw new Exception("Recolor changed image geometry instead of a palette entry");
            mapping[before[i]]=after[i];
        }
    }
    public static void Recolor()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Use isolated clone");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            var rects=presentation.GetComponentsInChildren<RectTransform>(true).Where(r=>r.GetComponent<Canvas>()==null).ToArray();
            var poses=rects.ToDictionary(r=>r.GetInstanceID(),RectPose);
            var cameraSettings=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>());
            var presentationSettings=EditorJsonUtility.ToJson(presentation);
            var beforeSky=ReadPixels("AzureDawn-320x180");var beforeLogo=ReadPixels("AzureQuest-Wordmark");
            Landscape();Wordmark();Selector();StarlightSprites();
            ApplyPalette(presentation);
            InstallPaletteReveal(presentation);
            CheckPaletteOnly(beforeSky,ReadPixels("AzureDawn-320x180"));CheckPaletteOnly(beforeLogo,ReadPixels("AzureQuest-Wordmark"));
            if(rects.Any(r=>poses[r.GetInstanceID()]!=RectPose(r))||cameraSettings!=EditorJsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>())||
                presentationSettings!=EditorJsonUtility.ToJson(presentation))throw new Exception("Palette update modified layout or presentation settings");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_RECOLORED: saturated blue silhouettes, cyan/ice-white UI, warm golden dawn; layout preserved");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    // Focused migration: preserve authored positions, camera settings and hierarchy
    // outside the menu. Never rebuild the entire scene for this revision.
    public static void Refine()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Use isolated clone");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var logo=Wordmark();
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            foreach(var image in presentation.GetComponentsInChildren<Image>(true))
                if(image.sprite!=null&&AssetDatabase.GetAssetPath(image.sprite)==Art+"/AzureQuest-Wordmark.png")
                {image.sprite=logo;image.gameObject.name="AZURE QUEST - Pixel Logo";}
            InstallCrispMenu(presentation);
            InstallStarlight(presentation);
            InstallAspectFrame(presentation);
            ApplyPalette(presentation);
            InstallPaletteReveal(presentation);
            InstallTitleEntry(presentation);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_REFINED: point-sampled menu, five colour palettes, sword removed");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }

    public static void Run()
    {
        try
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Author only in isolated clone");
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            var bg=Landscape();var logo=Wordmark();var pointer=Selector();
            var scene=EditorSceneManager.OpenScene(ScenePath);
            // Re-running only replaces our generated root, preserving the camera/scene GUID.
            foreach(var oldRoot in scene.GetRootGameObjects())if(oldRoot.name=="AZURE QUEST - Fake Title")Object.DestroyImmediate(oldRoot);
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=C("0b182c");camera.allowMSAA=false;
            var crt=camera.GetComponent<CRTScreenEffect>()??camera.gameObject.AddComponent<CRTScreenEffect>();
            var s=new SerializedObject(crt);
            s.FindProperty("crtMaterial").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CRTScreen.mat");
            // Same CRT vocabulary, gentler strength for the already coarse title art.
            var fields=new[]{"curvature","scanlineIntensity","scanlineCount","chromaticAberration","vignette","maskIntensity","noiseIntensity","brightness","contrast","phosphorGlow"};
            var values=new[]{.018f,.13f,180f,.5f,.12f,.035f,0f,1.12f,1.05f,.12f};
            for(int i=0;i<fields.Length;i++)s.FindProperty(fields[i]).floatValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();
            var rootObject=new GameObject("AZURE QUEST - Fake Title");
            var root=rootObject.transform;
            var canvasRect=Rect("Title Canvas",root,Vector2.zero,new Vector2(1280,720));
            var canvas=canvasRect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect=true;canvas.sortingOrder=100;
            var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var surround=Image("Retro Aspect Surround",canvasRect,Vector2.zero,Vector2.zero,C("0c1a2e"));Stretch(surround.rectTransform);
            var content=Rect("Safe Area 1280 x 720",canvasRect,Vector2.zero,new Vector2(1280,720));
            Image("Layered Mountains and Sunrise",content,Vector2.zero,new Vector2(1280,720),Color.white,bg);
            Image("AZURE QUEST - Pixel Logo",content,new Vector2(0,144),new Vector2(624,264),Color.white,logo);
            // Low, flat silhouette under the options; no modern menu card/frame.
            var groupRect=Rect("Menu - presentation only",content,new Vector2(0,-196),new Vector2(320,192));
            var group=groupRect.gameObject.AddComponent<CanvasGroup>();
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
            string[] names={"开始新游戏","继续游戏","游戏设置","退出游戏"};
            var buttons=new Button[4];
            for(int i=0;i<names.Length;i++)
            {
                var image=Image(names[i],groupRect,new Vector2(0,72-48*i),new Vector2(288,40),Color.clear);
                image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();
                button.targetGraphic=image;button.transition=Selectable.Transition.None;button.interactable=i!=1;buttons[i]=button;
                // Render at the font's native twelve-pixel size, then enlarge
                // the complete glyph rather than rendering thin smooth text.
                var labelRect=Rect("Label",image.transform,Vector2.zero,new Vector2(88,14));labelRect.localScale=Vector3.one*3f;
                var text=labelRect.gameObject.AddComponent<FakeTitlePixelLabel>();text.font=font;text.fontSize=12;text.text=names[i];
                text.alignment=TextAnchor.MiddleCenter;text.color=i==1?C("6e7a8a"):C("eee8cc");text.raycastTarget=false;
                text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
                var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=C("050d19");shadow.effectDistance=new Vector2(1,-1);
                var cursor=Image("Pixel Selector",image.transform,new Vector2(-112,0),new Vector2(14,18),Color.white,pointer);
                cursor.gameObject.SetActive(false);
                var feedback=image.gameObject.AddComponent<FakeTitleMenuButton>();
                Ref(feedback,"label",text);Ref(feedback,"highlight",image);Ref(feedback,"selector",cursor.gameObject);
            }
            // Continue is genuinely disabled; keyboard navigation skips it.
            int[] enabled={0,2,3};
            for(int i=0;i<3;i++)buttons[enabled[i]].navigation=new Navigation {mode=Navigation.Mode.Explicit,
                selectOnUp=buttons[enabled[(i+2)%3]],selectOnDown=buttons[enabled[(i+1)%3]]};
            buttons[1].navigation=new Navigation {mode=Navigation.Mode.None};
            var events=new GameObject("Title EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(root);
            var es=events.GetComponent<EventSystem>();es.firstSelectedGameObject=null;
            var overlayRect=Rect("Power-on Black Curtain",root,Vector2.zero,Vector2.zero);
            var overlay=overlayRect.gameObject.AddComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;
            overlay.overrideSorting=true;overlay.sortingOrder=32000;overlay.sortingLayerID=SortingLayer.layers.OrderBy(l=>l.value).Last().id;
            overlay.gameObject.AddComponent<CRTCanvasBypass>();overlay.gameObject.AddComponent<GraphicRaycaster>();
            var curtain=overlay.gameObject.AddComponent<CanvasGroup>();curtain.alpha=0;curtain.blocksRaycasts=false;
            var black=Image("Black",overlayRect,Vector2.zero,Vector2.zero,Color.black);Stretch(black.rectTransform);black.raycastTarget=true;
            // Add last: edit-mode scene is fully visible, first runtime frame is black.
            var presentation=rootObject.AddComponent<FakeTitleScreenPresentation>();
            Ref(presentation,"menu",group);Ref(presentation,"blackCurtain",curtain);Ref(presentation,"firstOption",buttons[0].gameObject);
            InstallCrispMenu(presentation);
            InstallStarlight(presentation);
            InstallAspectFrame(presentation);
            ApplyPalette(presentation);
            InstallPaletteReveal(presentation);
            InstallTitleEntry(presentation);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("FAKE_TITLE_AUTHORED: 320x180 art, four inert menu options, Continue disabled, palette power-on");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
