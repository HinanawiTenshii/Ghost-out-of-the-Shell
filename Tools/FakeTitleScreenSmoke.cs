using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FakeTitleScreenSmoke
{
    static int checks,frame=-1;
    static IEnumerator routine;
    static double deadline;
    static Vector3[] referenceGlyphVertices;
    static readonly string scenePath="Assets/Scenes/FakeTitleScreen.unity";
    static FakeTitleScreenSmoke(){EditorApplication.update+=Tick;}
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance).GetValue(obj);
    static bool OpenMenu(FakeTitleScreenPresentation p)=>(bool)typeof(FakeTitleScreenPresentation).GetMethod("BeginMenuReveal",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(p,null);
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"isolated project");
            PlayerSettings.productName="fake-title-validation";
            var scene=EditorSceneManager.OpenScene(scenePath);
            var presentation=Object.FindObjectOfType<FakeTitleScreenPresentation>();
            Check(presentation!=null,"authored scene presentation");
            Check(Object.FindObjectsOfType<Button>().Length==4,"exactly four buttons");
            Check(Object.FindObjectOfType<TitleScreenController>()==null,"no functional main-menu controller");
            Check(Object.FindObjectsOfType<ZeldaCharacterData>().Length==0,"no gameplay characters");
            Check(Object.FindObjectsOfType<Canvas>().Length==3,"art, crisp menu and independent fade canvases");
            var mainCrt=Camera.main.GetComponent<CRTScreenEffect>();
            Check(Field<bool>(mainCrt,"constrainAspectRatio")&&Mathf.Abs(Field<float>(mainCrt,"presentationAspectRatio")-4f/3f)<.001f,"CRT uses the central 4:3 display");
            var resolve=typeof(CRTScreenEffect).GetMethod("ResolveViewport",BindingFlags.NonPublic|BindingFlags.Instance);
            Check((Vector4)resolve.Invoke(mainCrt,new object[]{1920,1080})==new Vector4(.125f,0,.75f,1),"wide CRT viewport has correct side bars");
            var gameplay=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Main Camera.prefab").GetComponent<CRTScreenEffect>();
            Check(!Field<bool>(gameplay,"constrainAspectRatio")&&(Vector4)resolve.Invoke(gameplay,new object[]{1920,1080})==new Vector4(0,0,1,1),"gameplay CRT keeps its original full-screen coordinates");
            foreach(var b in Object.FindObjectsOfType<Button>())
            {
                Check(b.onClick.GetPersistentEventCount()==0,"no action wired: "+b.name);
                Check(b.interactable==(b.name!="继续游戏"),"authored availability: "+b.name);
                var label=b.GetComponentInChildren<Text>();
                Check(label.fontSize==12&&label.transform.localScale==Vector3.one*3f,"native pixel lettering enlarged three times");
                Check(label is FakeTitlePixelLabel,"glyph rasterization stays at native resolution");
                Check(label.GetComponent<TitleScreenGlow>()==null&&label.GetComponent<Shadow>()!=null,"flat retro lettering, no fluorescent halo");
                Check(label.material.shader.name=="UI/FakeTitlePixelText"&&label.material.shader.isSupported,"font uses supported point-sampling shader");
                Check(label.GetComponentInParent<Canvas>().GetComponent<CRTCanvasBypass>()!=null,"CRT cannot soften menu lettering");
            }
            Check(Field<CanvasGroup>(presentation,"blackCurtain").alpha==0,"edit-mode preview is not black");
            Check(Field<CanvasGroup>(presentation,"menu").alpha==1,"edit-mode menu remains visible");
            var entry=Field<CanvasGroup>(presentation,"enterPrompt");
            Check(entry!=null&&entry.alpha==0&&!entry.blocksRaycasts,"Enter prompt is separately authored and never a mouse button");
            var entryLabel=entry.GetComponentInChildren<FakeTitlePixelLabel>();
            Check(entryLabel.text=="按 ENTER 开始"&&entryLabel.fontSize==12&&entryLabel.material.shader.name=="UI/FakeTitlePixelText","Enter prompt uses matching crisp pixel typography");
            Check(Field<FakeTitleMenuButton[]>(presentation,"menuOptions").Select(x=>x.name).SequenceEqual(new[]{"开始新游戏","继续游戏","游戏设置","退出游戏"}),"menu reveal follows top-to-bottom option order");
            var music=Field<LoopingBackgroundMusicPlayer>(presentation,"music");
            Check(music!=null&&Object.FindObjectsOfType<LoopingBackgroundMusicPlayer>().Length==1,"exactly one scene-local music player");
            Check(AssetDatabase.GetAssetPath(music.MusicClip)=="Assets/BGMS/Domi/AdventuresSpirit.wav"&&Mathf.Abs(music.MusicClip.length-96)<.1f,"previously selected 96-second Adventure's Spirit is assigned");
            Check(!Field<bool>(music,"playOnSceneStart")&&Field<bool>(music,"loop")&&Mathf.Abs(Field<float>(music,"volume")-.18f)<.001f,"music starts with reveal, loops at restrained volume");
            var feedbackSource=Field<AudioSource>(presentation,"feedbackSource");
            Check(Object.FindObjectsOfType<AudioSource>().Length==2&&Object.FindObjectsOfType<AudioListener>().Length==1,"one BGM source, one feedback voice, one listener");
            Check(feedbackSource!=null&&feedbackSource!=music.GetComponent<AudioSource>()&&!feedbackSource.loop&&!feedbackSource.playOnAwake&&feedbackSource.spatialBlend==0,"feedback has a dedicated non-looping 2D source");
            Check(Field<float>(presentation,"feedbackVolume")==.3f,"restrained menu cue gain");
            foreach(var cueName in new[]{"enterSound","confirmSound"})
            {
                var cue=Field<AudioClip>(presentation,cueName);
                Check(cue!=null&&cue.channels==1&&cue.frequency==44100&&Mathf.Abs(cue.length-(cueName=="enterSound"?.28f:.14f))<.001f,"correct short original chiptune cue: "+cueName);
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(cue));
                Check(importer.defaultSampleSettings.loadType==AudioClipLoadType.DecompressOnLoad&&importer.preloadAudioData,"tiny UI cue is preloaded for immediate feedback");
                cue.LoadAudioData();var samples=new float[cue.samples*cue.channels];
                Check(cue.GetData(samples,0)&&samples.Max(x=>Mathf.Abs(x))<.245f,"Unity import preserves quiet synthesis gain, without automatic normalization");
            }
            Check(Mathf.Abs(Field<float>(presentation,"paletteHoldSeconds")-.24f)<.001f&&
                Mathf.Abs(Field<float>(presentation,"starlightDelaySeconds")-.12f)<.001f,"recommended palette and starlight timings");
            VerifyPaletteAssets(presentation);
            foreach(var p in Directory.GetFiles("Assets/Art/UI/FakeTitle","*.png"))
            {
                var i=(TextureImporter)AssetImporter.GetAtPath(p);
                Check(i.filterMode==FilterMode.Point&&!i.mipmapEnabled,"crisp nearest-neighbour sprites");
            }
            var logo=new Texture2D(2,2);logo.LoadImage(File.ReadAllBytes("Assets/Art/UI/FakeTitle/AzureQuest-Wordmark.png"));
            Check(logo.GetPixel(24,4).a==0&&logo.GetPixel(10,69).a==0,"sword and old baked cross removed");
            Check(logo.GetPixel(174,37).g>.6f&&logo.GetPixel(180,37).g>.6f,"T stem joins the crossbar at full width");Object.DestroyImmediate(logo);
            var sky=new Texture2D(2,2);sky.LoadImage(File.ReadAllBytes("Assets/Art/UI/FakeTitle/AzureDawn-320x180.png"));
            var skyColor=sky.GetPixel(160,179);var mountain=sky.GetPixel(50,116);var sun=sky.GetPixel(160,90);
            Check(skyColor.b>skyColor.g*1.5f&&skyColor.g>skyColor.r*2f&&mountain.b>mountain.g+.15f,"sky and mountains use saturated blue, not neutral grey-blue");
            Check(sun.r>.9f&&sun.g>.7f&&sun.b<.7f,"sunrise retains warm gold contrast");
            var frameColors=new Texture2D(2,2);frameColors.LoadImage(File.ReadAllBytes("Assets/Art/UI/FakeTitle/AzureFrame-Palette.png"));
            Check(frameColors.GetPixel(0,210)==skyColor&&frameColors.GetPixel(0,29)==sky.GetPixel(160,0),"4:3 padding palette meets the original art without colour seams");
            Object.DestroyImmediate(frameColors);Object.DestroyImmediate(sky);
            foreach(var feedback in Object.FindObjectsOfType<FakeTitleMenuButton>())
            {
                feedback.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(feedback,null);
                Color normal=Field<Color>(feedback,"normal"),focus=Field<Color>(feedback,"focused");
                Check(normal.b>normal.r+.3f&&normal.g>normal.r+.3f&&focus.b>=focus.g,"menu follows cyan/ice-white palette");
            }
            var stars=Object.FindObjectOfType<FakeTitleStarlight>();
            Check(stars!=null&&Field<Image[]>(stars,"stars").Length==6&&Field<Sprite[]>(stars,"frames").Length==3,"six glints with three authored pixel frames");
            Check(Field<int>(stars,"cycleFrames")==36&&Field<int>(stars,"framesPerSecond")==6,"glints start about once per second, preserving six fps");
            var first=Object.FindObjectsOfType<Button>().Single(b=>b.name=="开始新游戏").GetComponent<FakeTitleMenuButton>();
            first.GetType().GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(first,null);
            first.OnSelect(new BaseEventData(EventSystem.current));
            first.GetType().GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(first,null);
            Capture(1280,720,"fake-title-1280.png");
            Capture(1024,768,"fake-title-4x3.png");
            Capture(1920,1080,"fake-title-1920.png");
            Capture(1920,810,"fake-title-wide.png");
            Capture(900,1200,"fake-title-portrait.png");
            var menuGroup=Field<CanvasGroup>(presentation,"menu");menuGroup.alpha=0;entry.alpha=1;
            Capture(1024,768,"fake-title-enter-4x3.png");
            Capture(1920,1080,"fake-title-enter-wide.png");
            entry.alpha=0;menuGroup.alpha=1;
            foreach(int step in new[]{1,3,6})
            {
                typeof(FakeTitleScreenPresentation).GetMethod("SetMenuRevealStep",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(presentation,new object[]{step});
                Capture(1024,768,"fake-title-menu-step-"+step+".png");
            }
            // Preview the actual baked palettes before CRT, with menu/star sequencing.
            menuGroup.alpha=0;
            var glints=Field<Image[]>(stars,"stars");var enabledStars=glints.Select(s=>s.enabled).ToArray();
            foreach(var glint in glints)glint.enabled=false;
            for(int stage=0;stage<FakeTitleScreenPresentation.PaletteStageCount;stage++)
            {
                typeof(FakeTitleScreenPresentation).GetMethod("ApplyPalette",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(presentation,new object[]{stage});
                Capture(1024,768,"fake-title-poweron-"+(stage+1)+".png");
            }
            menuGroup.alpha=1;
            for(int i=0;i<glints.Length;i++)glints[i].enabled=enabledStars[i];
            SessionState.SetInt("FakeTitleChecks",checks);SessionState.SetBool(nameof(FakeTitleScreenSmoke),true);
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Texture2D ReadSprite(Sprite sprite)
    {
        var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));return texture;
    }
    static void VerifyPaletteAssets(FakeTitleScreenPresentation p)
    {
        var layers=Field<Array>(p,"paletteLayers");Check(layers.Length==3,"art, logo and surround have palette tracks");
        var earlyColors=new HashSet<Color32>();var finalColors=new HashSet<Color32>();
        var consistentMapping=new Dictionary<Color32,Color32>[4];
        for(int i=0;i<4;i++)consistentMapping[i]=new Dictionary<Color32,Color32>();
        foreach(var layer in layers)
        {
            var image=Field<Image>(layer,"image");var sprites=Field<Sprite[]>(layer,"stages");
            Check(sprites.Length==5&&sprites.All(s=>s!=null),"five complete authored palettes");
            Check(image.sprite==sprites[4]&&!AssetDatabase.GetAssetPath(sprites[4]).Contains("PowerOn"),"last palette is the unchanged original asset");
            var original=ReadSprite(sprites[4]);var originalPixels=original.GetPixels32();
            foreach(var color in originalPixels)if(color.a>0)finalColors.Add(color);
            for(int stage=0;stage<4;stage++)
            {
                var texture=ReadSprite(sprites[stage]);var pixels=texture.GetPixels32();
                Check(texture.width==original.width&&texture.height==original.height,"palette keeps image dimensions");
                for(int i=0;i<pixels.Length;i++)
                {
                    Check(pixels[i].a==originalPixels[i].a,"no alpha fade or changing silhouettes");
                    if(pixels[i].a==0)continue;
                    if(stage==0)earlyColors.Add(pixels[i]);
                    if(consistentMapping[stage].TryGetValue(originalPixels[i],out var expected))
                        Check(pixels[i].Equals(expected),"colour mapping agrees across images and surround seams");
                    else consistentMapping[stage].Add(originalPixels[i],pixels[i]);
                }
                if(image.name.Contains("Mountains"))
                {
                    var sun=texture.GetPixel(160,90);
                    Check(stage<2?sun.b>sun.r:sun.r>sun.b,"sun changes hue from navy to gold, not a global brightness multiplier");
                }
                Object.DestroyImmediate(texture);
            }
            Object.DestroyImmediate(original);
        }
        Check(earlyColors.Count<=8&&earlyColors.Count<finalColors.Count/3,"first palette collapses into a small navy bank");
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(nameof(FakeTitleScreenSmoke),false))
            {
                SessionState.SetBool(nameof(FakeTitleScreenSmoke),false);checks=SessionState.GetInt("FakeTitleChecks",0);
                routine=Verify();deadline=EditorApplication.timeSinceStartup+60;
            }
            if(routine==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("test timeout");
            if(!routine.MoveNext()){routine=null;Debug.Log("FAKE_TITLE_PASS checks="+checks);EditorApplication.Exit(0);}
        }
        catch(Exception e){routine=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        var p=Object.FindObjectOfType<FakeTitleScreenPresentation>();
        var curtain=Field<CanvasGroup>(p,"blackCurtain");var menu=Field<CanvasGroup>(p,"menu");
        Check(FakeTitleScreenPresentation.IsOpen,"cursor owner active");
        Check(curtain.alpha>.99f&&!p.IsReady&&!menu.interactable&&menu.alpha==0,"first frame black, menu hidden and input gated");
        var buttons=menu.GetComponentsInChildren<Button>();
        var entry=Field<CanvasGroup>(p,"enterPrompt");
        var music=Field<LoopingBackgroundMusicPlayer>(p,"music");
        var source=music.AudioSource;
        var feedbackSource=Field<AudioSource>(p,"feedbackSource");
        var enterSound=Field<AudioClip>(p,"enterSound");var confirmSound=Field<AudioClip>(p,"confirmSound");
        Check(!OpenMenu(p)&&!p.IsAwaitingEnter,"Enter during initial black/palette reveal cannot skip it");
        Check(!feedbackSource.isPlaying&&feedbackSource.clip==null,"ignored early Enter does not make sound");
        var starlight=Object.FindObjectOfType<FakeTitleStarlight>();var starImages=Field<Image[]>(starlight,"stars");
        while(RetroSceneLoadReveal.IsBlockingInput){Check(curtain.alpha==1,"normal loading bands hidden behind solid black");yield return null;}
        float start=Time.realtimeSinceStartup,stageStarted=start;int last=-1,repeatedFrames=0;
        float hold=Field<float>(p,"paletteHoldSeconds");var levels=new HashSet<int>();
        var layers=Field<Array>(p,"paletteLayers");
        Time.timeScale=0;
        while(!p.IsAwaitingEnter)
        {
            int stage=p.PaletteStage;
            Check(stage>=last,"palette sequence never goes backwards");
            Check(curtain.alpha==(stage<0?1:0),"black curtain never fades; actual sprite colours change");
            if(stage==last)repeatedFrames++;
            else
            {
                if(last>=0)Check(Time.realtimeSinceStartup-stageStarted>=hold-.05f,"each palette visibly holds, no skipped stages");
                stageStarted=Time.realtimeSinceStartup;last=stage;
            }
            if(stage>=0)
            {
                levels.Add(stage);
                foreach(var layer in layers)Check(Field<Image>(layer,"image").sprite==Field<Sprite[]>(layer,"stages")[stage],"all layers switch to the authored palette together");
            }
            Check(menu.alpha==0&&entry.alpha==0&&!p.IsStarlightReady&&starImages.All(s=>!s.enabled),"menu, Enter prompt and stars remain absent during palette reveal");
            Check(!menu.interactable&&!menu.blocksRaycasts,"no interaction during power-on");
            yield return null;
        }
        Check(Time.realtimeSinceStartup-start>=1.25f&&levels.Count==5&&repeatedFrames>5,"all five palettes play in unscaled time before prompt appears");
        Check(curtain.alpha==0&&!curtain.blocksRaycasts&&!menu.interactable&&menu.alpha==0&&entry.alpha==1,"only Enter prompt appears; options remain inaccessible");
        Check(!p.IsStarlightReady,"stars start after the Enter prompt");
        while(!p.IsStarlightReady){Check(starImages.All(s=>!s.enabled),"brief prompt-only lead-in before glints");yield return null;}
        Check(Time.realtimeSinceStartup-start>=1.45f,"complete power-on includes delayed stars");
        Check(source!=null&&source.isPlaying&&source.loop&&source.spatialBlend==0&&source.volume==.18f,"quiet 2D title music plays before Enter, even with timeScale zero");
        var promptLevels=new HashSet<float>();float waiting=Time.unscaledTime+.85f;
        var pe=new PointerEventData(EventSystem.current);
        while(Time.unscaledTime<waiting)
        {
            promptLevels.Add(entry.alpha);
            Check(p.IsAwaitingEnter&&!p.IsReady&&menu.alpha==0&&!menu.blocksRaycasts,"title waits indefinitely rather than opening menu on a timer");
            foreach(var b in buttons)Check(!b.IsInteractable(),"hidden options cannot be interacted with");
            ExecuteEvents.Execute(buttons[0].gameObject,pe,ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(buttons[0].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            Check(EventSystem.current.currentSelectedGameObject==null,"mouse and Submit cannot select invisible options");
            Check(feedbackSource.clip==null,"hidden options and prompt blink are silent");
            yield return null;
        }
        Check(promptLevels.SetEquals(new[]{1f,.4f}),"Enter prompt blinks in two discrete brightness levels");
        float musicBefore=source.time,menuStarted=Time.unscaledTime;
        Check(OpenMenu(p)&&!OpenMenu(p),"Enter opens exactly one menu reveal, repeated calls are ignored");
        Check(feedbackSource.clip==enterSound&&feedbackSource.isPlaying,"opening Enter plays its upward chiptune cue immediately");
        var menuRows=Field<FakeTitleMenuButton[]>(p,"menuOptions");var seenMenuLevels=new HashSet<int>();
        while(!p.IsReady)
        {
            Check(!p.IsAwaitingEnter&&entry.alpha==0&&!menu.interactable&&!menu.blocksRaycasts,"prompt removed and input locked throughout option animation");
            int previousLevel=3;
            foreach(var row in menuRows)
            {
                int level=Field<int>(row,"revealLevel");seenMenuLevels.Add(level);
                Check(level<=previousLevel,"lower options cannot brighten before higher ones");previousLevel=level;
                Check(Field<float>(row,"blinkStarted")<0,"opening Enter does not trigger option confirmation");
            }
            yield return null;
        }
        Check(Time.unscaledTime-menuStarted>=.4f&&Time.unscaledTime-menuStarted<1f&&seenMenuLevels.SetEquals(new[]{0,1,2,3}),"short staggered menu reveal has all dark-to-cyan stages");
        Check(source.isPlaying&&source.time>musicBefore+.2f,"Enter does not restart music");
        Check(feedbackSource.clip==enterSound&&!feedbackSource.isPlaying,"Enter cue ends once, without replaying for each menu step");
        Check(menu.alpha==1&&menu.interactable&&menu.blocksRaycasts&&menuRows.All(r=>Field<int>(r,"revealLevel")==3),"all options reach final palette before navigation is enabled");
        Time.timeScale=1;
        var first=buttons.Single(b=>b.name=="开始新游戏");var disabled=buttons.Single(b=>b.name=="继续游戏");
        var settings=buttons.Single(b=>b.name=="游戏设置");var quit=buttons.Single(b=>b.name=="退出游戏");
        Check(EventSystem.current.currentSelectedGameObject==first.gameObject,"first active option selected after fade");
        Check(!disabled.IsInteractable()&&disabled.navigation.mode==Navigation.Mode.None,"continue cannot be selected");
        Check(first.FindSelectableOnDown()==settings&&settings.FindSelectableOnDown()==quit&&quit.FindSelectableOnDown()==first,"keyboard skips disabled continue and wraps");
        ExecuteEvents.Execute(settings.gameObject,pe,ExecuteEvents.pointerEnterHandler);yield return null;
        Check(feedbackSource.clip==enterSound&&!feedbackSource.isPlaying,"hover changes focus silently");
        Check(EventSystem.current.currentSelectedGameObject==settings.gameObject,"mouse hover selects available option");
        var feedback=settings.GetComponent<FakeTitleMenuButton>();
        Check(Field<GameObject>(feedback,"selector").activeSelf,"hover shows pixel pointer");
        Check(Field<Image>(feedback,"highlight").color.a==0,"selection never brightens the background");
        pe.button=PointerEventData.InputButton.Right;ExecuteEvents.Execute(settings.gameObject,pe,ExecuteEvents.pointerClickHandler);
        Check(!feedbackSource.isPlaying&&feedbackSource.clip==enterSound,"right click is silent");
        pe.button=PointerEventData.InputButton.Left;ExecuteEvents.Execute(settings.gameObject,pe,ExecuteEvents.pointerClickHandler);yield return null;
        Check(feedbackSource.clip==confirmSound&&feedbackSource.isPlaying,"left click plays short confirm cue alongside blink");
        float clickAudioDeadline=Field<float>(p,"nextFeedbackAt");
        for(int i=0;i<20;i++)ExecuteEvents.Execute(settings.gameObject,pe,ExecuteEvents.pointerClickHandler);
        Check(Field<float>(p,"nextFeedbackAt")==clickAudioDeadline&&Object.FindObjectsOfType<AudioSource>().Length==2,"rapid confirmation never stacks or restarts active feedback");
        float clicked=Time.unscaledTime;int visibilityChanges=0;bool wasVisible=Field<Text>(feedback,"label").color.a>0;
        while(Time.unscaledTime-clicked<.6f)
        {
            bool visible=Field<Text>(feedback,"label").color.a>0;
            if(visible!=wasVisible){visibilityChanges++;wasVisible=visible;}
            Check(Field<Image>(feedback,"highlight").color.a==0,"click never lights the hit area");
            yield return null;
        }
        Check(visibilityChanges>=4&&Field<Text>(feedback,"label").color.a==1,"click produces repeated blink and restores visible text");
        Check(!feedbackSource.isPlaying&&Field<float>(p,"nextFeedbackAt")==clickAudioDeadline,"visual blink does not repeatedly play the sound");
        ExecuteEvents.Execute(settings.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        Check(Field<float>(feedback,"blinkStarted")>=clicked,"keyboard confirmation starts same blink");
        Check(feedbackSource.clip==confirmSound&&feedbackSource.isPlaying&&Field<float>(p,"nextFeedbackAt")>clickAudioDeadline,"keyboard confirmation uses same sound as mouse");
        ExecuteEvents.Execute(disabled.gameObject,pe,ExecuteEvents.pointerEnterHandler);yield return null;
        Check(EventSystem.current.currentSelectedGameObject==settings.gameObject,"disabled hover does not steal selection");
        Check(!Field<GameObject>(disabled.GetComponent<FakeTitleMenuButton>(),"selector").activeSelf,"disabled item has no focus glyph");
        float soundWait=Time.unscaledTime+.2f;while(Time.unscaledTime<soundWait)yield return null;
        float lastSound=Field<float>(p,"nextFeedbackAt");
        ExecuteEvents.Execute(disabled.gameObject,pe,ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(disabled.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
        Check(!feedbackSource.isPlaying&&Field<float>(p,"nextFeedbackAt")==lastSound,"disabled Continue is silent for both mouse and keyboard");
        foreach(var b in buttons)b.onClick.Invoke();yield return null;
        Check(SceneManager.GetActiveScene().name=="FakeTitleScreen"&&p.IsReady,"all placeholder clicks leave scene untouched");
        for(int i=0;i<20;i++){yield return null;Check(Cursor.visible&&Cursor.lockState==CursorLockMode.None,"global UI does not hide or flicker cursor");}
        int ticks=0,previous=Field<int>(starlight,"tick");float until=Time.unscaledTime+2.1f;
        var seen=new HashSet<Sprite>();
        while(Time.unscaledTime<until)
        {
            int current=Field<int>(starlight,"tick");if(current!=previous){ticks++;previous=current;}
            foreach(var star in Field<Image[]>(starlight,"stars"))if(star.enabled)seen.Add(star.sprite);
            yield return null;
        }
        Check(ticks>=9&&ticks<=14&&seen.Count>=2,"starlight swaps frames at about six fps rather than interpolating every frame");
        source.time=source.clip.length-.18f;float loopWait=Time.unscaledTime+.65f;
        while(Time.unscaledTime<loopWait)yield return null;
        Check(source.isPlaying&&source.time<2f,"streamed music wraps around the clip end without stopping");
        p.enabled=false;Check(!source.isPlaying,"disabling title presentation stops its music");
        Check(!feedbackSource.isPlaying&&feedbackSource.clip==null,"feedback is stopped and cleared on disable");
        p.enabled=true;yield return null;
        Check(!p.IsReady&&!p.IsStarlightReady&&menu.alpha==0&&curtain.alpha==1,"component restart resets the reveal instead of keeping stale menu state");
        Check(starImages.All(s=>!s.enabled),"component restart also hides previous stars");
        while(!p.IsStarlightReady)yield return null;
        EditorSceneManager.LoadSceneInPlayMode(scenePath,new LoadSceneParameters(LoadSceneMode.Single));yield return null;
        p=Object.FindObjectOfType<FakeTitleScreenPresentation>();
        Check(p!=null&&!p.IsReady&&Field<CanvasGroup>(p,"blackCurtain").alpha>.99f,"reload starts dark again");
        while(!p.IsAwaitingEnter)yield return null;
        Check(Object.FindObjectsOfType<FakeTitleScreenPresentation>().Length==1,"scene reload does not duplicate title");
        Check(Object.FindObjectsOfType<EventSystem>().Length==1,"scene reload has one event system");
        Check(Object.FindObjectsOfType<LoopingBackgroundMusicPlayer>().Length==1&&Object.FindObjectsOfType<AudioSource>().Length==2,"scene reload keeps exactly one BGM and one feedback source");
    }
    static void Capture(int width,int height,string file)
    {
        var camera=Camera.main;var rt=new RenderTexture(width,height,24);
        var canvas=GameObject.Find("Title Canvas").GetComponent<Canvas>();
        camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=2;
        typeof(CanvasScaler).GetMethod("Handle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(canvas.GetComponent<CanvasScaler>(),null);
        // Render CRT art first, then the isolated menu exactly as the game composites it.
        Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
        var content=GameObject.Find("Safe Area 1280 x 720").GetComponent<RectTransform>();
        var corners=new Vector3[4];content.GetWorldCorners(corners);
        foreach(var c in corners){var point=camera.WorldToViewportPoint(c);Check(point.x>=-.01f&&point.x<=1.01f&&point.y>=-.01f&&point.y<=1.01f,"safe area fits "+width+"x"+height);}
        var frameRect=GameObject.Find("Retro Aspect Surround").GetComponent<RectTransform>();frameRect.GetWorldCorners(corners);
        Vector3 a=camera.WorldToViewportPoint(corners[0]),b=camera.WorldToViewportPoint(corners[2]);
        Check(Mathf.Abs((b.x-a.x)*width/((b.y-a.y)*height)-4f/3f)<.001f,"visual frame retains 4:3 at "+width+"x"+height);
        var menu=GameObject.Find("Crisp Menu Canvas").GetComponent<Canvas>();
        var menuCamera=new GameObject("Crisp capture camera").AddComponent<Camera>();
        menuCamera.enabled=false;menuCamera.orthographic=true;menuCamera.orthographicSize=360;
        menuCamera.transform.position=new Vector3(0,0,-10);menuCamera.clearFlags=CameraClearFlags.Depth;
        menuCamera.cullingMask=1<<5;menuCamera.targetTexture=rt;canvas.enabled=false;
        menu.renderMode=RenderMode.ScreenSpaceCamera;menu.worldCamera=menuCamera;menu.planeDistance=2;
        typeof(CanvasScaler).GetMethod("Handle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu.GetComponent<CanvasScaler>(),null);
        Canvas.ForceUpdateCanvases();menuCamera.Render();
        var sample=menu.GetComponentsInChildren<FakeTitlePixelLabel>().Single(t=>t.text=="开始新游戏");
        var glyphs=sample.cachedTextGenerator.verts.Select(v=>v.position).ToArray();
        if(referenceGlyphVertices==null)referenceGlyphVertices=glyphs;
        else Check(referenceGlyphVertices.SequenceEqual(glyphs),"native glyph shapes identical at "+width+"x"+height);
        var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        if(width/(float)height>4f/3f+.01f)
            Check(tex.GetPixel(4,height/2).maxColorComponent<.005f&&tex.GetPixel(width-5,height/2).maxColorComponent<.005f,"wide excess is pure black, not stretched or CRT-tinted");
        if(width/(float)height<4f/3f-.01f)
            Check(tex.GetPixel(width/2,4).maxColorComponent<.005f&&tex.GetPixel(width/2,height-5).maxColorComponent<.005f,"tall excess is pure black");
        const string previews="../../.codex-temp/fake-title-previews";
        Directory.CreateDirectory(previews);File.WriteAllBytes(Path.Combine(previews,file),tex.EncodeToPNG());RenderTexture.active=old;
        canvas.worldCamera=null;canvas.renderMode=RenderMode.ScreenSpaceOverlay;camera.targetTexture=null;
        menu.worldCamera=null;menu.renderMode=RenderMode.ScreenSpaceOverlay;canvas.enabled=true;
        menuCamera.targetTexture=null;Object.DestroyImmediate(menuCamera.gameObject);
        rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}
