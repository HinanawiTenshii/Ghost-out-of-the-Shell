#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class LevelVisualRollout
{
    public static void CompleteFountains()
    {
        Isolate();
        foreach(var path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path);int count=0;
            var surfaces=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            foreach(var s in surfaces)
            {
                if(s.name!="Water"||s.transform.parent==null||!s.transform.parent.name.StartsWith("Fountain")||s.GetComponent<SpriteStillWaterSurface>()||s.GetComponent<SpriteWaterFlowParticles>())continue;
                var ripple=s.gameObject.AddComponent<SpriteStillWaterSurface>();Ref(ripple,"sourceRenderer",s);
                Number(ripple,"settings.density",.16f);Number(ripple,"settings.width",.08f);Number(ripple,"settings.length",.35f);
                // Keep the authored blue water, use sparse restrained crests, no replacement fill.
                count++;
            }
            if(count>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            Debug.Log("LEVEL_FOUNTAIN_PASS "+scene.name+" added="+count);
        }
    }
}
#endif
