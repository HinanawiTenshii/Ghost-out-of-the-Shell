using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterSprintVisualSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static IEnumerator suite; static int frame=-1,checks; static double deadline;
    static readonly List<Color32[]> previewFrames = new List<Color32[]>();
    static CharacterSprintVisualSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value,string label) { if(!value)throw new Exception(label); checks++; }
    static FieldInfo Field(object obj,string name)
    {
        for(var t=obj.GetType();t!=null;t=t.BaseType) { var f=t.GetField(name,F);if(f!=null)return f; }
        throw new Exception("Missing "+name);
    }
    static void Set(object obj,string name,object value)=>Field(obj,name).SetValue(obj,value);
    static T Get<T>(object obj,string name)=>(T)Field(obj,name).GetValue(obj);
    static ZeldaCharacterData Spawn(string path)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Check(prefab!=null,"prefab exists: "+path);
        var data=Object.Instantiate(prefab).GetComponent<ZeldaCharacterData>();
        var ai=data.GetComponent<ZeldaCharacterAiBase>();if(ai!=null)ai.enabled=false;
        return data;
    }
    static SpriteRenderer Renderer(ZeldaCharacterData data)=>data.transform.Find("Visual").GetComponent<SpriteRenderer>();
    static void Request(ZeldaCharacterData data,bool sprint,Vector2 direction)
    {
        var mover=data.GetComponent<ZeldaFourWayMover>();mover.enabled=true;
        Set(mover,"isSprinting",sprint);Set(mover,"moveDirection",direction);Set(mover,"facingDirection",direction.sqrMagnitude>0?direction:Vector2.down);
    }
    static void Frame(ZeldaCharacterData data,Vector2 displacement,bool moving=true,bool attacking=false)
    {
        var effect=data.GetComponent<CharacterSprintVisual>();
        if(effect!=null) {Set(effect,"prepareFrame",-1);Set(effect,"clockFrame",-1);effect.RecordMovement(displacement);}
        data.ApplyCharacterVisualWithMovement(Renderer(data),Vector2.down,moving,attacking);
    }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("isolated only");
        PlayerSettings.productName="character-sprint-visual-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool(nameof(CharacterSprintVisualSmoke),true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try {
            if(SessionState.GetBool(nameof(CharacterSprintVisualSmoke),false))
            {SessionState.SetBool(nameof(CharacterSprintVisualSmoke),false);suite=Verify();deadline=EditorApplication.timeSinceStartup+180;}
            if(suite==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Sprint suite timed out");
            if(suite.MoveNext())return;
            suite=null;Debug.Log("CHARACTER_SPRINT_VISUAL_PASS checks="+checks);EditorApplication.Exit(0);
        } catch(Exception e){suite=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        while(Time.frameCount<3||RetroSceneLoadReveal.IsPreparingGameplay)yield return null;
        Time.timeScale=1;Time.captureDeltaTime=.02f;yield return null;
        Check(Time.deltaTime>0,"animation has a nonzero deterministic timestep");
        new GameObject("Listener").AddComponent<AudioListener>();
        string[] paths={
            "Assets/Prefabs/VitalNPCS/Hero.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/Strongman-level2.prefab",
            "Assets/Prefabs/Level2NPCS/Skeleton-level2.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/Engineer-level2-Idle.prefab",
            "Assets/Prefabs/Level2NPCS/Automaton-level2.prefab",
            "Assets/Prefabs/Level0NPCs/Prisoner.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/Paladin.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/Wizard-level2-Idle.prefab"
        };
        foreach(var path in paths)
        {
            var data=Spawn(path);var renderer=Renderer(data);var mover=data.GetComponent<ZeldaFourWayMover>();
            var body=data.GetComponent<BoxCollider2D>();Vector2 bodySize=body.size,bodyOffset=body.offset;
            Vector3 rootPosition=data.transform.position,visualPosition=renderer.transform.localPosition;
            Vector3 rootScale=data.transform.localScale;var material=renderer.sharedMaterial;
            Transform stamina=data.transform.Find("Sprint Stamina");var staminaPosition=stamina.localPosition;
            mover.enabled=false;data.ApplyCharacterVisualWithMovement(renderer,Vector2.down,true,false);
            Check(data.GetComponent<CharacterSprintVisual>()==null,"NPC does not allocate sprint runtime");
            Request(data,false,Vector2.down);Frame(data,Vector2.zero);
            Check(data.GetComponent<CharacterSprintVisual>()==null,"walking does not allocate sprint runtime");
            var settings=Get<CharacterSprintVisualSettings>(data,"sprintVisualSettings");
            Check(settings!=null&&settings.enabled&&settings.cadenceMultiplier==1.5f,"old prefab receives default settings without editing YAML");
            Check(settings.bobCyclesPerSecond==2.5f,"existing prefabs receive slower independent bob default");
            Request(data,true,Vector2.down);Frame(data,Vector2.zero);
            var run=data.GetComponent<CharacterSprintVisual>();Check(run!=null,"grounded humanoid opts in on first sprint");
            Check(!run.IsRunningVisual,"no bob before accepted movement");
            for(int i=0;i<18;i++)Frame(data,Vector2.down*.14f);
            Check(run.IsRunningVisual&&Mathf.Abs(run.CadenceMultiplier-1.5f)<.001f,"smooth acceleration reaches 1.5x cadence");
            var cache=Get<IDictionary>(run,"frames");Check(cache.Count>0&&cache.Count<=8,"bounded cache for walking sprites");
            var pair=cache.Values.Cast<object>().First();var low=Get<Sprite>(pair,"low");var high=Get<Sprite>(pair,"high");
            Check(low.bounds==high.bounds&&low.pivot==high.pivot,"fixed bounds prevent floating HUD anchors");
            Check(low.texture==high.texture&&low.texture.filterMode==FilterMode.Point,"two poses share one point-filtered atlas");
            Color[] lowPixels=low.texture.GetPixels((int)low.rect.x,0,(int)low.rect.width,(int)low.rect.height);
            Color[] highPixels=high.texture.GetPixels((int)high.rect.x,0,(int)high.rect.width,(int)high.rect.height);
            int w=(int)low.rect.width,h=(int)low.rect.height;
            Check(lowPixels.Count(p=>p.a>0)==highPixels.Count(p=>p.a>0),"rising frame does not clip hats or feet");
            for(int y=0;y<h-1;y++)for(int x=0;x<w;x++)
                Check(lowPixels[y*w+x]==highPixels[(y+1)*w+x],"exact one-pixel rise without stretch/recolor");
            Check(data.transform.position==rootPosition&&data.transform.localScale==rootScale&&renderer.transform.localPosition==visualPosition&&
                body.size==bodySize&&body.offset==bodyOffset,"body transforms and physics are unchanged");
            Check(stamina.localPosition==staminaPosition&&renderer.sharedMaterial==material,"HUD and material unchanged");
            var dust=Get<ParticleSystem>(run,"dust");Check(dust!=null,"actual distance produces dust");
            Check(dust.main.maxParticles==24&&dust.main.simulationSpace==ParticleSystemSimulationSpace.World&&
                !dust.collision.enabled&&!dust.emission.enabled,"bounded world-space manual particles without collision");
            Check(dust.GetComponent<ParticleSystemRenderer>().sortingOrder<renderer.sortingOrder,"dust stays below character");
            int count=cache.Count;for(int i=0;i<15;i++)Frame(data,Vector2.down*.14f);
            Check(Get<IDictionary>(run,"frames").Count==count&&Get<ParticleSystem>(run,"dust")==dust,"steady sprint reuses frames and emitter");
            // Two seconds of real rendered poses: five bobs, not one bob per foot frame.
            Set(run,"bobPhase",0f);
            int rises=0,holdFrames=0;bool wasHigh=false;
            int samples=Mathf.RoundToInt(2f/Time.deltaTime);
            for(int i=0;i<samples;i++)
            {
                Frame(data,Vector2.down*.14f);
                bool isHigh=renderer.sprite.name.EndsWith("Run Rise");
                if(isHigh!=wasHigh)
                {
                    if(isHigh)rises++;
                    if(i>samples/4)Check(holdFrames*Time.deltaTime>=.12f,"bob poses have a stable dwell instead of rapid flicker");
                    holdFrames=0;wasHigh=isHigh;
                }
                holdFrames++;
            }
            Check(rises==5,"default bob completes 2.5 cycles per second");
            Check(Mathf.Abs(run.CadenceMultiplier-1.5f)<.001f,"lower bob frequency preserves faster foot cadence");
            float bobBefore=Get<float>(run,"bobPhase");
            run.Prepare(renderer,true,false);
            Set(run,"clockFrame",-1);run.WalkFrame(16f);
            Check(Get<float>(run,"bobPhase")==bobBefore,"multiple redraws and another foot rate cannot speed up bob");
            if(previewFrames.Count<18) {AddPreview(low);AddPreview(high);AddPreview(low);}
            // Clock progresses continuously; changing multiplier cannot multiply absolute Time.time.
            float phase=Get<float>(run,"walkPhase");bobBefore=Get<float>(run,"bobPhase");
            Request(data,false,Vector2.down);Frame(data,Vector2.down*.07f);
            float delta=Mathf.Repeat(Get<float>(run,"walkPhase")-phase,2);
            Check(delta>0&&delta<=Time.deltaTime*7*1.51f+.001f,"run-to-walk phase is continuous");
            Check(run.CadenceMultiplier<1.5f&&run.CadenceMultiplier>1f,"run-to-walk cadence eases down");
            Check(Mathf.Abs(Mathf.Repeat(Get<float>(run,"bobPhase")-bobBefore,1f)-Time.deltaTime*2.5f)<.001f,
                "run-to-walk bob phase remains continuous");
            for(int i=0;i<20;i++)Frame(data,Vector2.down*.07f);
            Check(!run.IsRunningVisual&&Mathf.Abs(run.CadenceMultiplier-1)<.001f,"walk returns to original rate");
            // Wall/no motion immediately stops the new effects, even with a held run key.
            Request(data,true,Vector2.right);Frame(data,Vector2.zero);
            Check(!run.IsRunningVisual&&Get<float>(run,"pendingDistance")==0,"wall blocks stop bob and distance emission");
            Check(Get<float>(run,"bobPhase")==0f,"blocked movement resets bob to grounded pose");
            // Attack exit restores the original authored sprite before the attack frame is drawn.
            for(int i=0;i<10;i++)Frame(data,Vector2.right*.14f);
            Set(mover,"attackTimer",.2f);Frame(data,Vector2.zero,true,true);
            Check(!run.IsRunningVisual&&!renderer.sprite.name.Contains("Run "),"attack takes priority and clears run frame");
            Set(mover,"attackTimer",0f);
            for(int i=0;i<10;i++)Frame(data,Vector2.right*.14f);
            mover.enabled=false;
            Check(!run.IsRunningVisual&&!renderer.sprite.name.Contains("Run ")&&dust.particleCount==0,"control loss restores original and clears particles");
            var ownedTexture=low.texture;
            Object.Destroy(data.gameObject);yield return null;
            Check(low==null&&high==null&&ownedTexture==null,"destroy releases cached sprites and texture");
        }
        foreach(string path in new[]{"Assets/Prefabs/Level1NPCS/Behemoth.prefab","Assets/Prefabs/Ghost.prefab",
            "Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab"})
        {
            var data=Spawn(path);Request(data,true,Vector2.right);
            for(int i=0;i<10;i++)Frame(data,Vector2.right*.2f);
            Check(!CharacterSprintVisual.Supports(data)&&data.GetComponent<CharacterSprintVisual>()==null,
                "explicit special-character exclusion including Behemoth: "+data.GetType().Name);
            Object.Destroy(data.gameObject);yield return null;
        }
        var hero=(HeroZeldaCharacterData)Spawn(paths[0]);Request(hero,true,Vector2.right);Frame(hero,Vector2.zero);
        var effect=hero.GetComponent<CharacterSprintVisual>();var heroRenderer=Renderer(hero);
        for(int i=0;i<10;i++)Frame(hero,Vector2.right*.2f);
        // A real solid wall exercises the mover's accepted-displacement hook.
        var wall=new GameObject("Sprint wall");wall.layer=LayerMask.NameToLayer("Blocks");
        var wallBox=wall.AddComponent<BoxCollider2D>();wallBox.size=new Vector2(.2f,4);
        var bounds=hero.GetComponent<BoxCollider2D>().bounds;
        wall.transform.position=new Vector3(bounds.max.x+.1f,bounds.center.y,0);Physics2D.SyncTransforms();
        Set(effect,"prepareFrame",-1);
        typeof(ZeldaFourWayMover).GetMethod("FixedUpdate",F).Invoke(hero.GetComponent<ZeldaFourWayMover>(),null);
        hero.ApplyCharacterVisualWithMovement(heroRenderer,Vector2.right,true,false);
        Check(!effect.IsRunningVisual,"real collision-blocked sprint does not bob");Object.Destroy(wall);yield return null;
        Request(hero,true,Vector2.right);
        for(int i=0;i<10;i++)Frame(hero,Vector2.right*.2f);
        hero.GetComponent<ZeldaFourWayMover>().ReceiveControl(Vector2.right);
        Request(hero,true,Vector2.right);
        Check(hero.TakeDamage(1,null,DirectionalDamageContext.Melee((Vector2)hero.transform.position+Vector2.right*2))==CharacterDamageResult.DirectionalShieldBlocked,
            "shield mechanics work during sprint");
        Frame(hero,Vector2.right*.2f);
        Check(hero.IsShowingShieldBlockFeedback&&heroRenderer.sprite.texture.GetPixels().Any(p=>p.r>.98f&&p.g>.98f&&p.b>.98f&&p.a>0),
            "run pixels preserve white shield feedback");
        hero.TakeDamage(1,null,default);Frame(hero,Vector2.right*.2f);
        Check(hero.IsShowingDamageFeedback&&heroRenderer.color==hero.GetDamageFeedbackTint(Color.white),"run does not overwrite damage flash");
        Check(CharacterPotionBuffs.TryApply(hero,AlchemyPotionKind.Veil),"veil setup");
        Frame(hero,Vector2.right*1);
        Check(Get<ParticleSystem>(effect,"dust").particleCount==0,"veil suppresses revealing dust trail");
        var saved=hero.CaptureRuntimeState();hero.ApplyRuntimeState(saved);
        Check(!effect.IsRunningVisual,"load clears transient sprint visuals");
        hero.gameObject.SetActive(false);hero.gameObject.SetActive(true);
        Check(!effect.IsRunningVisual,"streaming reactivation has no stale run pose");
        var heroSettings=Get<CharacterSprintVisualSettings>(hero,"sprintVisualSettings");heroSettings.enabled=false;
        Request(hero,true,Vector2.right);Frame(hero,Vector2.right*.2f);
        Check(!effect.IsRunningVisual,"inspector disable works on already-created runtime");
        Object.Destroy(hero.gameObject);yield return null;
        WritePreview();Time.captureDeltaTime=0;
    }
    static void AddPreview(Sprite sprite)
    {
        const int cell=192,scale=6;var canvas=Enumerable.Repeat(new Color32(24,31,43,255),cell*cell).ToArray();
        var pixels=sprite.texture.GetPixels32();int w=(int)sprite.rect.width,h=(int)sprite.rect.height;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            var c=pixels[((int)sprite.rect.y+y)*sprite.texture.width+(int)sprite.rect.x+x];if(c.a==0)continue;
            for(int dy=0;dy<scale;dy++)for(int dx=0;dx<scale;dx++)
                canvas[(16+y*scale+dy)*cell+(cell-w*scale)/2+x*scale+dx]=c;
        }
        previewFrames.Add(canvas);
    }
    static void WritePreview()
    {
        const int cell=192,w=1152,h=576;var output=new Texture2D(w,h,TextureFormat.RGBA32,false);
        var pixels=new Color32[w*h];
        for(int actor=0;actor<6;actor++)for(int phase=0;phase<3;phase++)
        {
            var cellPixels=previewFrames[actor*3+phase];
            for(int y=0;y<cell;y++)for(int x=0;x<cell;x++)pixels[((2-phase)*cell+y)*w+actor*cell+x]=cellPixels[y*cell+x];
        }
        output.SetPixels32(pixels);output.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../Character-Sprint-preview.png"),output.EncodeToPNG());Object.Destroy(output);
    }
}
