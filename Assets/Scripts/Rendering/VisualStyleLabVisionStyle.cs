using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local visual override. Reuses the exact AI visibility mesh and stencil union.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1200)]
public sealed class VisualStyleLabVisionStyle : MonoBehaviour
{
    [SerializeField] private Shader displayShader;
    [SerializeField] private Color calm = new Color(.30f, .62f, .80f, .065f);
    [SerializeField] private Color warning = new Color(1f, .65f, .20f, .13f);
    [SerializeField] private Color danger = new Color(1f, .23f, .16f, .19f);
    [SerializeField, Range(.5f, 3f)] private float edgePixels = 1.1f;
    private ZeldaCharacterAiBase ai;
    private MeshFilter filter;
    private MeshRenderer visual;
    private Mesh mesh;
    private Material material;
    private Shader originalShader;
    private int originalQueue, vertexCount;
    private MaterialPropertyBlock properties, originalProperties;
    private readonly List<Vector2> coordinates = new List<Vector2>();
    private readonly List<Vector2> originalCoordinates = new List<Vector2>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private float retryAt;
    public Color DisplayColor { get; private set; }
    public void Configure(Shader shader) { displayShader = shader; }

    public static Color ColorFor(ZeldaAiState state, Color calm, Color warning, Color danger)
    {
        if (state == ZeldaAiState.Hostile) return danger;
        if (state == ZeldaAiState.Alert || state == ZeldaAiState.Suspicious) return warning;
        if (state == ZeldaAiState.Search) { warning.a *= .7f; return warning; }
        return calm;
    }
    private void OnEnable() { ai = GetComponent<ZeldaCharacterAiBase>(); retryAt = 0; }
    private bool Prepare()
    {
        if (visual != null && material != null && filter != null) return true;
        if (Time.unscaledTime < retryAt || displayShader == null) return false;
        retryAt = Time.unscaledTime + .2f;
        var child = transform.Find("AI Vision Cone");
        if (child == null) return false;
        visual = child.GetComponent<MeshRenderer>(); filter = child.GetComponent<MeshFilter>();
        if (visual == null || filter == null || visual.sharedMaterial == null) return false;
        material = visual.sharedMaterial; originalShader = material.shader; originalQueue = material.renderQueue;
        properties = new MaterialPropertyBlock(); originalProperties = new MaterialPropertyBlock();
        visual.GetPropertyBlock(originalProperties); visual.GetPropertyBlock(properties);
        material.shader = displayShader;
        return true;
    }
    private void LateUpdate()
    {
        if (ai == null || !Prepare() || !visual.enabled) return;
        if (!SceneVisualWorkBudget.IsVisible(visual.bounds)) return;
        var current = filter.sharedMesh;
        if (current == null || current.vertexCount < 3) return;
        if (mesh != current || vertexCount != current.vertexCount)
        {
            mesh = current; vertexCount = mesh.vertexCount;
            originalCoordinates.Clear(); mesh.GetUVs(0, originalCoordinates);
            coordinates.Clear(); coordinates.Add(Vector2.zero);
            for (int i = 1; i < vertexCount; i++)
                coordinates.Add(new Vector2(1f, i == 1 || i == vertexCount - 1 ? 0f : 1f));
            mesh.SetUVs(0, coordinates);
        }
        mesh.GetVertices(vertices);
        // A full circle has no radial side edges; occlusion already comes from the AI.
        bool closed = (vertices[1] - vertices[vertices.Count - 1]).sqrMagnitude < .0001f;
        DisplayColor = ColorFor(ai.CurrentState, calm, warning, danger);
        var edge = DisplayColor; edge.a = ai.CurrentState == ZeldaAiState.Hostile ? .55f : ai.CurrentState == ZeldaAiState.Alert ? .45f : .27f;
        properties.SetColor("_Color", DisplayColor); properties.SetColor("_EdgeColor", edge);
        properties.SetFloat("_EdgePixels", edgePixels); properties.SetFloat("_Closed", closed ? 1f : 0f);
        visual.SetPropertyBlock(properties);
        // Same stencil bit as production: first cone wins, danger gets priority.
        material.renderQueue = ai.CurrentState == ZeldaAiState.Hostile ? 2997 : ai.CurrentState == ZeldaAiState.Alert || ai.CurrentState == ZeldaAiState.Suspicious ? 2998 : 3000;
    }
    private void OnDisable()
    {
        if (material != null) { material.shader = originalShader; material.renderQueue = originalQueue; }
        if (visual != null) visual.SetPropertyBlock(originalProperties);
        if (mesh != null) mesh.SetUVs(0, originalCoordinates);
        visual = null; filter = null; material = null; mesh = null; vertexCount = 0;
    }
}
