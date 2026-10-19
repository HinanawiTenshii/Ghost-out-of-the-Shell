using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class LevelSettlementCatalogBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 10;
    public void OnPreprocessBuild(BuildReport report) { Rebuild(); }
    [MenuItem("Tools/Level Settlement/Rebuild Authored Totals")]
    public static void Rebuild()
    {
        const string path="Assets/Resources/LevelSettlementCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<LevelSettlementCatalog>(path);
        if(catalog==null) { catalog=ScriptableObject.CreateInstance<LevelSettlementCatalog>();AssetDatabase.CreateAsset(catalog,path); }
        catalog.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
        foreach(var template in Resources.LoadAll<GameObject>("PickupItems"))
            if(template.GetComponent<MemoPickupItemVisual>() is MemoPickupItemVisual visual)catalog.expansionVisual=visual;
        catalog.levels=new[]{
            new LevelSettlementCatalog.Level{title="监牢",entryScene="Level0",nextScene="Level1-Garden"},
            new LevelSettlementCatalog.Level{title="城堡",entryScene="Level1-Garden",nextScene="Level2-Desert"},
            new LevelSettlementCatalog.Level{title="边境城市",entryScene="Level2-Desert",nextScene="LastScene"}};
        var characterSkills=new[]{new HashSet<string>(),new HashSet<string>(),new HashSet<string>()};
        foreach(var guid in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}))
        {
            string scenePath=AssetDatabase.GUIDToAssetPath(guid), name=System.IO.Path.GetFileNameWithoutExtension(scenePath);
            int level=LevelSettlementCatalog.LevelIndex(name);if(level<0)continue;
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try {
                foreach(var root in scene.GetRootGameObjects()) {
                    foreach(var item in root.GetComponentsInChildren<GrowthCollectible>(true))
                        catalog.levels[level].skillPoints+=Mathf.Max(1,new SerializedObject(item).FindProperty("collectibleAmount").intValue);
                    foreach(var item in root.GetComponentsInChildren<MemoPickupItem>(true))
                        catalog.levels[level].inventoryExpansions++;
                    foreach(var actor in root.GetComponentsInChildren<ZeldaCharacterData>(true))
                        AddCharacterSkill(characterSkills[level],actor.NativeCharacterSkillId);
                    foreach(var circle in root.GetComponentsInChildren<SummoningCircle>(true))
                        if(circle.SummonPrefab!=null)AddCharacterSkill(characterSkills[level],circle.SummonPrefab.NativeCharacterSkillId);
                    foreach(var relic in root.GetComponentsInChildren<SilenceSealRelicPickupItem>(true))
                        AddCharacterSkill(characterSkills[level],SilenceOathRuntime.SkillId);
                }
            } finally { if(opened)EditorSceneManager.CloseScene(scene,true); }
        }
        var roots=new List<PlayerGrowthAttributes.SkillDefinition>();
        foreach(var skill in PlayerGrowthAttributes.Skills)
            if(skill.id==skill.requiredPossessedSkillId)roots.Add(skill);
        roots.Sort((a,b)=>a.position.y!=b.position.y?b.position.y.CompareTo(a.position.y):a.position.x.CompareTo(b.position.x));
        for(int i=0;i<3;i++)
        {
            var ordered=new List<string>();
            foreach(var skill in roots)if(characterSkills[i].Contains(skill.id))ordered.Add(skill.id);
            catalog.levels[i].characterSkills=ordered.ToArray();
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        for(int i=0;i<3;i++)Debug.Log("Settlement Level"+i+": DATA="+catalog.levels[i].skillPoints+", MEMO="+catalog.levels[i].inventoryExpansions+", Skills="+string.Join(",",catalog.levels[i].characterSkills));
    }
    private static void AddCharacterSkill(HashSet<string> skills,string id)
    {
        if(string.IsNullOrEmpty(id))return;
        foreach(var definition in PlayerGrowthAttributes.Skills)
            if(definition.id==id && definition.requiredPossessedSkillId==id){skills.Add(id);return;}
    }
}
