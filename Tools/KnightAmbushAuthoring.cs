using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class KnightAmbushAuthoring
{
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        const string prefabPath = "Assets/Prefabs/Level2NPCS/Troops/AdvancedPaladin-Squad.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefab.GetComponent<SquadRegenerationAura>() == null) prefab.AddComponent<SquadRegenerationAura>();
        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); PrefabUtility.UnloadPrefabContents(prefab);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var root = GameObject.Find("NPCS/PatrolSquad/骑士长小队").transform;
        var captain = root.Find("AdvancedPaladin-Squad").GetComponent<SquadPatrolCharacterAi>();
        var director = Object.FindObjectOfType<KnightCaptainAmbush>();
        if (director == null) director = new GameObject("Knight Captain Ambush").AddComponent<KnightCaptainAmbush>();
        director.captain = captain;
        director.guards = new[] { root.Find("Paladin-Squad").GetComponent<SquadPatrolCharacterAi>(),
            root.Find("Paladin-Squad (1)").GetComponent<SquadPatrolCharacterAi>(),
            root.Find("HeavyPaladin-level2-Suqad").GetComponent<SquadPatrolCharacterAi>() };
        // Preserve the original leader/route and all character data. Reassert member
        // references using Unity serialization rather than generated YAML file IDs.
        var squad = new SerializedObject(captain); var members = squad.FindProperty("members"); members.arraySize = 3;
        for (int i = 0; i < 3; i++) members.GetArrayElementAtIndex(i).objectReferenceValue = director.guards[i];
        squad.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(captain);
        var areas = Object.FindObjectsOfType<PermissionArea>();
        var doors = Object.FindObjectsOfType<DoorHingeInteraction>();
        var south = areas.Single(a => a.name == "SafeRoom1 (1)");
        var north = areas.Single(a => a.name == "SafeRoom1");
        director.safeHouses = new[] {
            new KnightCaptainAmbush.SafeHouse { name = "南侧三门安全屋", permission = south,
                entrance = doors.OrderBy(d => Vector2.Distance(d.transform.position, new Vector2(59.81f,-39.01f))).First(),
                approach = new Vector2(58.1f,-37.92f), captainPath = Points(60.9f,-37.92f, 64,-39.1f), captainFacing = Vector2.up,
                guards = new[] {
                    Post(Vector2.left,58.2f,-37.92f),
                    Post(Vector2.right,58.1f,-45.2f,70,-45.2f,70,-40.65f),
                    Post(Vector2.down,58.1f,-45.2f,66.08f,-45.2f) } },
            new KnightCaptainAmbush.SafeHouse { name = "北侧单门安全屋", permission = north,
                entrance = doors.OrderBy(d => Vector2.Distance(d.transform.position, new Vector2(56.75f,77.82f))).First(),
                approach = new Vector2(57.83f,76.1f), captainPath = Points(57.83f,79.2f,61.1f,81), captainFacing = Vector2.up,
                guards = new[] { Post(Vector2.down,56.5f,76.1f), Post(Vector2.down,59.16f,76.1f), Post(Vector2.down,57.83f,75.1f) } }
        };
        director.retreatSpeedMultiplier = 1.2f;
        var notice = Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.name == "巡逻队信息");
        var doc = new SerializedObject(notice);
        doc.FindProperty("documentText").stringValue = "巡逻队应急预案·护卫传阅\n\n骑士长外出，三名圣骑士随行。若多人负伤、阵形受挫，全队即刻加速撤往就近安全屋，不得追敌误事。\n\n骑士长留守屋内，护卫驻守门外：南屋西、东、南三处入口各一人；北屋三人同守南门。安全屋权限升至5级。\n\n页边批注：“有他在，护卫的伤口转眼就合上了。零星袭扰赶不走他们，得用加农炮或自动人偶闹场大的。但要留活口——我们要借的是他的身份。”";
        doc.FindProperty("keepAuthoredTextOnLoad").boolValue = true;
        doc.FindProperty("addJournalEntryOnOpen").boolValue = true;
        doc.FindProperty("addJournalEntryOnlyOnce").boolValue = true;
        doc.FindProperty("journalEntryId").stringValue = QuestJournalManager.KnightAmbushQuestId;
        doc.FindProperty("journalParentEntryId").stringValue = QuestJournalManager.FortressProofQuestId;
        doc.FindProperty("journalEntryTitle").stringValue = QuestJournalManager.KnightAmbushQuestTitle;
        doc.FindProperty("journalEntryDetails").stringValue = QuestJournalManager.KnightAmbushDescription;
        doc.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(notice);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[] {scene.path,"Assets/Resources/MiniMaps/Level2-Floor2.asset",scene.name});
        AssetDatabase.SaveAssets();
        Debug.Log("KNIGHT_AUTHORING_PASS captain=" + captain.transform.position + " guards=" + director.guards.Length);
        EditorApplication.Exit(0);
    }
    static Vector2[] Points(params float[] coordinates)
    { var points = new Vector2[coordinates.Length/2]; for(int i=0;i<points.Length;i++) points[i]=new Vector2(coordinates[i*2],coordinates[i*2+1]); return points; }
    static KnightCaptainAmbush.GuardPost Post(Vector2 facing,params float[] p) => new KnightCaptainAmbush.GuardPost { facing=facing,path=Points(p) };
}
