$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw "$root/Assets/Scripts/Camera/CameraVisionObjectStreaming.cs"
$methods = [regex]::Match($source, '(?s)    private bool IsInsideStreamingRetentionRange.*?(?=    /// <summary>Used by newly placed)').Value
if (!$methods) { throw 'Streaming methods not found' }
if ($source.Contains('VisionMaskRadius') -or $source.Contains('IsWorldBoundsVisible(')) {
    throw 'Streaming must not depend on character vision radius or occlusion'
}
# Execute the production retention/viewport/urgent-wake methods. Only Unity
# value types and camera plane extraction are stand-ins; the editor build
# separately checks the actual Unity API. Does not claim a Play Mode test.
$stubs = @'
using System;
using System.Collections.Generic;
public struct Vector3 {
    public float x,y,z;
    public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
}
public struct Bounds {
    public Vector3 center,extents;
    public Bounds(Vector3 c,Vector3 size){center=c;extents=new Vector3(size.x/2,size.y/2,size.z/2);}
}
public struct Plane {
    public Vector3 normal; public float distance;
    public Plane(Vector3 n,float d){normal=n;distance=d;}
    public float GetDistanceToPoint(Vector3 p){return normal.x*p.x+normal.y*p.y+normal.z*p.z+distance;}
}
public static class Mathf {
    public static float Abs(float x){return Math.Abs(x);}
    public static float Max(float a,float b){return Math.Max(a,b);}
    public static float Min(float a,float b){return Math.Min(a,b);}
}
public class Transform {public Vector3 position;}
public class GameObject {
    public int scene;
    public Transform transform=new Transform(); public bool activeSelf;
    public void SetActive(bool value){activeSelf=value;}
}
public class Camera {
    public bool orthographic=true;public float orthographicSize=5,aspect=2,rotation;
    public Vector3 position;
}
public static class GeometryUtility {
    public static void CalculateFrustumPlanes(Camera c,Plane[] p){
        float cos=(float)Math.Cos(c.rotation),sin=(float)Math.Sin(c.rotation);
        var right=new Vector3(cos,sin);var up=new Vector3(-sin,cos);
        float x=right.x*c.position.x+right.y*c.position.y;
        float y=up.x*c.position.x+up.y*c.position.y;
        p[0]=new Plane(right,c.orthographicSize*c.aspect-x);
        p[1]=new Plane(new Vector3(-right.x,-right.y),c.orthographicSize*c.aspect+x);
        p[2]=new Plane(up,c.orthographicSize-y);
        p[3]=new Plane(new Vector3(-up.x,-up.y),c.orthographicSize+y);
        p[4]=new Plane(new Vector3(0,0,1),10);
        p[5]=new Plane(new Vector3(0,0,-1),90);
    }
}
public class Reveal {public bool enabled; public bool IsWorldBoundsInsideAdditionalReveal(Bounds b){return enabled;}}
// Region behavior is exercised separately in TestItemStreaming.ps1.
public static class CameraVisionStreamingRegion { public static bool Intersects(int scene,Bounds b){return false;} }
public class ViewportStreamingRegression {
    private class ManagedObject {
        public GameObject gameObject=new GameObject(); public Bounds localBounds;
        public bool suspendedByManager,containsVisionBlockLayer; public float outsideTimer;
    }
    private Camera streamingCamera=new Camera();
    private Reveal circularVision=new Reveal();
    private Plane[] streamingPlanes=new Plane[6];
    private bool hasStreamingPlanes;
    private float activationPreloadPadding=1.25f,viewportPreloadFraction=.1f,currentPreloadPadding,blockVisibilityHysteresis=.6f;
    private List<ManagedObject> managedObjects=new List<ManagedObject>();
    private T GetComponent<T>() where T:class {return streamingCamera as T;}
    private static Bounds TransformLocalBoundsToWorld(Transform t,Bounds b){
        b.center=new Vector3(b.center.x+t.position.x,b.center.y+t.position.y,b.center.z+t.position.z);return b;
    }
'@
$tests = @'
    static int checks;
    static void Check(bool result,string message){checks++;if(!result)throw new Exception(message);}
    ManagedObject At(float x,float y,float width=.1f,float height=.1f){
        var o=new ManagedObject {localBounds=new Bounds(new Vector3(x,y),new Vector3(width,height,.01f)),suspendedByManager=true};
        return o;
    }
    bool Inside(float x,float y){return IsInsideStreamingRetentionRange(At(x,y));}
    public static int Run(){
        var t=new ViewportStreamingRegression();t.RefreshStreamingViewport();
        Check(t.Inside(9.8f,4.8f),"Rectangular viewport corners remain loaded");
        Check(t.Inside(11,6),"Preload includes corners beyond both edges");
        Check(!t.Inside(11.5f,0),"Outside horizontal preload unloads");
        Check(!t.Inside(0,6.5f),"Outside vertical preload unloads");
        Check(t.Inside(-11,-6),"Opposite preload edges are symmetric");
        Check(t.IsInsideStreamingRetentionRange(t.At(12,0,4,1)),"Large objects overlapping edge are retained even with center outside");
        Check(!t.Inside(18,0),"Initial size excludes distant object");
        t.streamingCamera.orthographicSize=10;t.RefreshStreamingViewport();
        Check(t.Inside(18,0),"Zoom out immediately expands loading rectangle");
        Check(Math.Abs(t.currentPreloadPadding-2)<.001,"Preload grows with camera size");
        t.streamingCamera.orthographicSize=3;t.RefreshStreamingViewport();
        Check(!t.Inside(18,0),"Zoom in shrinks loading rectangle");
        t.streamingCamera.orthographicSize=5;t.streamingCamera.aspect=.5f;t.RefreshStreamingViewport();
        Check(!t.Inside(4,0) && t.Inside(0,4),"Portrait aspect changes correct boundaries");
        t.streamingCamera.aspect=2;t.streamingCamera.rotation=(float)Math.PI/2;t.RefreshStreamingViewport();
        Check(t.Inside(0,9) && !t.Inside(9,0),"Rotated viewport respects all planes");
        t.streamingCamera.rotation=0;t.streamingCamera.position=new Vector3(100,20);t.RefreshStreamingViewport();
        Check(t.Inside(109,24) && !t.Inside(0,0),"Camera translation updates immediately");
        t.streamingCamera.position=new Vector3();t.RefreshStreamingViewport();
        var block=t.At(11.6f,0);block.containsVisionBlockLayer=true;
        Check(!t.IsInsideStreamingRetentionRange(block),"Suspended block uses preload wake boundary");
        block.suspendedByManager=false;
        Check(t.IsInsideStreamingRetentionRange(block),"Active block has extra retention hysteresis");
        t.circularVision.enabled=true;
        Check(t.Inside(1000,1000),"Additional skill reveal retention preserved");
        t.circularVision=null;
        Check(t.Inside(0,0) && !t.Inside(1000,1000),"Camera streaming works independently of vision component");
        var near=t.At(10.5f,0);var outer=t.At(11,0);var far=t.At(20,0);
        t.managedObjects.Add(near);t.managedObjects.Add(outer);t.managedObjects.Add(far);
        for(int i=0;i<100;i++)t.managedObjects.Add(t.At(i*.05f,0));
        t.RestoreObjectsNearViewport();
        Check(near.gameObject.activeSelf && !near.suspendedByManager,"Inner preload band restores before entering screen");
        Check(outer.suspendedByManager && far.suspendedByManager,"Outer band still uses background activation budget");
        for(int i=3;i<t.managedObjects.Count;i++)Check(!t.managedObjects[i].suspendedByManager,"Visible object never waits for per-frame activation quota");
        t.streamingCamera.orthographicSize=12;t.RefreshStreamingViewport();t.RestoreObjectsNearViewport();
        Check(!far.suspendedByManager,"Sudden zoom out wakes newly visible objects in same frame");
        var behind=t.At(0,0);behind.localBounds=new Bounds(new Vector3(0,0,-20),new Vector3(1,1,1));
        Check(!t.IsInsideStreamingRetentionRange(behind),"Near depth plane is not expanded with padding");
        return checks;
    }
}
'@
Add-Type -TypeDefinition ($stubs + $methods + $tests)
$count = [ViewportStreamingRegression]::Run()
Write-Host "Camera viewport streaming regression: $count checks passed."
