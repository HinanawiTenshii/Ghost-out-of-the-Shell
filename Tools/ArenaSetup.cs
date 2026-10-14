using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run in the isolated verification project. The generated scene/prefab are reviewed before promotion.
public static class ArenaSetup
{
    public static void Run()
    {
        try
        {
            if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            if (all.Any(t => t.GetComponent<ArenaChallengeController>() != null)) throw new Exception("Arena already installed");
            var go = new GameObject("Arena Challenge / 竞技场挑战");
            var arena = go.AddComponent<ArenaChallengeController>();
            arena.roundEndSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/ArenaRoundEndBell.wav");
            arena.playerPool = all.Single(t => t.name == "左侧恢复池").GetComponent<TemporaryCharacterEffectArea>();
            arena.guardianPool = all.Single(t => t.name == "右侧回复池").GetComponent<TemporaryCharacterEffectArea>();
            var guards = all.Select(t => t.GetComponent<ZeldaCharacterData>()).Where(c => c != null &&
                c.transform.position.x > 89 && c.transform.position.x < 108 && c.transform.position.y < -9 &&
                c.transform.position.y > -14 && c.name.Contains("Paladin")).OrderBy(c => c.transform.position.x).ToArray();
            if (guards.Length != 3) throw new Exception("Expected exactly three guardians, found " + guards.Length);
            arena.guardians = guards;
            arena.residents = all.Select(t => t.GetComponent<ZeldaCharacterAiBase>()).Where(ai => ai != null && arena.region.Contains(ai.transform.position)).ToArray();
            var doors = all.Select(t => t.GetComponent<DoorHingeInteraction>()).Where(d => d != null && arena.region.Contains(d.transform.position)).ToArray();
            arena.entrance = doors.Single(d => d.name == "DoubleDoorHinge (3)");
            arena.sealedDoors = doors.Where(d => d != arena.entrance).ToArray();
            arena.reward = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PickupItems/TrialProofPickupItem.prefab").GetComponent<TrialProofPickupItem>();
            var official = all.Single(t => t.name == "报名官").gameObject;
            var dialogue = official.GetComponent<CharacterDialogue>() ?? official.AddComponent<CharacterDialogue>();
            arena.registrar = dialogue;
            var register = official.AddComponent<ArenaDialogueAction>(); register.arena = arena; register.operation = ArenaDialogueAction.Operation.Register;
            var abandon = official.AddComponent<ArenaDialogueAction>(); abandon.arena = arena; abandon.operation = ArenaDialogueAction.Operation.Abandon;
            var claim = official.AddComponent<ArenaDialogueAction>(); claim.arena = arena; claim.operation = ArenaDialogueAction.Operation.Claim;
            var so = new SerializedObject(dialogue);
            so.FindProperty("speakerName").stringValue = "报名官";
            so.FindProperty("arenaChallenge").objectReferenceValue = arena;
            var nodes = so.FindProperty("nodes"); nodes.arraySize = 7;
            Node(nodes, 0, "arena_start", "这里是守护者竞技场。想得到试炼证明，就在场内逐一战胜右侧休息室里的所有守护者。\n\n刀剑点到为止，体力见底便判负。每轮开始前可在左侧恢复池休整；走入大门后，比试即刻开始。上场消耗的物品与力量不会返还。你准备好了吗？",
                new[] { "再考虑一下。", "报名参赛。" }, new DialogueOptionEffect[] { null, register });
            Node(nodes, 1, "arena_prepare", "守护者已经就位。先在恢复池中养足精神，再从大门走入场地。\n\n还没踏进竞技场，就仍有退出的机会。你要继续，还是放弃这次挑战？",
                new[] { "我这就入场。", "放弃挑战。" }, new DialogueOptionEffect[] { null, abandon });
            Node(nodes, 2, "arena_victory", "是你！我记得你站到最后的样子。守护者已经认可你的本领，这枚试炼证明属于你。\n\n拿好它，去向试炼大门前的祭坛证明自己吧。",
                new[] { "稍后再来。", "领取试炼证明。" }, new DialogueOptionEffect[] { null, claim }, "arena_claimed");
            Node(nodes, 3, "arena_claimed", "证明已经交到你手上了，本届挑战也就此结束，不再接受报名。\n\n真正的试炼还在门后，愿你的勇气不只留在这座竞技场里。", new[] { "再见。" }, null);
            Node(nodes, 4, "arena_other", "今天的挑战已有胜者。证明只交给亲自完成比试的那位挑战者，不能让别人代领。", new[] { "再见。" }, null);
            Node(nodes, 5, "arena_retry", "可惜了……上一位胜者还没来领取证明，就已身故。荣耀不能由旁人代领，那份参赛记录已经作废。\n\n证明仍在我这里，竞技场重新接受报名。若你也想得到它，就亲自战胜所有守护者。你准备好了吗？", new[] { "再考虑一下。", "报名参赛。" }, new DialogueOptionEffect[] { null, register });
            Node(nodes, 6, "arena_closed", "你来晚了。本届挑战的试炼证明已经颁给胜者，竞技场不再接受报名。\n\n证明一经颁发，就不会再补发；无论谁再来，也不能重新参加这场挑战。", new[] { "再见。" }, null);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(dialogue);

            var board = new GameObject("ArenaScoreboard");
            var score = board.AddComponent<ArenaScoreboard>();
            var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Part(board.transform, "Outer frame", square, new Vector2(6.9f, 1.7f), new Color(.35f,.45f,.5f), 0);
            Part(board.transform, "Dark face", square, new Vector2(6.65f,1.45f), new Color(.035f,.07f,.15f), 1);
            var textGo = new GameObject("Score text"); textGo.transform.SetParent(board.transform,false);
            textGo.layer = LayerMask.NameToLayer("Visible Non Blocking");
            var text = textGo.AddComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
            text.fontSize = 48; text.characterSize = .2f; text.color = ZeldaUiPalette.Primary; text.text = "等待挑战者";
            var renderer = textGo.GetComponent<MeshRenderer>(); renderer.sharedMaterial = text.font.material; renderer.sortingOrder = 2;
            score.display = text;
            string path = "Assets/Prefabs/Decorations/ArenaScoreboard.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(board, path);
            UnityEngine.Object.DestroyImmediate(board);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            placed.transform.SetParent(go.transform,false); placed.transform.position = new Vector3(87.7f, 6.5f,0);
            arena.scoreboard = placed.GetComponent<ArenaScoreboard>();
            EditorUtility.SetDirty(arena);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("ARENA_SETUP_PASS guardians="+guards.Length+" doors="+doors.Length+" residents="+arena.residents.Length);
            EditorApplication.Exit(0);
        }
        catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static void Part(Transform parent,string name,Sprite sprite,Vector2 size,Color color,int order)
    {
        var go = new GameObject(name); go.transform.SetParent(parent,false);
        go.layer = LayerMask.NameToLayer("Visible Non Blocking");
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Sliced; sr.size = size;
        sr.color = color; sr.sortingOrder = order; sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
    }
    static void Node(SerializedProperty nodes,int index,string id,string text,string[] choices,DialogueOptionEffect[] effects,string next = "")
    {
        var node = nodes.GetArrayElementAtIndex(index); node.FindPropertyRelative("id").stringValue = id;
        node.FindPropertyRelative("text").stringValue = text;
        node.FindPropertyRelative("differentActorOpeningText").stringValue = "";
        var options = node.FindPropertyRelative("choices"); options.arraySize = choices.Length;
        for(int i=0;i<choices.Length;i++)
        {
            var option = options.GetArrayElementAtIndex(i); option.FindPropertyRelative("text").stringValue = choices[i];
            option.FindPropertyRelative("action").enumValueIndex = i == 1 && next != "" ? 0 : 1;
            option.FindPropertyRelative("nextNodeId").stringValue = next;
            option.FindPropertyRelative("effect").objectReferenceValue = effects != null ? effects[i] : null;
        }
    }
}
