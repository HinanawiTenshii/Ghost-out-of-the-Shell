using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Read-only authored-scene inspection in the disposable Unity project.</summary>
public static class Squad4RouteProbe
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    [Serializable] class Actor { public string name, globalId; public Vector3 position; public Vector2 body; public Vector3[] points; public int members; }
    [Serializable] class Obstacle { public string path, layer; public Vector3 center, size; }
    [Serializable] class Report { public Actor[] actors; public Obstacle[] obstacles; }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var squad = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "Squad4");
        var actors = squad.GetComponentsInChildren<SquadPatrolCharacterAi>(true).Select(ai => {
            var serialized = new SerializedObject(ai);
            int count = (int)typeof(ZeldaPatrolCharacterAi).GetProperty("ConfiguredPointCount", F).GetValue(ai);
            var pts = Enumerable.Range(0,count).Select(i => (Vector3)typeof(ZeldaPatrolCharacterAi).GetMethod("PreviewPoint", F).Invoke(ai,new object[]{i})).ToArray();
            var body = ai.GetComponent<BoxCollider2D>();
            return new Actor { name=ai.name, globalId=GlobalObjectId.GetGlobalObjectIdSlow(ai).ToString(), position=ai.transform.position,
                body=body != null ? body.size : Vector2.zero, points=pts, members=serialized.FindProperty("members").arraySize };
        }).ToArray();
        var leader = actors.Single(a => a.members > 0);
        Bounds area = new Bounds(leader.position, Vector3.zero); foreach (var p in leader.points) area.Encapsulate(p);
        area.Expand(new Vector3(20,20,100));
        var obstacles = Object.FindObjectsOfType<Collider2D>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
            c.GetComponentInParent<ZeldaCharacterData>() == null && c.bounds.Intersects(area)).Select(c => new Obstacle {
                path=PathOf(c.transform), layer=LayerMask.LayerToName(c.gameObject.layer), center=c.bounds.center, size=c.bounds.size }).ToArray();
        File.WriteAllText(Application.dataPath+"/../Squad4-route.json",JsonUtility.ToJson(new Report {actors=actors,obstacles=obstacles},true));
        var line = new GameObject("Route Probe").AddComponent<LineRenderer>(); line.useWorldSpace=true;line.positionCount=leader.points.Length;
        line.SetPositions(leader.points.Select(p=>new Vector3(p.x,p.y,-1)).ToArray());line.loop=true;line.startWidth=line.endWidth=.13f;
        line.material=new Material(Shader.Find("Sprites/Default")); line.startColor=line.endColor=new Color(1,.5f,.05f);line.sortingOrder=32000;
        for(int i=0;i<leader.points.Length;i++)
        {
            var label=new GameObject("Point "+i).AddComponent<TextMesh>();label.transform.position=leader.points[i]+new Vector3(.4f,.4f,-2);
            label.text=(i+1).ToString();label.fontSize=60;label.characterSize=.15f;label.color=Color.red;label.GetComponent<MeshRenderer>().sortingOrder=32001;
        }
        var camera=new GameObject("Probe Camera").AddComponent<Camera>(); camera.transform.position=new Vector3(area.center.x,area.center.y,-100);
        camera.orthographic=true;camera.orthographicSize=area.size.y*.5f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(45,45,45,255);
        int height=1400,width=Mathf.RoundToInt(height*area.size.x/area.size.y);
        var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Application.dataPath+"/../Squad4-route.png",image.EncodeToPNG());
        RenderTexture.active=old;
        Debug.Log("SQUAD4_PROBE_PASS "+JsonUtility.ToJson(new Report {actors=actors}));EditorApplication.Exit(0);
    }
}
