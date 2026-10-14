using UnityEngine;

/// <summary>Visual-only levitation, paired eye halos and a bounded smoke emitter.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public sealed class OuterRealmVisitorVisual : MonoBehaviour
{
    [SerializeField, Min(0)] private float floatingHeight = .24f;
    [SerializeField, Min(0)] private float floatingAmplitude = .055f;
    [SerializeField, Min(.2f)] private float floatingPeriod = 2.4f;
    [SerializeField, Range(1, 30)] private float smokePerSecond = 16;
    private OuterRealmVisitorZeldaCharacterData data;
    private SpriteRenderer body, leftEye, rightEye, shadow;
    private Vector3 basePosition;
    private int direction;
    private ParticleSystem smoke;
    private ParticleSystemRenderer smokeRenderer;
    private Material glowMaterial, smokeMaterial;
    private Texture2D puffTexture;
    private Sprite glowSprite;
    private Sprite shadowSprite;

    public void Bind(SpriteRenderer renderer, int facing)
    {
        if (body != renderer)
        {
            body = renderer;
            basePosition = renderer.transform == transform ? Vector3.zero : renderer.transform.localPosition;
        }
        direction = facing;
        if (data == null) data = GetComponent<OuterRealmVisitorZeldaCharacterData>();
        if (smoke == null) BuildEffects();
    }
    private void BuildEffects()
    {
        var shader = Resources.Load<Shader>("ElectricGateGlow");
        glowMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        glowMaterial.SetFloat("_Radial", 1); glowMaterial.SetFloat("_Intensity", 1.1f); glowMaterial.SetFloat("_CoreFraction", .18f);
        glowSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
        glowSprite.hideFlags = HideFlags.HideAndDontSave;
        leftEye = Eye("Left Violet Eye Halo"); rightEye = Eye("Right Violet Eye Halo");
        var child = new GameObject("Outer Realm Black Smoke") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        child.SetActive(false); child.transform.SetParent(body.transform, false); child.transform.localPosition = new Vector3(0, .36f, 0);
        smoke = child.AddComponent<ParticleSystem>(); smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = smoke.main;
        main.playOnAwake = false; main.loop = true; main.maxParticles = 48; main.duration = 2;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(.16f, .27f);
        main.startColor = new Color(.008f, .005f, .014f, .8f); main.startSpeed = 0;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.Pause; main.useUnscaledTime = false;
        var emission = smoke.emission; emission.rateOverTime = smokePerSecond;
        var shape = smoke.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(.5f, .65f, .01f);
        var velocity = smoke.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.48f, .48f);
        velocity.y = new ParticleSystem.MinMaxCurve(.24f, .6f); velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
        var size = smoke.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .55f, 1, 2));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .18f), new GradientAlphaKey(.75f, .55f), new GradientAlphaKey(0, 1) });
        var color = smoke.colorOverLifetime; color.enabled = true; color.color = gradient;
        puffTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[256];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) {
            float radius = new Vector2((x + .5f) / 8 - 1, (y + .5f) / 8 - 1).magnitude;
            pixels[y * 16 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), .7f));
        }
        puffTexture.SetPixels(pixels); puffTexture.Apply();
        smokeMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave, mainTexture = puffTexture };
        smokeRenderer = child.GetComponent<ParticleSystemRenderer>(); smokeRenderer.sharedMaterial = smokeMaterial;
        smokeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smokeRenderer.receiveShadows = false; smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        shadowSprite = Sprite.Create(puffTexture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
        shadowSprite.hideFlags = HideFlags.HideAndDontSave;
        var shadowRoot = new GameObject("Floating Ground Shadow") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        shadowRoot.transform.SetParent(transform, false);
        shadow = shadowRoot.AddComponent<SpriteRenderer>(); shadow.sprite = shadowSprite;
        shadowRoot.transform.localScale = new Vector3(.65f, .2f, 1);
        child.SetActive(true);
    }
    private SpriteRenderer Eye(string name)
    {
        var root = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
        root.transform.SetParent(body.transform, false);
        var eye = root.AddComponent<SpriteRenderer>(); eye.sprite = glowSprite; eye.sharedMaterial = glowMaterial; return eye;
    }
    private void LateUpdate()
    {
        if (body == null || data == null) return;
        bool visible = body.enabled && !data.IsDead && !data.IsGhostForm;
        float phase = Time.time * Mathf.PI * 2 / Mathf.Max(.2f, floatingPeriod);
        // Never shift the character's root/physics position, even during possession.
        if (body.transform != transform)
            body.transform.localPosition = basePosition + Vector3.up * (floatingHeight + Mathf.Sin(phase) * floatingAmplitude) + data.GetDamageShakeOffset();
        if (smoke == null) return;
        smokeRenderer.enabled = visible;
        smokeRenderer.sortingLayerID = body.sortingLayerID; smokeRenderer.sortingOrder = body.sortingOrder - 1;
        smokeRenderer.maskInteraction = body.maskInteraction; smoke.gameObject.layer = body.gameObject.layer;
        if (visible) { if (smoke.isStopped) smoke.Play(); }
        else if (!smoke.isStopped) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetEye(leftEye, direction == 2 ? -.15625f : direction == 3 ? .15625f : -.09375f, visible && direction != 1, phase);
        SetEye(rightEye, .09375f, visible && direction == 0, phase);
        shadow.enabled = visible; shadow.gameObject.layer = body.gameObject.layer;
        shadow.sortingLayerID = body.sortingLayerID; shadow.sortingOrder = body.sortingOrder - 2; shadow.maskInteraction = body.maskInteraction;
        shadow.color = new Color(0, 0, 0, (.42f - .04f * Mathf.Sin(phase)) * body.color.a);
    }
    private void SetEye(SpriteRenderer eye, float x, bool visible, float phase)
    {
        eye.enabled = visible; eye.gameObject.layer = body.gameObject.layer;
        eye.sortingLayerID = body.sortingLayerID; eye.sortingOrder = body.sortingOrder + 1; eye.maskInteraction = body.maskInteraction;
        eye.transform.localPosition = new Vector3(x, .71875f, 0);
        eye.transform.localScale = Vector3.one * (.18f + .015f * Mathf.Sin(phase));
        Color color = data.EyeColor; color.a = (.4f + .07f * Mathf.Sin(phase)) * body.color.a; eye.color = color;
    }
    private void OnDisable()
    {
        if (data != null) data.EndCastVisual();
        if (body != null && body.transform != transform) body.transform.localPosition = basePosition;
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (leftEye != null) leftEye.enabled = false; if (rightEye != null) rightEye.enabled = false;
        if (shadow != null) shadow.enabled = false;
    }
    private void OnDestroy()
    {
        if (leftEye != null) CrystalMesh.Release(leftEye.gameObject);
        if (rightEye != null) CrystalMesh.Release(rightEye.gameObject);
        if (smoke != null) CrystalMesh.Release(smoke.gameObject);
        if (shadow != null) CrystalMesh.Release(shadow.gameObject);
        CrystalMesh.Release(shadowSprite); CrystalMesh.Release(glowSprite); CrystalMesh.Release(glowMaterial); CrystalMesh.Release(smokeMaterial); CrystalMesh.Release(puffTexture);
    }
}
