#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class LevelVisualRollout
{
    public static string[] Scenes => new[]{"Assets/Scenes/Level0.unity"}
        .Concat(Directory.GetFiles("Assets/Scenes/Level1", "*.unity"))
        .Concat(Directory.GetFiles("Assets/Scenes/Level2", "*.unity")).Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray();
    static void Isolate() { if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated clone only"); }
    public static void Inspect()
    {
        Isolate();
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
            Debug.Log("INSPECT "+path+" roots="+string.Join("|",scene.GetRootGameObjects().Select(r=>r.name))+" types="+
                string.Join(",",all.OfType<MonoBehaviour>().GroupBy(c=>c.GetType().Name).Where(g=>g.Key.Contains("Tree")||g.Key.Contains("Water")||g.Key.Contains("Character")||g.Key.Contains("Socket")||g.Key.Contains("Lamp")||g.Key.Contains("Lever")).Select(g=>g.Key+":"+g.Count())));
            foreach(var g in all.OfType<SpriteRenderer>().GroupBy(s=>LayerMask.LayerToName(s.gameObject.layer)))
                Debug.Log("LAYER "+scene.name+" "+g.Key+" count="+g.Count()+" samples="+string.Join(" | ",g.OrderByDescending(s=>s.bounds.size.x*s.bounds.size.y).Take(12).Select(s=>s.name+" parent="+s.transform.parent?.name+" dim="+s.bounds.size+" order="+s.sortingOrder+" col="+s.color+" sprite="+s.sprite?.name+" doordata="+(s.GetComponentInParent<DoorData>()!=null))));
        }
    }
}
#endif
