using System.Collections.Generic;
using UnityEngine;

/// <summary>Opt-in lab decoration. Only leaf art moves; trunks and physics never do.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1100)]
public sealed class VisualStyleLabPlantMotion : MonoBehaviour
{
    [SerializeField] private bool meshCanopy;
    [SerializeField, Range(0f, .15f)] private float amplitude = .065f;
    [SerializeField, Min(3f)] private float gustInterval = 8f;
    [SerializeField, Min(.5f)] private float gustDuration = 2.4f;
    [SerializeField] private float phase;
    private MeshFilter canopy;
    private Mesh original, animated;
    private Vector3[] rest, points, spritePositions;
    private float[] weights;
    private SpriteRenderer[] leaves;
    private float nextTick;

    public float CurrentOffset { get; private set; }
    public static float Gust(float time, float interval, float duration)
    {
        float t = Mathf.Repeat(time, Mathf.Max(3f, interval));
        if (t >= duration) return 0f;
        float p = t / Mathf.Max(.5f, duration);
        return Mathf.Sin(p * Mathf.PI) * Mathf.Sin(p * Mathf.PI * 2f);
    }
    private void OnEnable() { nextTick = 0; }
    private bool Prepare()
    {
        if (meshCanopy)
        {
            if (animated != null && canopy != null && canopy.sharedMesh == animated) return true;
            Release();
            var visual = transform.Find("Tree Geometry (Generated)");
            if (visual == null) return false;
            canopy = visual.GetComponent<MeshFilter>();
            if (canopy == null || canopy.sharedMesh == null) return false;
            original = canopy.sharedMesh;
            animated = Instantiate(original); animated.name = "Lab5 Moving leaves";
            animated.hideFlags = HideFlags.HideAndDontSave; animated.MarkDynamic();
            rest = original.vertices; points = (Vector3[])rest.Clone();
            var colors = original.colors; weights = new float[rest.Length];
            for (int i = 0; i < rest.Length; i++)
                // Green leaf vertices only: trunk and branch vertices stay fixed.
                weights[i] = colors[i].g > colors[i].r * 1.1f && colors[i].g > colors[i].b * 1.1f ? 1f : 0f;
            var bounds = original.bounds; bounds.Expand(.3f); animated.bounds = bounds;
            canopy.sharedMesh = animated;
        }
        else if (leaves == null)
        {
            var candidates = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));
            // The bush also has a root sprite; moving it would move its collider.
            candidates.RemoveAll(s => s.transform == transform || s.GetComponent<Collider2D>() != null);
            leaves = candidates.ToArray();
            spritePositions = new Vector3[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) spritePositions[i] = leaves[i].transform.localPosition;
        }
        return true;
    }
    private void LateUpdate()
    {
        if (Time.time < nextTick || Time.timeScale <= 0f) return;
        nextTick = Time.time + .125f; // Eight art poses/sec; no gameplay throttling.
        if (!SceneVisualWorkBudget.IsVisible(transform, new Vector2(5, 6)) || !Prepare()) return;
        CurrentOffset = amplitude * Gust(Mathf.Floor(Time.time * 8f) / 8f + phase, gustInterval, gustDuration);
        if (meshCanopy)
        {
            for (int i = 0; i < rest.Length; i++) points[i] = rest[i] + Vector3.right * (CurrentOffset * weights[i]);
            animated.vertices = points;
        }
        else
            for (int i = 0; i < leaves.Length; i++)
                if (leaves[i] != null) leaves[i].transform.localPosition = spritePositions[i] + Vector3.right * (CurrentOffset * (.6f + (i % 3) * .2f));
    }
    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }
    private void Release()
    {
        if (canopy != null && canopy.sharedMesh == animated) canopy.sharedMesh = original;
        if (animated != null) Destroy(animated);
        animated = null; canopy = null; original = null;
        if (leaves != null)
            for (int i = 0; i < leaves.Length; i++) if (leaves[i] != null) leaves[i].transform.localPosition = spritePositions[i];
        leaves = null; CurrentOffset = 0f;
    }
}
