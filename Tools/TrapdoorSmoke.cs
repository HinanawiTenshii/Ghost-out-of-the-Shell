using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated editor test: actual prefab, ExecuteAlways mesh and GPU preview; no game scene is opened.
public static class TrapdoorSmoke
{
    static int checks;
    static void Check(bool v,string label){checks++;if(!v)throw new Exception(label);}
    public static void Run()
    {
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Trapdoor.prefab");
            Check(prefab!=null,"Prefab imports");
            var obj=UnityEngine.Object.Instantiate(prefab);
            var decoration=obj.GetComponent<GeometricFortressInteriorDecoration>();
            Check(decoration!=null,"Shared visual component bound");
            Check(obj.GetComponents<MonoBehaviour>().Length==1,"No gameplay scripts");
            Check(!obj.GetComponent<BoxCollider2D>().enabled,"Flat ground asset has no active collision");
            Check(obj.GetComponentsInChildren<MeshRenderer>().Length==1,"One combined renderer");
            var renderer=obj.GetComponentInChildren<MeshRenderer>();
            var mesh=obj.GetComponentInChildren<MeshFilter>().sharedMesh;
            Check(mesh.vertexCount==144,"Expected compact native geometry");
            Check(renderer.sortingOrder==-5,"Above floor, below characters and furniture");
            Check(Mathf.Abs(mesh.bounds.center.x)<.0001f&&Mathf.Abs(mesh.bounds.center.y)<.0001f,"Centered editing pivot");
            Check(Mathf.Abs(mesh.bounds.size.x-1.8f)<.0001f&&Mathf.Abs(mesh.bounds.size.y-1.8f)<.0001f,"Square top-down footprint");
            var unchanged=mesh;
            for(int i=0;i<100;i++) typeof(GeometricFortressInteriorDecoration).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(decoration,null);
            Check(obj.GetComponentInChildren<MeshFilter>().sharedMesh==unchanged,"Idle editor/runtime frames do not rebuild mesh");
            var camera=new GameObject("Preview Camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.22f;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.12f,.23f);
            var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;camera.aspect=1;
            camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,640,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
            Color center=image.GetPixel(320,320),background=image.GetPixel(10,10);
            Check(Vector3.Distance(new Vector3(center.r,center.g,center.b),new Vector3(background.r,background.g,background.b))>.15f,"Actual GPU render visible against dark floor");
            System.IO.File.WriteAllBytes(Application.dataPath+"/../Trapdoor-preview.png",image.EncodeToPNG());
            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                obj.transform.rotation=Quaternion.Euler(0,0,angle);obj.transform.localScale=new Vector3(1.4f,.75f,1);
                Check(renderer.bounds.size.x>.9f&&renderer.bounds.size.y>.9f,"Placement can rotate and scale");
                Check(!obj.GetComponent<BoxCollider2D>().enabled,"Transform never creates collision");
            }
            Debug.Log("TRAPDOOR_SMOKE_PASS checks="+checks);
            UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
