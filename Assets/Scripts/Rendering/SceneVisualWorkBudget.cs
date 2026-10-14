using System.Collections.Generic;
using UnityEngine;

// Never throttles visible effects. Shares camera/frustum work across decorative
// systems, and skips only CPU art uploads outside every camera plus a safety band.
public static class SceneVisualWorkBudget
{
    private static Camera[] cameras = new Camera[8];
    private static readonly List<Plane[]> planes = new List<Plane[]>();
    private static int frame = -1, count;
    public static bool IsVisible(Bounds bounds)
    {
        if (!Application.isPlaying) return true;
        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            if (Camera.allCamerasCount > cameras.Length)
                cameras = new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
            int found = Camera.GetAllCameras(cameras);
            count = 0;
            for (int i = 0; i < found; i++)
            {
                if (cameras[i] == null || !cameras[i].isActiveAndEnabled) continue;
                if (planes.Count <= count) planes.Add(new Plane[6]);
                GeometryUtility.CalculateFrustumPlanes(cameras[i], planes[count++]);
            }
#if UNITY_EDITOR
            var view = UnityEditor.SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                if (planes.Count <= count) planes.Add(new Plane[6]);
                GeometryUtility.CalculateFrustumPlanes(view.camera, planes[count++]);
            }
#endif
        }
        if (count == 0) return true;
        bounds.Expand(3f); // 1.5 world units on every side: update before entry.
        for (int i = 0; i < count; i++)
            if (GeometryUtility.TestPlanesAABB(planes[i], bounds)) return true;
        return false;
    }
    public static bool IsVisible(Transform owner, Vector2 localSize)
    {
        Matrix4x4 m = owner.localToWorldMatrix;
        Vector3 x = m.MultiplyVector(Vector3.right * localSize.x * .5f);
        Vector3 y = m.MultiplyVector(Vector3.up * localSize.y * .5f);
        Vector3 extent = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x), Mathf.Abs(x.y)+Mathf.Abs(y.y), Mathf.Abs(x.z)+Mathf.Abs(y.z));
        return IsVisible(new Bounds(owner.position, extent * 2f));
    }
}
