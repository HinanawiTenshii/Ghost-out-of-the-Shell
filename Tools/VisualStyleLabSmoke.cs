#if UNITY_EDITOR
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
public static partial class VisualStyleLabSmoke
{
    const string Key="VisualStyleLabSmoke";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Stack<IEnumerator> suite; static int frame=-1,checks; static double deadline;
    static VisualStyleLabSmoke(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"),"Clone only");
        PlayerSettings.productName="visual-style-lab-smoke-"+Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene(BuildVisualStyleLab.ScenePath);
        SessionState.SetBool(Key,true);SessionState.SetString(Key+".error","");
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type)
    {
        if(SessionState.GetBool(Key,false)&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))
            SessionState.SetString(Key+".error",message+"\n"+stack);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(suite==null){suite=new Stack<IEnumerator>();suite.Push(Verify());deadline=EditorApplication.timeSinceStartup+180;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Lab verification timeout");
            string error=SessionState.GetString(Key+".error","");
            if(error.Length>0)throw new Exception("Runtime error: "+error);
            while(suite.Count>0)
            {
                var step=suite.Peek();
                if(!step.MoveNext()){suite.Pop();continue;}
                if(step.Current is IEnumerator nested){suite.Push(nested);continue;}
                return;
            }
            SessionState.SetBool(Key,false);Debug.Log("VISUAL_STYLE_LAB_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Delay(float seconds)
    {
        float until=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<until)yield return null;
    }
    static IEnumerator Verify()
    {
        while(ZeldaRuntimeRegistry.GetControlledMover()==null || RetroSceneLoadReveal.IsBlockingInput ||
            RetroSceneLoadReveal.IsPreparingGameplay || GameSaveSystem.IsPreparingSceneSave)yield return null;
        yield return Delay(1);
        var scene=SceneManager.GetActiveScene();
        Check(scene.name=="VisualStyleLab","Lab scene loaded");
        VerifyBrightPalette(scene);
        var surfaceRoot=scene.GetRootGameObjects().FirstOrDefault(r=>r.name.StartsWith("08_SurfaceStudy_Phase2"));
        if(surfaceRoot)
        {
            Check(surfaceRoot.GetComponentsInChildren<Collider2D>(true).Length==0,"Surface study adds no collision");
            Check(surfaceRoot.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c is SceneTravelStableId),
                "Surface study has only the existing save system's automatic identity tags: "+string.Join(",",surfaceRoot.GetComponentsInChildren<MonoBehaviour>(true).Select(c=>c.GetType().Name)));
            var detailRenderers=surfaceRoot.GetComponentsInChildren<MeshRenderer>(true);
            Check(detailRenderers.Length==8,"Surface details are combined into eight static batches");
            foreach(var detail in detailRenderers)
            {
                Check(detail.GetComponent<MeshFilter>().sharedMesh!=null,"Baked mesh: "+detail.name);
                Check(detail.sharedMaterial!=null&&detail.sharedMaterial.shader.isSupported,"Supported local material: "+detail.name);
            }
            Check(detailRenderers.Where(r=>r.gameObject.layer==LayerMask.NameToLayer("Blocks")).All(r=>r.sortingOrder<10),"Wall faces remain below interactable overlays");
            var ground=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("01_Grounds"));
            var roadBounds=ground.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name.EndsWith(" Road")).Select(s=>s.bounds).ToArray();
            var edges=surfaceRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.sharedMesh.name=="Lab RoadEdges").sharedMesh;
            Check(edges.colors.All(c=>Mathf.Abs(c.a-1)<.001f),"Road edge has no translucent cast shadow");
            Check(edges.colors.Select(c=>(Color32)c).Distinct().Count()==1,"All road edges have the same neutral tone, without lit/dark bevels");
            Check(edges.vertices.All(v=>roadBounds.Any(b=>v.x>=b.min.x-.001f&&v.x<=b.max.x+.001f&&v.y>=b.min.y-.001f&&v.y<=b.max.y+.001f)),"Road edge detail stays inside the road, with no exterior shadow strip");
            var wallGroup=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("02_Walls"));
            var walls=wallGroup.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.transform.parent==wallGroup.transform&&s.GetComponent<BoxCollider2D>()!=null&&
                (s.name.StartsWith("Boundary ")||(s.name.StartsWith("Shop ")&&s.name!="Shop Breakable Glass"))).ToArray();
            Check(walls.Length==12&&walls.All(s=>s.color.r>.55f&&s.color.b-s.color.r<.15f),"Twelve walls use a bright, pale grey-blue base");
            var wallJoints=surfaceRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.sharedMesh.name=="Lab WallJoints").sharedMesh;
            Check(wallJoints.colors.All(c=>Mathf.Abs(c.r-walls[0].color.r)<.04f&&Mathf.Abs(c.g-walls[0].color.g)<.04f&&Mathf.Abs(c.b-walls[0].color.b)<.04f),"Wall seams are close to the base color, without bright end accents");
            Check(wallJoints.vertexCount<240,"Wall texture density reduced");
            var wallFaces=surfaceRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.sharedMesh.name=="Lab WallFaces").sharedMesh;
            Check(wallFaces.vertices.All(v=>walls.Any(w=>v.x>=w.bounds.min.x-.001f&&v.x<=w.bounds.max.x+.001f&&v.y>=w.bounds.min.y-.001f&&v.y<=w.bounds.max.y+.001f)),"Continuous wall rims stay inside the physical wall footprint");
            var shadowMesh=surfaceRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.sharedMesh.name=="Lab ContactShadows").sharedMesh;
            var shadowVertices=shadowMesh.vertices;var shadowRects=new List<Rect>();
            for(int i=0;i<shadowVertices.Length;i+=4)
                shadowRects.Add(Rect.MinMaxRect(shadowVertices[i].x,shadowVertices[i].y,shadowVertices[i+2].x,shadowVertices[i+2].y));
            bool noOverlap=true;
            for(int i=0;i<shadowRects.Count;i++)for(int j=0;j<i;j++)
            {
                var a=shadowRects[i];var b=shadowRects[j];
                if(Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)>.001f&&Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin)>.001f)noOverlap=false;
            }
            Check(noOverlap,"No stacked translucent shadow rectangles at wall junctions");
        }
        Check(DevlogDemoSceneSettings.SuppressesAutomaticSaves(scene),"Lab disables automatic saves");
        Check(Object.FindObjectsOfType<CameraFollowActiveZeldaMover>().Length==1,"One gameplay camera");
        Check(Object.FindObjectsOfType<ZeldaHealthHeartsUI>().Length==1,"One HUD");
        Check(Object.FindObjectsOfType<EventSystem>().Length==1,"One event system");
        Check(Object.FindObjectsOfType<SceneTransitionPoint>().Length==0,"No exits to real levels");
        Check(PersistentInventory.Instance!=null && PersistentInventory.Instance.Slots.All(s=>s.Quantity==0),"Fresh empty inventory");
        Check(QuestJournalManager.GetOrCreate().CaptureSaveState().entries.Count==0,"No production quests");
        var hud=ZeldaHealthHeartsUI.Instance;
        var mini=(RuntimeMiniMapGraphic)typeof(ZeldaHealthHeartsUI).GetField("taskMiniMapGraphic",Flags).GetValue(hud);
        Check(mini!=null&&mini.gameObject.activeInHierarchy,"Minimap is displayed");
        var surfaces=(IList)typeof(RuntimeMiniMapGraphic).GetField("surfaces",Flags).GetValue(mini);
        Check(surfaces.Count>=9,"Roads, bridge, banks and water are registered on the map");
        Check(Object.FindObjectOfType<TabJournalMenuController>().AreaDisplayName=="视觉实验街区","Independent map label");
        var ghost=ZeldaRuntimeRegistry.GetControlledMover();
        Check(ghost.CharacterData.IsGhostLike,"Playable ghost spawned");
        Check(Vector2.Distance(ghost.transform.position,new Vector2(0,-8.5f))<.2f,"Spawn at the south street");
        var all=Object.FindObjectsOfType<ZeldaCharacterData>(true).Where(c=>c.gameObject.scene==scene&&!c.IsGhostLike).ToArray();
        Check(all.Length==5,"Five representative NPCs");
        var labVision=Object.FindObjectsOfType<VisualStyleLabVisionStyle>(true).Where(v=>v.gameObject.scene==scene).ToArray();
        if(labVision.Length>0)
        {
            Check(labVision.Length==5,"Five opt-in vision displays, no global AI override");
            Check(Object.FindObjectsOfType<VisualStyleLabPlantMotion>(true).Length==2,"Only tree/bush art has wind");
            Check(Object.FindObjectsOfType<VisualStyleLabMechanismSignal>(true).Length==4,"Four opt-in small mechanism displays");
            Check(VisualStyleLabPlantMotion.Gust(4,8,2.4f)==0&&VisualStyleLabPlantMotion.Gust(7,8,2.4f)==0,"Foliage rests between gusts");
            Check(Mathf.Abs(VisualStyleLabPlantMotion.Gust(.6f,8,2.4f))>.4f,"Foliage has a brief breeze pose");
            foreach(var v in labVision.Where(v=>v.gameObject.activeInHierarchy))
            {
                var cone=v.transform.Find("AI Vision Cone");
                if(cone==null||!cone.GetComponent<MeshRenderer>().enabled)continue;
                Check(cone.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Hidden/Cogitans/LabVisionStudy","Lab shader reuses original cone");
                Check(v.DisplayColor.a<.10f,"Calm cone has restrained fill");
                Check(cone.GetComponent<MeshFilter>().sharedMesh.uv.Length==cone.GetComponent<MeshFilter>().sharedMesh.vertexCount,"Contour data matches perception topology");
                v.enabled=false;
                Check(cone.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Hidden/Cogitans/AiVisionUnion","Disabling display restores production shader");
                v.enabled=true;
            }
            Check(VisualStyleLabVisionStyle.ColorFor(ZeldaAiState.Alert,Color.blue,Color.yellow,Color.red)==Color.yellow,"Warning vision differs from calm");
            Check(VisualStyleLabVisionStyle.ColorFor(ZeldaAiState.Hostile,Color.blue,Color.yellow,Color.red)==Color.red,"Hostile vision uses danger color");
            VerifyVisionUnion();
        }
        var actorShadows=Object.FindObjectsOfType<VisualStyleLabContactShadow>(true).Where(s=>s.gameObject.scene==scene).ToArray();
        if(actorShadows.Length>0)
        {
            Check(actorShadows.Length==5,"All five physical actors have opt-in contact shadows");
            Check(ghost.GetComponent<VisualStyleLabContactShadow>()==null,"Ghost has no physical contact shadow or new outline");
            foreach(var s in actorShadows)
            {
                Check(s.ShadowRenderer!=null&&s.ShadowRenderer.transform.parent==s.transform,"Shadow follows its owner via parenting");
                Check(s.ShadowRenderer.sortingOrder==-7&&s.ShadowRenderer.GetComponent<Collider2D>()==null,"Shadow is below characters and has no collision");
                Check(s.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.name.StartsWith("Lab4 "))==1,"Only a foot shadow, no identification silhouette");
            }
            Check(actorShadows.Select(s=>s.ShadowRenderer.GetComponent<MeshFilter>().sharedMesh).Distinct().Count()==1,"Actors share one cached shadow mesh");
            var labProps=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).Where(r=>r.name=="Lab4 Contact shadow").ToArray();
            Check(labProps.Length>=17,"Furniture, cauldron and devices have local contact shadows");
            Check(labProps.All(r=>r.GetComponent<Collider2D>()==null&&r.sortingOrder==-7),"Prop shadows never change collision or cover characters");
            var mechanisms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
                .Where(c=>c is BatterySocket||c is WaterwayGateController||c is LeverData||c is GeometricAlchemyDecoration).ToArray();
            foreach(var mechanism in mechanisms)
            {
                var contact=mechanism.transform.Find("Lab4 Contact shadow").GetComponent<MeshFilter>().sharedMesh;
                Check(contact.vertexCount==60&&contact.colors.Any(c=>c.a==0),"Mechanism has a fading footprint rather than extruded collision edges: "+mechanism.name);
                Check(contact.colors.Max(c=>c.a)<.25f,"Mechanism contact is restrained: "+mechanism.name);
                if(mechanism is LeverData)
                    Check(contact.bounds.max.y<.12f&&contact.bounds.min.y>-.19f,"Lever contact stays under its plinth, not its raised handle/collision box");
            }
            var street=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("04_Street"));
            var streetShadows=street.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Lab4 Contact shadow"&&r.GetComponentInParent<LeverData>()==null).ToArray();
            if(streetShadows.Length>0)
            {
                Check(streetShadows.Length==8,"All eight outdoor props use local contact shadows");
                Check(streetShadows.All(r=>r.GetComponentsInChildren<Collider2D>(true).Length==0&&r.sortingOrder==-7),"Outdoor decoration adds no collision or character overlay");
                var shop=scene.GetRootGameObjects().Single(r=>r.name.StartsWith("03_Shop"));
                var chairs=shop.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Chair")).ToArray();
                Check(chairs.Length==11,"All eleven chairs are covered by the refinement");
                foreach(var chair in chairs)
                {
                    var rim=chair.Find("Lab4 Upper-left rim");
                    Check(rim==null||!rim.GetComponent<MeshRenderer>().enabled,"Chair has no generic box-shaped rim: "+chair.name);
                    var sprites=chair.GetComponentsInChildren<SpriteRenderer>(true);
                    var seat=sprites.Single(s=>s.name=="Square (1)");
                    var back=sprites.Single(s=>s.name=="Square (2)");
                    Check(back.color.grayscale-seat.color.grayscale>.12f,"Backrest remains distinct from seat: "+chair.name);
                    // Every shadow vertex must sit on a native local corner, or on
                    // its short world-light offset; a rotated world AABB fails this.
                    var frameSprite=sprites.Single(s=>s.name=="Square");
                    var bounds=frameSprite.sprite.bounds;
                    var corners=new[]{new Vector3(bounds.min.x,bounds.min.y),new Vector3(bounds.max.x,bounds.min.y),
                        new Vector3(bounds.max.x,bounds.max.y),new Vector3(bounds.min.x,bounds.max.y)}
                        .Select(v=>chair.InverseTransformPoint(frameSprite.transform.TransformPoint(v))).ToArray();
                    var offset=chair.InverseTransformVector(new Vector3(.065f,-.09f,0));
                    var valid=corners.Concat(corners.Select(v=>v+offset)).ToArray();
                    var contact=chair.Find("Lab4 Contact shadow").GetComponent<MeshFilter>().sharedMesh;
                    Check(contact.vertexCount>0&&contact.vertices.All(v=>valid.Any(c=>Vector3.Distance(v,c)<.002f)),"Shadow respects rotated chair footprint: "+chair.name);
                }
                Check(street.GetComponentsInChildren<KeroseneStreetLamp>(true).Length==3,"Three original street lamps retained");
            }
        }
        foreach(var c in all)Check(string.IsNullOrEmpty(c.PersistentCharacterId),"No quest actor identity: "+c.name);
        var patrols=Object.FindObjectsOfType<ZeldaPatrolCharacterAi>(true).Where(p=>p.gameObject.scene==scene).ToArray();
        Check(patrols.Length==2,"Two editable patrols");
        Vector3 patrolStart=patrols[0].transform.position;
        yield return Delay(3);
        Check(Vector3.Distance(patrolStart,patrols[0].transform.position)>.1f,"Patrol is running");
        Capture("VisualStyleLab-ghost");

        var engineer=all.Single(c=>c.name.StartsWith("测试工程师"));
        Check(engineer.SkillValue==6,"Engineer can use test mechanisms");
        Check(ZeldaInteractionObstruction.HasClearPath(engineer,ghost,engineer.transform.position),"Initial possession approach unobstructed");
        var mover=engineer.GetComponent<ZeldaFourWayMover>();
        ghost.enabled=false;ghost.gameObject.SetActive(false);
        mover.gameObject.SetActive(true);mover.ReceiveControl(Vector2.up);ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        ActiveZeldaCharacterDataSource.Instance.Refresh();
        Check(ActiveZeldaCharacterDataSource.Instance.ActiveMover==mover,"Controlled actor connected to HUD");
        yield return Delay(.4f);
        var activeShadow=engineer.GetComponent<VisualStyleLabContactShadow>();
        if(activeShadow)
        {
            Check(activeShadow.ShadowRenderer.enabled,"Possessed body retains its contact shadow without an outline");
            activeShadow.enabled=false;Check(!activeShadow.ShadowRenderer.enabled,"Disabling experiment hides its contact shadow");
            activeShadow.enabled=true;yield return null;yield return null;
            Check(activeShadow.ShadowRenderer.enabled,"Shadow returns after re-enabling");
            Check(activeShadow.BodyRenderer.sprite.texture.filterMode==FilterMode.Point,"Character sprite remains crisp point-sampled art");
            activeShadow.BodyRenderer.enabled=false;yield return null;yield return null;
            Check(!activeShadow.ShadowRenderer.enabled,"Hiding the body also hides its shadow");
            activeShadow.BodyRenderer.enabled=true;yield return null;yield return null;
            Check(activeShadow.ShadowRenderer.enabled,"Revealing the body restores its shadow");
        }
        var lever=Object.FindObjectOfType<LeverData>();
        var door=Object.FindObjectOfType<DoubleSlidingDoor>();
        Check(lever.LinkedObject==door.gameObject,"Lever controls lab entrance");
        lever.ActivateFromClockworkPuppet();yield return Delay(1.1f);
        Check(door.IsFullyOpen,"Entrance opens");
        // Verify common passage centers do not overlap solid geometry.
        foreach(var p in new[]{new Vector2(0,-5.71f),new Vector2(12,0),new Vector2(18.5f,-2),new Vector2(-12,0)})
        {
            Physics2D.SyncTransforms();
            var blocked=Physics2D.OverlapCircleAll(p,.2f).Where(c=>c.enabled&&!c.isTrigger&&c.GetComponentInParent<ZeldaCharacterData>()==null).ToArray();
            Check(blocked.Length==0,"Walkable passage "+p+": "+string.Join(",",blocked.Select(c=>c.name)));
        }
        Move(mover,new Vector2(0,-2));yield return Delay(.8f);
        Capture("VisualStyleLab-shop");
        CaptureDeviceDetail(lever.transform,"VisualStyleLab-lever-detail");
        CaptureDeviceDetail(Object.FindObjectOfType<GeometricAlchemyDecoration>().transform,"VisualStyleLab-cauldron-detail");
        var red=Object.FindObjectsOfType<PickupItemBase>(true).First(p=>p.name.Contains("RedHerb"));
        red.gameObject.SetActive(true);Move(mover,(Vector2)red.transform.position+Vector2.down*.8f);yield return Delay(.2f);
        Check(ZeldaInteractionObstruction.HasClearPath(red,mover,red.transform.position),"Test herb can be approached");
        Check(red.TryPickUp(),"Test item enters inventory");yield return null;
        Check(PersistentInventory.Instance.Slots.Any(s=>s.Quantity>0),"Inventory UI receives pickup");
        ItemDescriptionWindow.GetOrCreate().CloseWindow();yield return null;

        Move(mover,new Vector2(-11,4));yield return Delay(.8f);
        Capture("VisualStyleLab-street");
        if(labVision.Length>0)
        {
            var plant=Object.FindObjectsOfType<VisualStyleLabPlantMotion>().First(p=>p.GetComponent<GeometricTreeDecoration>()!=null);
            var plantCollider=plant.GetComponent<BoxCollider2D>();var size=plantCollider.size;var offset=plantCollider.offset;var position=plant.transform.position;
            var mesh=plant.transform.Find("Tree Geometry (Generated)").GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.name=="Lab5 Moving leaves","Tree has its own runtime wind mesh");
            var trunk=mesh.vertices.Take(4).ToArray();
            var bush=Object.FindObjectsOfType<VisualStyleLabPlantMotion>().First(p=>p.GetComponent<GeometricTreeDecoration>()==null);
            var bushPosition=bush.transform.position;var bushSize=bush.GetComponent<BoxCollider2D>().size;
            yield return Delay(.4f);
            Check(mesh.vertices.Take(4).SequenceEqual(trunk),"Wind never moves trunk vertices");
            Check(plant.transform.position==position&&plantCollider.size==size&&plantCollider.offset==offset,"Wind never moves physics or owner");
            Check(bush.transform.position==bushPosition&&bush.GetComponent<BoxCollider2D>().size==bushSize,"Bush wind never moves the root sprite or collider");
            plant.enabled=false;
            Check(plant.transform.Find("Tree Geometry (Generated)").GetComponent<MeshFilter>().sharedMesh.name=="Minimal Geometric Tree","Disabling wind restores original mesh");
            plant.enabled=true;yield return Delay(.2f);
            Check(plant.transform.Find("Tree Geometry (Generated)").GetComponent<MeshFilter>().sharedMesh.name=="Lab5 Moving leaves","Reloading wind recreates only its visual clone");
        }
        Move(mover,new Vector2(13.7f,1.5f));yield return Delay(.8f);
        var control=Object.FindObjectOfType<WaterwayGateController>();
        var gate=Object.FindObjectOfType<WaterwayGate>();
        var socket=Object.FindObjectOfType<BatterySocket>();
        var fade=Object.FindObjectOfType<WaterwayGateTargetFade>();
        Check(control.LinkedGate==gate&&socket.LinkedMechanism==control,"Water controller and power supply bound");
        Check(socket.HasPower&&gate.IsOpenRequested,"Water starts with a working battery");
        Capture("VisualStyleLab-water");
        CaptureDeviceDetail(control.transform,"VisualStyleLab-controller-detail");
        CaptureDeviceDetail(socket.transform,"VisualStyleLab-socket-detail");
        control.SetOpen(false);yield return Delay(.16f);
        if(labVision.Length>0)
        {
            Check(control.GetComponent<VisualStyleLabMechanismSignal>().State==VisualStyleLabMechanismSignal.DisplayState.Working,"Controller displays ongoing transition");
            Capture("VisualStyleLab-mechanism-working");
        }
        yield return Delay(1.6f);
        Check(gate.IsFullyClosed&&!fade.VisibleRequested&&!fade.gameObject.activeSelf,"Closing the gate fades and disables water");
        Check(socket.TryUninstall(PersistentInventory.Instance),"Battery can be removed");yield return Delay(1.6f);
        Check(gate.IsFullyOpen&&fade.VisibleRequested&&fade.gameObject.activeSelf,"Removing power restores authored open state");
        if(labVision.Length>0)
        {
            Check(socket.GetComponent<VisualStyleLabMechanismSignal>().State==VisualStyleLabMechanismSignal.DisplayState.Unpowered,"Removed battery extinguishes socket signal");
            Check(control.GetComponent<VisualStyleLabMechanismSignal>().State==VisualStyleLabMechanismSignal.DisplayState.Unpowered,"Power loss extinguishes controller signal");
            Check(gate.GetComponent<VisualStyleLabMechanismSignal>().State==VisualStyleLabMechanismSignal.DisplayState.Unpowered,"Gate signal matches controller power");
            Capture("VisualStyleLab-mechanism-unpowered");
        }
        Check(socket.TryInstall(PersistentInventory.Instance),"Battery can be reinstalled");
        if(labVision.Length>0)
        {
            yield return Delay(.4f);
            Check(socket.GetComponent<VisualStyleLabMechanismSignal>().State==VisualStyleLabMechanismSignal.DisplayState.Standby,"Power restoration returns signal to standby");
            foreach(var water in Object.FindObjectsOfType<SpriteWaterFlowParticles>())
                Check(water.TargetParticleCount<=Mathf.CeilToInt(water.SurfaceWorldArea*.6f),"Water ribbons remain sparse and bounded by area");
        }
        var pause=Object.FindObjectOfType<PauseMenuController>();pause.OpenMenu();
        Check(PauseMenuController.IsPaused,"Pause works");pause.CloseMenu();
        var tab=Object.FindObjectOfType<TabJournalMenuController>();tab.OpenMenu();yield return null;
        Check(TabJournalMenuController.IsOpen,"Map/journal/skills open");tab.CloseMenu();
        Check(!GameSaveSystem.Instance.Save(0,false),"Automatic slot writes are blocked");
        if(labVision.Length>0)
        {
            var stateField=typeof(ZeldaCharacterAiBase).GetField("currentState",Flags);
            var refresh=typeof(VisualStyleLabVisionStyle).GetMethod("LateUpdate",Flags);
            var style=labVision.First(v=>v.gameObject.activeInHierarchy&&v.GetComponent<ZeldaCharacterAiBase>().enabled);
            var ai=style.GetComponent<ZeldaCharacterAiBase>();var previous=ai.CurrentState;
            var renderer=style.transform.Find("AI Vision Cone").GetComponent<MeshRenderer>();bool visible=renderer.enabled;
            renderer.enabled=true;
            stateField.SetValue(ai,ZeldaAiState.Alert);refresh.Invoke(style,null);
            Check(style.DisplayColor.r>style.DisplayColor.b&&renderer.sharedMaterial.renderQueue==2998,"Live warning state updates color and render priority");
            stateField.SetValue(ai,ZeldaAiState.Hostile);refresh.Invoke(style,null);
            Check(style.DisplayColor.r>style.DisplayColor.g*2&&renderer.sharedMaterial.renderQueue==2997,"Live hostile state uses red danger priority");
            stateField.SetValue(ai,previous);refresh.Invoke(style,null);renderer.enabled=visible;
        }
        if (Object.FindObjectOfType<VisualStyleLabTerminalPresentation>()) yield return VerifyTerminal(mover);
        CaptureOverview();
    }
    static void VerifyVisionUnion()
    {
        var go=new GameObject("Temporary lab shader check");go.transform.position=new Vector3(10000,10000,-10);
        var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;
        camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=1<<31;camera.enabled=false;
        var rt=new RenderTexture(128,128,24);camera.targetTexture=rt;
        var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.right,Vector2.right,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,1};
        var mat=new Material(Shader.Find("Hidden/Cogitans/LabVisionStudy"));mat.SetFloat("_Closed",1);mat.SetFloat("_EdgePixels",2);
        var first=new GameObject("First cone",typeof(MeshFilter),typeof(MeshRenderer));first.layer=31;first.transform.position=new Vector3(10000,10000,0);
        first.GetComponent<MeshFilter>().sharedMesh=mesh;first.GetComponent<MeshRenderer>().sharedMaterial=mat;
        var tex=new Texture2D(128,128,TextureFormat.RGB24,false);var old=RenderTexture.active;
        camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,128,128),0,0);tex.Apply();var single=tex.GetPixel(64,64);var edge=tex.GetPixel(127,64);
        var second=Object.Instantiate(first);camera.Render();tex.ReadPixels(new Rect(0,0,128,128),0,0);tex.Apply();var overlap=tex.GetPixel(64,64);
        Check(single.g>.01f,"Lab fill is visible");Check(Mathf.Abs(single.g-overlap.g)<.01f,"Overlapping cones still render exactly one color layer");
        Check(edge.g>single.g+.02f,"Thin contour is clearer than cone fill");
        var dangerMat=new Material(mat);dangerMat.SetColor("_Color",new Color(1,.1f,.1f,.2f));dangerMat.renderQueue=2997;
        second.GetComponent<MeshRenderer>().sharedMaterial=dangerMat;
        camera.Render();tex.ReadPixels(new Rect(0,0,128,128),0,0);tex.Apply();var dangerPixel=tex.GetPixel(64,64);
        Check(dangerPixel.r>dangerPixel.g*2,"Danger cone wins over calm cone without additive overlap");
        RenderTexture.active=old;camera.targetTexture=null;rt.Release();
        foreach(var o in new UnityEngine.Object[]{first,second,go,rt,tex,mesh,mat,dangerMat})Object.Destroy(o);
    }
    static void Move(ZeldaFourWayMover mover,Vector2 p)
    {
        mover.transform.position=p;var body=mover.GetComponent<Rigidbody2D>();if(body){body.position=p;body.velocity=Vector2.zero;}
        Physics2D.SyncTransforms();Camera.main.GetComponent<CameraFollowActiveZeldaMover>().PrepareLoadedSceneView();
        Camera.main.GetComponent<CameraVisionObjectStreaming>().PrepareSoulTransferView();
    }
    static void Capture(string name)
    {
        var cameras=Camera.allCameras.Where(c=>c.enabled&&c.targetTexture==null).OrderBy(c=>c.depth).ToArray();
        var rt=new RenderTexture(1280,720,24);
        foreach(var c in cameras)c.targetTexture=rt;
        foreach(var scaler in Object.FindObjectsOfType<CanvasScaler>())typeof(CanvasScaler).GetMethod("Handle",Flags).Invoke(scaler,null);
        Canvas.ForceUpdateCanvases();foreach(var c in cameras)c.Render();
        Canvas.ForceUpdateCanvases();foreach(var c in cameras)c.Render();
        Write(rt,name);foreach(var c in cameras)c.targetTexture=null;rt.Release();Object.Destroy(rt);
    }
    static void Write(RenderTexture rt,string name)
    {
        var old=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
        Directory.CreateDirectory("../../.codex-temp/visual-lab-preview");
        File.WriteAllBytes("../../.codex-temp/visual-lab-preview/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=old;Object.Destroy(tex);
    }
    static void CaptureOverview()
    {
        // Documentation-only overview, without gameplay fog or CRT. Not a gameplay capture.
        var scene=SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects().Where(r=>r.name.StartsWith("0")&&!r.name.StartsWith("00")))
            foreach(Transform child in root.transform)child.gameObject.SetActive(true);
        foreach(var renderer in Object.FindObjectsOfType<MeshRenderer>())
            if(renderer.name=="Circular Vision Overlay"||renderer.name=="Visibility Polygon Stencil")renderer.enabled=false;
        var go=new GameObject("Overview capture camera");var camera=go.AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=14.2f;camera.transform.position=new Vector3(3.5f,.5f,-30);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.08f);
        camera.cullingMask=~(1<<LayerMask.NameToLayer("UI"));
        var rt=new RenderTexture(1440,960,24);camera.targetTexture=rt;camera.Render();Write(rt,"VisualStyleLab-overview");
        camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);
    }
    static void CaptureDeviceDetail(Transform device,string name)
    {
        var maskRenderers=Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.enabled&&
            (r.name=="Circular Vision Overlay"||r.name=="Visibility Polygon Stencil"||r.name=="AI Vision Cone")).ToArray();
        foreach(var r in maskRenderers)r.enabled=false;
        var go=new GameObject("Mechanism diagnostic camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=1.15f;camera.aspect=1;camera.transform.position=device.position+Vector3.back*30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.08f);
        camera.cullingMask=~(1<<LayerMask.NameToLayer("UI"));
        var rt=new RenderTexture(512,512,24);camera.targetTexture=rt;camera.Render();Write(rt,name);
        camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(go);
        foreach(var r in maskRenderers)r.enabled=true;
    }
}
#endif
