using UnityEngine;

/// <summary>Moves only authored foot/hem regions; leaves the upper silhouette untouched.</summary>
public static class PixelWalkFrameUtility
{
    public static Color32[] BuildStep(Color32[] source, int width, int height, RectInt movingFoot)
    {
        var result = (Color32[])source.Clone();
        int left = Mathf.Clamp(movingFoot.xMin, 0, width);
        int right = Mathf.Clamp(movingFoot.xMax, left, width);
        int bottom = Mathf.Clamp(movingFoot.yMin, 0, height);
        int top = Mathf.Clamp(movingFoot.yMax, bottom, height);
        // Lift one pixel within the foot region. Its top meets the unchanged body.
        for (int y = bottom; y < top; y++)
        for (int x = left; x < right; x++)
            result[y * width + x] = y == bottom ? default(Color32) : source[(y - 1) * width + x];
        return result;
    }
}
