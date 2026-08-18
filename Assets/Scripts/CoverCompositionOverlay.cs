using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds fixed world-space title typography for the cover composition scene.
/// The generated geometry stays at its authored scene position and never
/// follows or attaches itself to a camera.
/// </summary>
[ExecuteAlways]
public sealed class CoverCompositionOverlay : MonoBehaviour
{
    private const string GeneratedRootName = "Generated Cover Typography";

    [SerializeField] private Font titleFont;
    [SerializeField] private Color titleColor = new Color32(40, 130, 210, 255);
    [SerializeField, Range(0.01f, 0.12f)] private float lineThickness = 0.045f;
    [SerializeField] private Vector2 localOffset = Vector2.zero;

    private Transform generatedRoot;
    private readonly List<Material> generatedMaterials = new List<Material>();

    private void OnEnable()
    {
        RebuildComposition();
    }

    private void LateUpdate()
    {
        if (generatedRoot == null)
        {
            RebuildComposition();
        }
    }

    private void OnDisable()
    {
        DestroyGeneratedComposition();
    }

    private void RebuildComposition()
    {
        DestroyGeneratedComposition();
        if (titleFont == null)
        {
            return;
        }

        if (titleFont.material != null && titleFont.material.mainTexture != null)
        {
            titleFont.material.mainTexture.filterMode = FilterMode.Point;
        }

        GameObject rootObject = new GameObject(GeneratedRootName);
        rootObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        generatedRoot = rootObject.transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        CreateWorldText("GHOST", new Vector2(0f, 1.6f), 64, 0.2f, 0);
        CreateWorldText("out", new Vector2(-0.72f, 0.3f), 64, 0.085f, 0);
        CreateWorldText("of", new Vector2(0f, 0.3f), 64, 0.085f, 0);
        CreateWorldText("the", new Vector2(0.72f, 0.3f), 64, 0.085f, 0);
        CreateWorldText("SHELL", new Vector2(0f, -1.2f), 64, 0.2f, 0);

        CreatePolyline(
            "Lower Line Left",
            new[]
            {
                new Vector3(-3.45f, -2.65f, 0f),
                new Vector3(-0.7f, -2.65f, 0f)
            },
            -1);
        CreatePolyline(
            "Lower Line Right",
            new[]
            {
                new Vector3(0.7f, -2.65f, 0f),
                new Vector3(3.45f, -2.65f, 0f)
            },
            -1);

        CreateWorldText("感谢游玩", new Vector2(0f, -3.85f), 64, 0.095f, 0);
    }

    private void CreateWorldText(
        string content,
        Vector2 localPosition,
        int fontSize,
        float characterSize,
        int sortingOrder)
    {
        GameObject textObject = new GameObject(content, typeof(TextMesh));
        textObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        textObject.transform.SetParent(generatedRoot, false);
        textObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        textObject.transform.localRotation = Quaternion.identity;

        TextMesh text = textObject.GetComponent<TextMesh>();
        text.text = content;
        text.font = titleFont;
        text.fontSize = fontSize;
        text.characterSize = characterSize;
        text.fontStyle = FontStyle.Normal;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = titleColor;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = titleFont.material;
    }

    private void CreatePolyline(
        string objectName,
        Vector3[] positions,
        int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName, typeof(LineRenderer));
        lineObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        lineObject.transform.SetParent(generatedRoot, false);
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localRotation = Quaternion.identity;

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.startWidth = lineThickness;
        line.endWidth = lineThickness;
        line.startColor = titleColor;
        line.endColor = titleColor;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingLayerID = 0;
        line.sortingOrder = sortingOrder;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material material = new Material(shader)
            {
                name = objectName + " Cover Line Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            line.sharedMaterial = material;
            generatedMaterials.Add(material);
        }
    }

    private void DestroyGeneratedComposition()
    {
        Transform existing = transform.Find(GeneratedRootName);
        if (existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing.gameObject);
            }
            else
            {
                DestroyImmediate(existing.gameObject);
            }
        }

        for (int index = 0; index < generatedMaterials.Count; index++)
        {
            Material material = generatedMaterials[index];
            if (material == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        generatedMaterials.Clear();
        generatedRoot = null;
    }

    private void OnValidate()
    {
        lineThickness = Mathf.Clamp(lineThickness, 0.01f, 0.12f);
    }
}
