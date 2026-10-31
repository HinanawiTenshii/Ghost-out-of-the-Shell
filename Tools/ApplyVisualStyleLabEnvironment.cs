#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Applies proposal five/six only to the already-authored isolated lab copy.
public static class ApplyVisualStyleLabEnvironment
{
    const string ScenePath="Assets/Scenes/Experiments/VisualStyleLab.unity";
    const string Art="Assets/Art/VisualStyleLab/";
    static void Set(UnityEngine.Object obj,string field,Action<SerializedProperty> action)
    {
        var so=new SerializedObject(obj);var p=so.FindProperty(field);
        if(p==null)throw new Exception(obj.name+" missing "+field);
        action(p);so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Number(UnityEngine.Object obj,string field,float value)=>Set(obj,field,p=>p.floatValue=value);
    static void Reference(UnityEngine.Object obj,string field,UnityEngine.Object value)=>Set(obj,field,p=>p.objectReferenceValue=value);
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated lab only");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true))
            .Where(c=>c!=null&&(c.hideFlags&HideFlags.DontSave)==0).ToArray();
        if(components.Any(c=>c is VisualStyleLabVisionStyle||c is VisualStyleLabPlantMotion||c is VisualStyleLabMechanismSignal))
            throw new Exception("Experiment already applied; refuse overwriting edits");
        var snapshot=typeof(ApplyVisualStyleLabActorsProps).GetMethod("GameplayFields",BindingFlags.NonPublic|BindingFlags.Static);
        var unchanged=components.Where(c=>!(c is Renderer)&&!(c is MeshFilter)&&!(c is KeroseneStreetLamp)&&!(c is SpriteWaterFlowParticles)&&!(c is BatterySocketCable))
            .ToDictionary(c=>c,c=>(string)snapshot.Invoke(null,new object[]{c}));
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"VisionStudy.shader");
        if(shader==null||!shader.isSupported)throw new Exception("Missing lab vision shader");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"ActorPropStudy/ContactAndRim.mat");
        var tree=components.OfType<GeometricTreeDecoration>().Single();
        var motion=tree.gameObject.AddComponent<VisualStyleLabPlantMotion>();
        Set(motion,"meshCanopy",p=>p.boolValue=true);Number(motion,"phase",1.2f);
        var bush=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="BreakableBush");
        motion=bush.gameObject.AddComponent<VisualStyleLabPlantMotion>();Number(motion,"amplitude",.035f);Number(motion,"phase",3.7f);
        foreach(var lamp in components.OfType<KeroseneStreetLamp>())
        {
            Number(lamp,"radiusBreathing",.018f);Number(lamp,"alphaBreathing",.07f);Number(lamp,"breathingPeriod",4.6f);
            Number(lamp,"glowIntensity",2.3f);Number(lamp,"glowRadiusMultiplier",1.2f);
        }
        foreach(var flow in components.OfType<SpriteWaterFlowParticles>())
        {
            flow.GetComponent<SpriteRenderer>().color=new Color(35/255f,88/255f,126/255f,1);
            Number(flow,"speed",.42f);Number(flow,"speedRandomness",.16f);Number(flow,"directionSpread",4);
            Number(flow,"lifetime",3);Number(flow,"particlesPerSquareUnit",1.4f);Number(flow,"ribbonDensityMultiplier",.12f);
            Number(flow,"particleSize",.04f);Number(flow,"ribbonLengthScale",6);Number(flow,"ribbonWidthScale",2);
            Number(flow,"shapeAnimationSpeed",.45f);Number(flow,"currentSway",.035f);
            Set(flow,"particleColor",p=>p.colorValue=new Color(.42f,.73f,.84f,.32f));
        }
        foreach(var ai in components.OfType<ZeldaCharacterAiBase>())
            Reference(ai.gameObject.AddComponent<VisualStyleLabVisionStyle>(),"displayShader",shader);
        var socket=components.OfType<BatterySocket>().Single();
        var control=components.OfType<WaterwayGateController>().Single();
        var gate=components.OfType<WaterwayGate>().Single();
        var lever=components.OfType<LeverData>().Single();
        var door=components.OfType<DoubleSlidingDoor>().Single();
        var a=Signal(socket,new Vector2(0,.425f),material);Reference(a,"socket",socket);Reference(a,"gate",gate);
        a=Signal(control,new Vector2(0,-.50f),material);Reference(a,"controller",control);
        a=Signal(gate,new Vector2(-1.71f,.40f),material);Reference(a,"gate",gate);Reference(a,"controller",control);
        a=Signal(lever,new Vector2(0,-.025f),material);Reference(a,"lever",lever);Reference(a,"door",door);
        var cable=components.OfType<BatterySocketCable>().Single();
        Number(cable,"currentFlowLength",.35f);Number(cable,"currentFlowInterval",1.6f);
        Number(cable,"currentFlowSpeed",1.8f);Number(cable,"currentFlowIntensity",.65f);
        foreach(var pair in unchanged)
            if(pair.Key==null||pair.Value!=(string)snapshot.Invoke(null,new object[]{pair.Key}))throw new Exception("Gameplay changed: "+pair.Key);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("LAB_ENVIRONMENT_PASS plants=2 water=2 lamps=3 vision=5 signals=4 gameplay=unchanged");
    }
    static VisualStyleLabMechanismSignal Signal(Component owner,Vector2 position,Material material)
    {
        var signal=owner.gameObject.AddComponent<VisualStyleLabMechanismSignal>();
        Reference(signal,"displayMaterial",material);Set(signal,"localPosition",p=>p.vector2Value=position);
        return signal;
    }
}
#endif
