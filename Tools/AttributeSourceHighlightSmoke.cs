using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AttributeSourceHighlightSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static string error;
    static AttributeSourceHighlightSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "attribute-highlight-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(AttributeSourceHighlightSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(AttributeSourceHighlightSmoke), false))
            {
                SessionState.SetBool(nameof(AttributeSourceHighlightSmoke), false);
                Application.logMessageReceived += (m,s,t) => { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) error = m; };
                suite = Verify();
            }
            if (suite == null) return;
            if (error != null) throw new Exception(error);
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("ATTRIBUTE_SOURCE_HIGHLIGHT_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name,F).GetValue(o);
    static object Call(object o, string name, params object[] a) => o.GetType().GetMethod(name,F).Invoke(o,a);
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var cam = new GameObject("Highlight test camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera"; cam.transform.position = new Vector3(0,0,-10); cam.orthographic = true; cam.orthographicSize = 4;
        var actor = new GameObject("Highlight actor").AddComponent<SwordZeldaCharacterData>();
        actor.gameObject.AddComponent<ZeldaFourWayMover>().enabled = false;
        var window = actor.GetComponentInChildren<ZeldaCharacterStatsWindow>();
        window.enabled = false;
        window.Configure(actor, new Vector2(0,1), 2.2f);
        var effect = Field<AttributeSourceHighlight>(window,"sourceHighlight");
        Check(Field<GameObject>(effect,"root") == null, "Hidden characters allocate no highlight canvas");
        var source = Array.Find(actor.GetComponentsInChildren<SpriteRenderer>(true), r => r.enabled && r.sprite != null);
        Check(source != null, "Character's live visual renderer exists");
        var material = source.sharedMaterial; var color = source.color; int layer = source.sortingLayerID, order = source.sortingOrder;
        Call(window,"UpdateWindow",true,.12f);
        var root = Field<GameObject>(effect,"root");
        Check(root != null && root.activeSelf, "Character highlights on opening");
        Check(root.GetComponent<Canvas>().sortingOrder == 30001, "Highlight between dimmer and attributes");
        Check(source.color == color && source.sharedMaterial == material && source.sortingLayerID == layer && source.sortingOrder == order, "Opening never edits gameplay renderer");
        var graphics = Field<List<AttributeHighlightGraphic>>(effect,"graphics");
        int sourceIndex = Field<List<SpriteRenderer>>(effect,"sources").IndexOf(source);
        var sourceGraphic = graphics[sourceIndex];
        Check(graphics.Count > 0 && graphics[0].material.shader.isSupported, "Shared shader available in player resources");
        Check(AttributeHighlightGraphic.SharedMaterial != null && sourceGraphic.material.shader.name == "UI/Attribute Source White Highlight", "Highlight uses its own shader, not the default fallback");
        source.color = new Color(.6f,.1f,.1f,.5f); source.flipX = true;
        Call(window,"UpdateWindow",true,.016f);
        Check(sourceGraphic.color == source.color, "Damage tint and semi-transparency followed live");
        Check(sourceGraphic.rectTransform.localScale.x == -1, "Facing flip followed");
        Call(window,"UpdateWindow",false,.12f);
        Check(!root.activeSelf && source.color.a == .5f && source.color.r == .6f, "Closing restores no stale source color");
        for (int i=0;i<60;i++) Call(window,"UpdateWindow",false,.016f);
        Check(Field<GameObject>(effect,"root") == root, "Hidden updates reuse cached overlay");
        Call(window,"UpdateWindow",true,.12f);
        window.enabled = true;
        actor.gameObject.SetActive(false);
        Check(!root.activeSelf, "Streaming hides the copy through OnDisable");
        actor.gameObject.SetActive(true);
        window.enabled = false;

        var prop = new GameObject("Requirement prop", typeof(SpriteRenderer));
        var propSprite = prop.GetComponent<SpriteRenderer>(); propSprite.sprite = source.sprite;
        propSprite.color = new Color(.12f,.3f,.7f,.65f); propSprite.sortingOrder = 35;
        var requirement = ZeldaRequirementWindow.AttachSkillRequirement(propSprite,3);
        requirement.enabled = false;
        Call(requirement,"SetVisible",true,true); Call(requirement,"UpdateFade",.12f);
        var propEffect = Field<AttributeSourceHighlight>(requirement,"sourceHighlight");
        Check(Field<GameObject>(propEffect,"root").activeSelf, "Object requirement uses same white highlight");
        Check(propSprite.sortingOrder == 35 && propSprite.color.a == .65f, "Object ordering and alpha are unchanged");
        Call(requirement,"SetVisible",false,true); Call(requirement,"UpdateFade",.12f);
        Check(!Field<GameObject>(propEffect,"root").activeSelf, "Object highlight fades out");

        RenderPreview(source.sprite);
        Object.Destroy(actor.gameObject); Object.Destroy(prop);
        yield return null;
        Check(root == null && Field<GameObject>(propEffect,"root") == null, "Destroy releases both overlay roots");
        Check(!ShaderUtil.ShaderHasError(AttributeHighlightGraphic.SharedMaterial.shader), "Shader compiles without errors");
    }
    static void RenderPreview(Sprite actorSprite)
    {
        var cam = new GameObject("Highlight preview camera", typeof(Camera)).GetComponent<Camera>();
        cam.transform.position = new Vector3(0,0,-10); cam.orthographic = true; cam.orthographicSize = 1.5f;
        cam.backgroundColor = new Color(.04f,.07f,.12f); cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << 31;
        var go = new GameObject("Preview canvas",typeof(RectTransform),typeof(Canvas)); go.layer = 31;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(600,300); rect.localScale = Vector3.one * .01f;
        rect.position = Vector3.zero; rect.rotation = Quaternion.identity;
        var before = new GameObject("Before",typeof(RectTransform),typeof(Image)); before.layer=31; before.transform.SetParent(rect,false);
        var image = before.GetComponent<Image>(); image.sprite=actorSprite; image.rectTransform.sizeDelta = new Vector2(90,144); image.rectTransform.anchoredPosition=new Vector2(-125,0);
        var after = new GameObject("After",typeof(RectTransform),typeof(AttributeHighlightGraphic)); after.layer=31; after.transform.SetParent(rect,false);
        var graphic = after.GetComponent<AttributeHighlightGraphic>(); graphic.material=AttributeHighlightGraphic.SharedMaterial;
        graphic.SetSource(actorSprite,Color.white); graphic.rectTransform.sizeDelta=new Vector2(90,144); graphic.rectTransform.anchoredPosition=new Vector2(125,0);
        var rt = new RenderTexture(960,480,24); cam.targetTexture=rt;
        Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active=rt;
        Debug.Log("HIGHLIGHT_MATERIAL shared=" + (AttributeHighlightGraphic.SharedMaterial != null ? AttributeHighlightGraphic.SharedMaterial.shader.name : "NULL") +
            " graphic=" + graphic.material.shader.name + " rendering=" + graphic.materialForRendering.shader.name + " renderer=" + graphic.canvasRenderer.GetMaterial(0).shader.name);
        var tex = new Texture2D(960,480,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,960,480),0,0);tex.Apply();
        long brightnessBefore=0,brightnessAfter=0;
        int footprintBefore=0,footprintAfter=0,mismatch=0;
        var pixels=tex.GetPixels32();
        Color32 background=pixels[0];
        for(int y=100;y<380;y++) for(int x=200;x<360;x++)
        {
            var a=pixels[y*960+x];var b=pixels[y*960+x+400];
            bool insideA=Mathf.Abs(a.r-background.r)+Mathf.Abs(a.g-background.g)+Mathf.Abs(a.b-background.b)>6;
            bool insideB=Mathf.Abs(b.r-background.r)+Mathf.Abs(b.g-background.g)+Mathf.Abs(b.b-background.b)>6;
            if(insideA){footprintBefore++;brightnessBefore+=a.r+a.g+a.b;}
            if(insideB){footprintAfter++;brightnessAfter+=b.r+b.g+b.b;}
            if(insideA!=insideB)mismatch++;
        }
        System.IO.File.WriteAllBytes(Application.dataPath + "/../AttributeHighlightComparison.png",tex.EncodeToPNG());
        Check(footprintBefore>100, "Preview has a visible source sprite");
        Check(mismatch==0 && footprintAfter==footprintBefore, "Highlighted silhouette must match original pixel-for-pixel: " + mismatch);
        Debug.Log("HIGHLIGHT_RENDER footprint="+footprintBefore+" mismatch="+mismatch+" brightnessRatio="+((float)brightnessAfter/brightnessBefore));
        Check(brightnessAfter>brightnessBefore*1.3, "White wash increases brightness without changing size");
        RenderTexture.active=null;cam.targetTexture=null;
        Object.Destroy(tex);Object.Destroy(rt);Object.Destroy(go);Object.Destroy(cam.gameObject);
    }
}
