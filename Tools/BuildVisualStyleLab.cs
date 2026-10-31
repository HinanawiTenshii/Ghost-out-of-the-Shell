#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copy into Assets/Editor of an isolated project to regenerate the lab.
// Never saves the source level, modifies shared prefabs or changes Build Settings.
public static class BuildVisualStyleLab
{
    public const string ScenePath = "Assets/Scenes/Experiments/VisualStyleLab.unity";
    const string AssetsFolder = "Assets/Art/VisualStyleLab";
    static readonly Vector3 SourceOrigin = new Vector3(-84, -29, 0);
    static readonly Rect Room = Rect.MinMaxRect(-93.5f, -34.95f, -75.1f, -23.4f);
    static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
    static Scene lab;
    static Sprite square;
    static Transform systems, ground, walls, shop, street, water, actors, pickups;
    static int importedProps;

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new InvalidOperationException("Run this builder in an isolated Unity project.");
        Directory.CreateDirectory("Assets/Scenes/Experiments");
        Directory.CreateDirectory(AssetsFolder + "/Materials");
        AssetDatabase.Refresh();
        var source = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var sourceRoots = source.GetRootGameObjects();
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TestPrefabs/Square.png");
        lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(lab);
        systems = Group("00_Systems — 独立测试基础组件");
        ground = Group("01_Grounds — 地面与道路");
        walls = Group("02_Walls — 墙与门窗");
        shop = Group("03_Shop — 城镇一层原始家具");
        street = Group("04_Street — 灯具与植物");
        water = Group("05_WaterAndMechanisms — 水道与机关");
        actors = Group("06_Characters — 附身与巡逻测试");
        pickups = Group("07_Pickups — 拾取与说明测试");

        BuildSystems(source);
        // Match the original room's scale, palette and furniture arrangement.
        var originalFloor = sourceRoots.Single(r => r.name == "Grounds").transform.Find("Square");
        Clone(originalFloor.gameObject, ground);
        foreach (Transform category in sourceRoots.Single(r => r.name == "Decorations").transform)
        {
            if (category.name != "Tables" && category.name != "Chairs" && category.name != "Candles") continue;
            foreach (Transform prop in category)
                if (Room.Contains(prop.position)) { Clone(prop.gameObject, shop); importedProps++; }
        }
        // The baseline is intentionally NOT a visual makeover; these colors are
        // the current town's palette, kept editable on scene-local renderers.
        RectSprite("Street Ground", ground, 3.5f, .5f, 39, 25, new Color(.267f,.267f,.267f), -30);
        var roads = Group("Roads — 小地图与菜单地图共用"); roads.SetParent(ground,false);
        roads.gameObject.AddComponent<MiniMapSurfaceGroup>();
        RectSprite("South Road", roads, 3.5f, -8.5f, 38, 2.8f, new Color(.29f,.36f,.40f), -20);
        RectSprite("East Road", roads, 12, .5f, 2.8f, 24, new Color(.29f,.36f,.40f), -20);
        RectSprite("North Road", roads, 3.5f, 9, 38, 2.5f, new Color(.29f,.36f,.40f), -20);
        RectSprite("West Road", roads, -12, .5f, 2.8f, 24, new Color(.29f,.36f,.40f), -20);
        BuildWalls(sourceRoots);
        BuildStreet();
        BuildWater();
        BuildActors();
        BuildItems();

        // Keep every authored material private to the experiment. Scripts and
        // sprite artwork stay shared so fixes still reach this representative lab.
        foreach (var component in InScene<Component>(lab))
        {
            if (component == null) throw new Exception("Missing script in lab");
            var serialized = new SerializedObject(component);
            var field = serialized.GetIterator(); bool changed = false;
            while (field.Next(true))
            {
                if (field.propertyType != SerializedPropertyType.ObjectReference ||
                    !(field.objectReferenceValue is Material material) || !EditorUtility.IsPersistent(material)) continue;
                field.objectReferenceValue = LocalMaterial(material); changed = true;
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        EditorSceneManager.CloseScene(source, true);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(lab, ScenePath)) throw new Exception("Cannot save lab");
        VerifyAuthoring();
        Debug.Log("VISUAL_STYLE_LAB_BUILT importedProps=" + importedProps + " actors=" + InScene<ZeldaCharacterData>(lab).Length +
            " colliders=" + InScene<Collider2D>(lab).Length + " materials=" + Materials.Count);
    }

    static T[] InScene<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static Transform Group(string name) => new GameObject(name).transform;
    static void Edit(Object o, string name, Action<SerializedProperty> edit)
    {
        var so = new SerializedObject(o); var field = so.FindProperty(name);
        if (field == null) throw new Exception(o.GetType().Name + ": missing " + name);
        edit(field); so.ApplyModifiedPropertiesWithoutUndo();
    }
    static GameObject Prefab(string path, Transform parent, Vector2 p, string name = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) throw new Exception("Missing prefab: " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, lab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(parent, true); go.transform.position = new Vector3(p.x, p.y, 0);
        if (name != null) go.name = name;
        return go;
    }
    static GameObject Clone(GameObject original, Transform parent)
    {
        var go = Object.Instantiate(original); go.name = original.name;
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.SetParent(null, true); SceneManager.MoveGameObjectToScene(go, lab);
        go.transform.SetParent(parent, true);
        go.transform.position = original.transform.position - SourceOrigin;
        go.transform.rotation = original.transform.rotation; go.transform.localScale = original.transform.lossyScale;
        return go;
    }
    static GameObject RectSprite(string name, Transform parent, float x, float y, float w, float h, Color color, int order, string layer = "Default", bool solid = false)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x,y,0); go.transform.localScale = new Vector3(w,h,1);
        go.layer = LayerMask.NameToLayer(layer);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = square; sr.color = color; sr.sortingOrder = order;
        if (solid) go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }
    static void Wall(string name, float x, float y, float w, float h) => RectSprite(name, walls,x,y,w,h,Color.white,0,"Blocks",true);

    static void BuildSystems(Scene source)
    {
        var sourceCamera = InScene<Camera>(source).Single(c => c.CompareTag("MainCamera"));
        // Clone the actual city camera, including its scene-local CRT settings.
        var camera = Clone(sourceCamera.gameObject, systems);
        camera.name = "Main Camera — 实验独立配置"; camera.transform.position = new Vector3(0,-8.5f,-10);
        foreach (var canvas in camera.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(canvas.gameObject);
        Prefab("Assets/Prefabs/ActiveZeldaCharacterDataSource.prefab",systems,Vector2.zero);
        Prefab("Assets/Prefabs/PlayerStates.prefab",systems,Vector2.zero);
        var inventory = Prefab("Assets/Prefabs/PersistentInventory.prefab",systems,Vector2.zero).GetComponent<PersistentInventory>();
        Edit(inventory,"saveToPlayerPrefs",p=>p.boolValue=false);
        Edit(inventory,"saveKey",p=>p.stringValue="CogitansExtensa.VisualStyleLab.Inventory");
        var hud = Prefab("Assets/Prefabs/ZeldaHealthHeartsUI.prefab",null,Vector2.zero).GetComponent<ZeldaHealthHeartsUI>();
        Edit(hud,"showTaskPointer",p=>p.boolValue=true);
        Edit(hud,"taskPointerDestinations",p=>p.arraySize=0);
        var pause = Prefab("Assets/Prefabs/PauseMenu.prefab",null,Vector2.zero);
        Edit(pause.GetComponent<TabJournalMenuController>(),"areaNameOverride",p=>p.stringValue="视觉实验街区");
        Prefab("Assets/Prefabs/GhostSpawnPoint.prefab",systems,new Vector2(0,-8.5f));
        var settings = new GameObject("VisualStyleLab Settings — 禁用自动存档 / 非正式关卡");
        settings.transform.SetParent(systems, false); settings.AddComponent<DevlogDemoSceneSettings>();
        settings.AddComponent<SharedPlayerHostilityTimer>();
        var group = settings.AddComponent<LevelSceneGroup>();
        Edit(group,"levelId",p=>p.stringValue="visual-style-lab");
        Edit(group,"displayName",p=>p.stringValue="视觉实验街区");
        Edit(group,"scenes",p=>p.arraySize=0);
    }

    static void BuildWalls(GameObject[] sourceRoots)
    {
        // Preserve the original side-door locations; a new south entrance makes
        // the compact section easy to walk into without the rest of the town.
        Wall("Shop West Lower",-9.17f,-4.2f,.5f,3.02f);
        Wall("Shop West Upper",-9.17f,2.37f,.5f,5.94f);
        Wall("Shop East Lower",8.61f,-5.35f,.5f,.72f);
        Wall("Shop East Upper",8.61f,1.295f,.5f,8.09f);
        Wall("Shop South Left",-5.46f,-5.71f,7.92f,.5f);
        Wall("Shop South Right",5.18f,-5.71f,7.36f,.5f);
        Wall("Shop North Left",-8.27f,5.34f,2.3f,.5f);
        Wall("Shop North Right",2.79f,5.34f,12.14f,.5f);
        var sourceDoors = sourceRoots.Single(r=>r.name=="Doors").GetComponentsInChildren<DoorHingeInteraction>(true);
        foreach (var original in sourceDoors.Where(d=>Room.Contains(d.transform.position) &&
            (Mathf.Abs(d.transform.position.x+93.16f)<.2f || Mathf.Abs(d.transform.position.x+75.39f)<.2f)))
        {
            var door=Clone(original.gameObject,walls).GetComponent<DoorHingeInteraction>();
            Edit(door,"isLocked",p=>p.boolValue=false);
            Edit(door,"passwordLockEnabled",p=>p.boolValue=false);
            Edit(door,"addJournalEntryWhenLockedInteractionFails",p=>p.boolValue=false);
            foreach (var field in new[]{"requiredUnlockItem","requiredUnlockItem2","requiredUnlockItem3","requiredUnlockItemIdentity","requiredUnlockItemIdentity2","requiredUnlockItemIdentity3"})
                Edit(door,field,p=>p.objectReferenceValue=null);
        }
        var originalGlass = sourceRoots.Single(r=>r.name=="Glasses").GetComponentsInChildren<DoorData>(true)
            .First(d=>Room.Contains(d.transform.position));
        Clone(originalGlass.gameObject,walls).name="Shop Breakable Glass";
        var doorGo=Prefab("Assets/Prefabs/Decorations/DoubleSlidingDoor.prefab",walls,new Vector2(0,-5.71f),"Lab Sliding Door");
        var doorSlide=doorGo.GetComponent<DoubleSlidingDoor>();
        Edit(doorSlide,"negativeSize",p=>p.vector2Value=new Vector2(1.5f,.25f));
        Edit(doorSlide,"positiveSize",p=>p.vector2Value=new Vector2(1.5f,.25f));
        var lever=Prefab("Assets/Prefabs/GroundLever.prefab",street,new Vector2(-2.8f,-7),"Door Lever — 门与需求窗口测试").GetComponent<LeverData>();
        Edit(lever,"linkedObject",p=>p.objectReferenceValue=doorGo);
        Edit(lever,"complexity",p=>p.intValue=1);
        Wall("Boundary South",3.5f,-12.25f,39.5f,.5f);
        Wall("Boundary North",3.5f,13.25f,39.5f,.5f);
        Wall("Boundary West",-16.25f,.5f,.5f,26);
        Wall("Boundary East",23.25f,.5f,.5f,26);
    }

    static void BuildStreet()
    {
        foreach(var p in new[]{new Vector2(-11,-6),new Vector2(10.5f,6),new Vector2(21,7.5f)})
            Prefab("Assets/Prefabs/Decorations/KeroseneStreetLamp.prefab",street,p);
        var tree=Prefab("Assets/Prefabs/Decorations/Plants/GeometricBroadleafTree.prefab",street,new Vector2(-13,8));
        tree.transform.localScale=Vector3.one*.7f;
        Prefab("Assets/Prefabs/Decorations/Plants/BreakableBush.prefab",street,new Vector2(-13,3));
        var bed=Prefab("Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab",street,new Vector2(-4,11));
        bed.transform.localScale=Vector3.one*.55f;
        Prefab("Assets/Prefabs/Decorations/Barrel-Horizontal-Large.prefab",street,new Vector2(-13,-10.7f));
        var sign=Prefab("Assets/Prefabs/Decorations/ReadableNoticeSign.prefab",street,new Vector2(3,-10.5f),"实验说明 — 按 E 阅读");
        var reader=sign.GetComponent<DocumentReader>();
        if(!reader) reader=sign.AddComponent<DocumentReader>();
        Edit(reader,"preserveExistingAppearance",p=>p.boolValue=true);
        Edit(reader,"documentText",p=>p.stringValue="视觉实验街区\n\n此处取材于城镇一层，用于比较场景、角色与终端界面的视觉效果。\n\n先按 F 附身出生点左侧的工程师（技巧 6）。前方拉杆控制商铺滑动门；侧门也可进入。室内有原场景家具、可拾取物品与炼药锅。\n\n右侧水道的控制器连接了电池槽。切换水闸可以观察水面淡入淡出，移除电池则会回到初始状态。北侧紫色地面为权限 2 区域，可观察警告与敌对。\n\n右键查看角色与物品属性；E 交互；Tab 打开地图与技能；Esc 暂停。\n\n此场景不接入正式关卡，不自动保存进度。退出运行后再次进入，即可恢复初始布局。修改实验材质时，请使用 VisualStyleLab 目录内的副本。");
        Edit(reader,"addJournalEntryOnOpen",p=>p.boolValue=false);
        Edit(reader,"setTemporaryTaskOnOpen",p=>p.boolValue=false);
        RectSprite("Permission Test Floor",ground,4,9,8,5,new Color(.12f,.12f,.35f),-19);
        var area=new GameObject("权限 2 — 视野警告测试"); area.transform.SetParent(street,false); area.transform.position=new Vector3(4,9,0);
        var permission=area.AddComponent<PermissionArea>();
        Edit(permission,"permissionLevel",p=>p.intValue=2);
        Edit(permission,"areaSize",p=>p.vector2Value=new Vector2(8,5));
    }

    static void BuildWater()
    {
        var banks=Group("Water Banks"); banks.SetParent(ground,false);
        Edit(banks.gameObject.AddComponent<MiniMapSurfaceGroup>(),"kind",p=>p.enumValueIndex=2);
        RectSprite("Channel Bed North",banks,18.5f,5,3.8f,12,new Color(.29f,.39f,.5f),-11);
        RectSprite("Channel Bed South",banks,18.5f,-4,3.8f,2,new Color(.29f,.39f,.5f),-11);
        var flowing=new GameObject("Water — 可淡出水面");flowing.transform.SetParent(water,false);
        Edit(flowing.AddComponent<MiniMapSurfaceGroup>(),"kind",p=>p.enumValueIndex=1);
        // A collider-free and ripple-free crossing remains visible above the
        // scene's Visible Non Blocking overlay camera, not just in Scene View.
        RectSprite("Flowing Water North",flowing.transform,18.5f,5,3,12,new Color(0,.35f,1),-9,"Visible Non Blocking",true).AddComponent<SpriteWaterFlowParticles>();
        RectSprite("Flowing Water South",flowing.transform,18.5f,-4,3,2,new Color(0,.35f,1),-9,"Visible Non Blocking",true).AddComponent<SpriteWaterFlowParticles>();
        var bridge=RectSprite("Channel Crossing",ground,18.5f,-2,4.4f,2,new Color(.29f,.36f,.4f),-7,"Visible Non Blocking");
        bridge.AddComponent<MiniMapSurfaceGroup>();
        var gate=Prefab("Assets/Prefabs/Decorations/WaterwayGate.prefab",water,new Vector2(18.5f,8),"Lab Water Gate 1A").GetComponent<WaterwayGate>();
        Edit(gate,"open",p=>p.boolValue=true);
        Edit(gate,"passageWidth",p=>p.floatValue=3);
        var control=Prefab("Assets/Prefabs/Decorations/WaterwayGateController.prefab",water,new Vector2(14,4),"Lab Water Controller").GetComponent<WaterwayGateController>();
        control.BindGate(gate);
        var socket=Prefab("Assets/Prefabs/Decorations/BatterySocket.prefab",water,new Vector2(14,6),"Lab Battery Socket").GetComponent<BatterySocket>();
        socket.BindMechanism(control); Edit(socket,"startsWithBattery",p=>p.boolValue=true);
        var fade=flowing.AddComponent<WaterwayGateTargetFade>();
        Edit(fade,"sourceGate",p=>p.objectReferenceValue=gate);
        // Reflection here is editor construction only; the saved UnityEvents are ordinary persistent calls.
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)typeof(WaterwayGate).GetField("onOpened",flags).GetValue(gate),fade.FadeIn);
        UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)typeof(WaterwayGate).GetField("onClosed",flags).GetValue(gate),fade.FadeOut);
        EditorUtility.SetDirty(gate);
    }

    static void BuildActors()
    {
        var engineer=Prefab("Assets/Prefabs/Level2NPCS/Idle/Engineer-level2-Idle.prefab",actors,new Vector2(-2,-8.5f),"测试工程师 — 技巧 6 / 初始附身目标");
        var data=engineer.GetComponent<ZeldaCharacterData>();
        Edit(data,"possessionCost",p=>p.intValue=1);
        Edit(data,"health",p=>p.intValue=12);
        Edit(data,"possessionEnergy",p=>p.intValue=8);
        Prefab("Assets/Prefabs/Level2NPCS/Idle/Merchant-level2-Idle.prefab",actors,new Vector2(4,1.5f),"商人 — 属性与服装对照");
        Prefab("Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab",actors,new Vector2(-7,-1),"平民 — 室内视野对照");
        var first=Prefab("Assets/Prefabs/Level2NPCS/Patrol/ArabSoldier-level2-Patrol.prefab",actors,new Vector2(11.8f,-4),"巡逻士兵 A — 街道");
        Route(first, new[]{Vector2.zero,new Vector2(0,8),new Vector2(-1.5f,8),new Vector2(-1.5f,0)});
        var second=Prefab("Assets/Prefabs/Level2NPCS/Patrol/ArabSoldier-level2-Patrol.prefab",actors,new Vector2(1,8),"巡逻士兵 B — 权限区");
        Route(second, new[]{Vector2.zero,new Vector2(6,0),new Vector2(6,2.5f),new Vector2(0,2.5f)});
        foreach(var actor in InScene<ZeldaCharacterData>(lab))
        {
            Edit(actor,"persistentIdentity",p=>p.objectReferenceValue=null);
            Edit(actor,"sceneCameraOverrides",p=>p.arraySize=0);
            actor.GetComponent<ZeldaFourWayMover>().enabled=false;
        }
    }
    static void Route(GameObject actor, Vector2[] points)
    {
        var ai=actor.GetComponent<ZeldaPatrolCharacterAi>();
        Edit(ai,"useCustomPatrolRoute",p=>p.boolValue=true);
        Edit(ai,"customPatrolPoints",p=>{p.arraySize=points.Length; for(int i=0;i<points.Length;i++) {var e=p.GetArrayElementAtIndex(i);e.FindPropertyRelative("offset").vector2Value=points[i];e.FindPropertyRelative("waitDuration").floatValue=1.5f;}});
    }
    static void BuildItems()
    {
        Prefab("Assets/Prefabs/Decorations/AlchemyWorkshop/Alchemy-Cauldron.prefab",shop,new Vector2(5,3),"炼药锅 — 动态液体与交互");
        string[] names={"Potions/RedHerbPowderPickupItem","Potions/GreenHerbPowderPickupItem","Potions/BlueHerbPowderPickupItem","Potions/GlassBottlePickupItem","Potions/Potion-Might","Bombs/BombPickupItem","BatteryPickupItem"};
        for(int i=0;i<names.Length;i++)
            Prefab("Assets/Resources/PickupItems/"+names[i]+".prefab",pickups,new Vector2(-6+i*1.65f,-.5f));
    }
    static Material LocalMaterial(Material source)
    {
        if(Materials.TryGetValue(source,out var local)) return local;
        string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        string safe=string.Concat(source.name.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_'));
        string path=AssetsFolder+"/Materials/"+safe+"-"+(guid.Length>8?guid.Substring(0,8):"builtin")+".mat";
        local=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!local) {local=new Material(source); AssetDatabase.CreateAsset(local,path);}
        Materials[source]=local; return local;
    }
    static void VerifyAuthoring()
    {
        if(importedProps<5) throw new Exception("Missing original room furnishings");
        if(InScene<Camera>(lab).Length!=1 || InScene<GhostPlayerSpawnPoint>(lab).Length!=1 || InScene<ZeldaCharacterData>(lab).Length!=5)
            throw new Exception("Unexpected infrastructure or actor count");
        if(InScene<SceneTransitionPoint>(lab).Length!=0) throw new Exception("Lab must not lead into production scenes");
        if(EditorBuildSettings.scenes.Any(s=>s.path==ScenePath)) throw new Exception("Lab must remain outside production build list");
        foreach(var c in InScene<Component>(lab))
        {
            if(c==null) throw new Exception("Missing component");
            var so=new SerializedObject(c);var p=so.GetIterator();
            while(p.Next(true)) if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue is Component target &&
                !EditorUtility.IsPersistent(target) && target.gameObject.scene!=lab && target.gameObject.scene.IsValid())
                throw new Exception(c.name+" retains an external scene reference: "+p.propertyPath);
        }
    }
}
#endif
