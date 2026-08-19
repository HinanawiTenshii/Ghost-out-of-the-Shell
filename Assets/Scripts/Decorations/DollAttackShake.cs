using UnityEngine;

/// <summary>
/// Applies the same short horizontal hit shake used by characters to every
/// visual piece of a decorative doll, without moving its collider.
/// </summary>
public sealed class DollAttackShake : MonoBehaviour
{
    [SerializeField, Min(0f)] private float shakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float shakeAmount = 0.04f;
    [SerializeField, Min(0f)] private float shakeSpeed = 80f;

    private Transform[] visualTransforms;
    private Vector3[] visualBasePositions;
    private float shakeTimer;

    private void Awake()
    {
        CacheVisualPieces();
    }

    private void Update()
    {
        if (shakeTimer <= 0f)
        {
            return;
        }

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
        float horizontalOffset = shakeTimer > 0f
            ? Mathf.Sin(Time.time * shakeSpeed) * shakeAmount
            : 0f;
        ApplyVisualOffset(horizontalOffset);
    }

    public void TriggerAttackShake()
    {
        if (visualTransforms == null || visualTransforms.Length == 0)
        {
            CacheVisualPieces();
        }

        shakeTimer = shakeDuration;
        ApplyVisualOffset(0f);
    }

    private void CacheVisualPieces()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        visualTransforms = new Transform[renderers.Length];
        visualBasePositions = new Vector3[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            visualTransforms[i] = renderers[i].transform;
            visualBasePositions[i] = renderers[i].transform.localPosition;
        }
    }

    private void ApplyVisualOffset(float horizontalOffset)
    {
        if (visualTransforms == null)
        {
            return;
        }

        Vector3 offset = new Vector3(horizontalOffset, 0f, 0f);
        for (int i = 0; i < visualTransforms.Length; i++)
        {
            if (visualTransforms[i] != null)
            {
                visualTransforms[i].localPosition = visualBasePositions[i] + offset;
            }
        }
    }

    private void OnDisable()
    {
        shakeTimer = 0f;
        ApplyVisualOffset(0f);
    }

    private void OnValidate()
    {
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeAmount = Mathf.Max(0f, shakeAmount);
        shakeSpeed = Mathf.Max(0f, shakeSpeed);
    }
}
