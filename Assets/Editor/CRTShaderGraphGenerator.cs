using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CRTShaderGraphGenerator
{
    private const string GraphPath = "Assets/Shaders/CRTScreen.shadergraph";
    private const string IncludePath = "Assets/Shaders/CRTScreen.hlsl";
    // Keep the editable graph material separate from the material used by the
    // Built-in full-screen Blit pass.
    private const string MaterialPath = "Assets/Materials/CRTScreenGraph.mat";

    static CRTShaderGraphGenerator()
    {
        if (NeedsGeneration())
            EditorApplication.delayCall += GenerateOnceAfterReload;
    }

    private static void GenerateOnceAfterReload()
    {
        EditorApplication.delayCall -= GenerateOnceAfterReload;
        if (!EditorApplication.isCompiling && NeedsGeneration())
            Generate();
    }

    private static bool NeedsGeneration()
    {
        return !File.Exists(GraphPath)
            || AssetDatabase.LoadAssetAtPath<Shader>(GraphPath) == null
            || AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null;
    }

    [MenuItem("Tools/Rendering/Generate CRT Shader Graph")]
    public static void Generate()
    {
        AssetDatabase.ImportAsset(IncludePath, ImportAssetOptions.ForceSynchronousImport);

        Type graphType = FindType("UnityEditor.ShaderGraph.GraphData");
        Type targetType = FindType("UnityEditor.ShaderGraph.Target");
        Type builtInTargetType = FindType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInTarget");
        Type builtInUnlitType = FindType("UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInUnlitSubTarget");
        Type descriptorType = FindType("UnityEditor.ShaderGraph.BlockFieldDescriptor");
        Type blockFieldsType = FindType("UnityEditor.ShaderGraph.BlockFields");
        Type blockNodeType = FindType("UnityEditor.ShaderGraph.BlockNode");
        Type screenPositionType = FindType("UnityEditor.ShaderGraph.ScreenPositionNode");
        Type customFunctionType = FindType("UnityEditor.ShaderGraph.CustomFunctionNode");
        Type vector4SlotType = FindType("UnityEditor.ShaderGraph.Vector4MaterialSlot");
        Type slotType = FindType("UnityEditor.Graphing.SlotType");
        Type stageType = FindType("UnityEditor.ShaderGraph.ShaderStageCapability");

        object graph = Activator.CreateInstance(graphType, true);
        graphType.GetMethod("AddContexts", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(graph, null);
        graphType.GetProperty("path", BindingFlags.Instance | BindingFlags.Public)
            ?.SetValue(graph, "Shader Graphs");

        object target = Activator.CreateInstance(builtInTargetType, true);
        builtInTargetType.GetMethod("TrySetActiveSubTarget", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(target, new object[] { builtInUnlitType });

        object position = GetNestedStaticField(blockFieldsType, "VertexDescription", "Position");
        object normal = GetNestedStaticField(blockFieldsType, "VertexDescription", "Normal");
        object tangent = GetNestedStaticField(blockFieldsType, "VertexDescription", "Tangent");
        object baseColor = GetNestedStaticField(blockFieldsType, "SurfaceDescription", "BaseColor");

        Array targets = Array.CreateInstance(targetType, 1);
        targets.SetValue(target, 0);
        Array descriptors = Array.CreateInstance(descriptorType, 4);
        descriptors.SetValue(position, 0);
        descriptors.SetValue(normal, 1);
        descriptors.SetValue(tangent, 2);
        descriptors.SetValue(baseColor, 3);
        graphType.GetMethod("InitializeOutputs", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(graph, new object[] { targets, descriptors });

        MethodInfo getNodes = graphType.GetMethod("GetNodes", BindingFlags.Instance | BindingFlags.Public)
            .MakeGenericMethod(blockNodeType);
        object baseColorBlock = ((IEnumerable)getNodes.Invoke(graph, null)).Cast<object>()
            .First(node => ReferenceEquals(
                blockNodeType.GetProperty("descriptor", BindingFlags.Instance | BindingFlags.Public)?.GetValue(node),
                baseColor));

        object screenPosition = Activator.CreateInstance(screenPositionType, true);
        object customFunction = Activator.CreateInstance(customFunctionType, true);
        customFunctionType.GetProperty("functionName", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(customFunction, "CRT");
        customFunctionType.GetProperty("functionSource", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(customFunction, AssetDatabase.AssetPathToGUID(IncludePath));

        object fileEnum = Enum.Parse(
            customFunctionType.GetProperty("sourceType", BindingFlags.Instance | BindingFlags.Public).PropertyType,
            "File");
        customFunctionType.GetProperty("sourceType", BindingFlags.Instance | BindingFlags.Public)
            .SetValue(customFunction, fileEnum);

        object inputEnum = Enum.Parse(slotType, "Input");
        object outputEnum = Enum.Parse(slotType, "Output");
        object allStages = Enum.Parse(stageType, "All");
        ConstructorInfo slotConstructor = vector4SlotType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First(constructor => constructor.GetParameters().Length == 11);

        object inputSlot = slotConstructor.Invoke(new object[]
        {
            0, "Screen Position", "ScreenPosition", inputEnum, Vector4.zero,
            allStages, null, null, null, null, false
        });
        object outputSlot = slotConstructor.Invoke(new object[]
        {
            1, "Color", "Out", outputEnum, Vector4.zero,
            allStages, null, null, null, null, false
        });

        MethodInfo addSlot = FindMethod(customFunctionType, "AddSlot", 2);
        addSlot.Invoke(customFunction, new[] { inputSlot, (object)true });
        addSlot.Invoke(customFunction, new[] { outputSlot, (object)true });

        SetNodePosition(screenPosition, new Rect(-520f, 80f, 210f, 150f));
        SetNodePosition(customFunction, new Rect(-230f, 80f, 240f, 180f));

        MethodInfo addNode = graphType.GetMethod("AddNode", BindingFlags.Instance | BindingFlags.Public);
        addNode.Invoke(graph, new[] { screenPosition });
        addNode.Invoke(graph, new[] { customFunction });

        MethodInfo getSlotReference = FindMethod(screenPositionType, "GetSlotReference", 1);
        object screenOutput = getSlotReference.Invoke(screenPosition, new object[] { 0 });
        object functionInput = FindMethod(customFunctionType, "GetSlotReference", 1)
            .Invoke(customFunction, new object[] { 0 });
        object functionOutput = FindMethod(customFunctionType, "GetSlotReference", 1)
            .Invoke(customFunction, new object[] { 1 });
        object blockInput = FindMethod(blockNodeType, "GetSlotReference", 1)
            .Invoke(baseColorBlock, new object[] { 0 });

        MethodInfo connect = graphType.GetMethod("Connect", BindingFlags.Instance | BindingFlags.Public);
        connect.Invoke(graph, new[] { screenOutput, functionInput });
        connect.Invoke(graph, new[] { functionOutput, blockInput });

        Type multiJsonType = FindType("UnityEditor.ShaderGraph.Serialization.MultiJson");
        string json = (string)multiJsonType.GetMethod("Serialize", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new[] { graph });
        File.WriteAllText(GraphPath, json);
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
        if (shader == null)
            throw new InvalidOperationException("CRT Shader Graph was written but Unity could not import its Shader.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            material = new Material(shader) { name = "CRTScreen" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("CRT Shader Graph generated at " + GraphPath);
    }

    private static Type FindType(string fullName)
    {
        Type result = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
        return result ?? throw new InvalidOperationException("Shader Graph type not found: " + fullName);
    }

    private static object GetNestedStaticField(Type parent, string nestedName, string fieldName)
    {
        Type nested = parent.GetNestedType(nestedName, BindingFlags.Public | BindingFlags.NonPublic);
        return nested.GetField(fieldName, BindingFlags.Static | BindingFlags.Public).GetValue(null);
    }

    private static MethodInfo FindMethod(Type type, string name, int parameterCount)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First(method => method.Name == name && method.GetParameters().Length == parameterCount);
    }

    private static void SetNodePosition(object node, Rect position)
    {
        PropertyInfo property = node.GetType().GetProperty(
            "drawState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object state = property.GetValue(node);
        PropertyInfo positionProperty = state.GetType().GetProperty(
            "position", BindingFlags.Instance | BindingFlags.Public);
        positionProperty.SetValue(state, position);
        property.SetValue(node, state);
    }
}
