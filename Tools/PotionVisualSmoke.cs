using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Copy to the isolated full-script smoke project. Run requires no authored scene/assets;
// RunGpu additionally requires a real graphics device and saves an actual camera preview.
[InitializeOnLoad]
public static class PotionVisualSmoke
{
    const string Pending = "PotionVisualSmoke.Pending", RequireGpu = "PotionVisualSmoke.RequireGpu";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly int Tint = Shader.PropertyToID("_Color"), Marker = Shader.PropertyToID("_SmokeMarker");
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static Material material;
    static Sprite whiteSprite;
    static PotionVisualSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(RequireGpu, false); Begin(); }
    public static void RunGpu() { SessionState.SetBool(RequireGpu, true); Begin(); }
    static void Begin() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); checks = 0; test = Test(); }
            if (test != null && !test.MoveNext())
            { test = null; Debug.Log("POTION_VISUAL_SMOKE_PASS checks=" + checks); EditorApplication.Exit(0); }
        }
        catch (Exception error) { test = null; Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Near(float actual, float expected, string label, float tolerance = .0001f)
    { Check(Mathf.Abs(actual - expected) <= tolerance, label + ": expected=" + expected + ", actual=" + actual); }
    static void Same(Color actual, Color expected, string label)
    { Near(actual.r, expected.r, label + " R"); Near(actual.g, expected.g, label + " G"); Near(actual.b, expected.b, label + " B"); Near(actual.a, expected.a, label + " A"); }
    static FieldInfo Field(object owner, string name)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Flags | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new Exception("Missing fixture field " + name);
    }
    static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner);
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        { var method = type.GetMethod(name, Flags | BindingFlags.DeclaredOnly); if (method != null) return method.Invoke(owner, args); }
        throw new Exception("Missing fixture method " + name);
    }
    static MaterialPropertyBlock Properties(SpriteRenderer renderer)
    { var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties); return properties; }
    static void SameProperties(SpriteRenderer renderer, MaterialPropertyBlock expected, string label)
    {
        // SpriteRenderer may add internal per-renderer entries even when callers never set an MPB.
        // Compare the owned override and fixture marker, not isEmpty or engine-private entries.
        var actual = Properties(renderer);
        Check(actual.HasProperty(Tint) == expected.HasProperty(Tint), label + " tint override presence");
        if (expected.HasProperty(Tint)) Same(actual.GetColor(Tint), expected.GetColor(Tint), label + " tint override");
        Check(actual.HasProperty(Marker) == expected.HasProperty(Marker), label + " marker presence");
        if (expected.HasProperty(Marker)) Near(actual.GetFloat(Marker), expected.GetFloat(Marker), label + " marker");
    }
    static SpriteRenderer Body(ZeldaCharacterData actor)
    { var visual = actor.transform.Find("Visual"); return visual != null ? visual.GetComponent<SpriteRenderer>() : actor.GetComponent<SpriteRenderer>(); }
    static SwordZeldaCharacterData Actor(string name, Vector2 point, bool child = true)
    {
        var root = new GameObject(name); root.transform.position = point;
        root.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
        var actor = root.AddComponent<SwordZeldaCharacterData>();
        var visual = child ? new GameObject("Visual") : root;
        if (child) visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite; renderer.color = Color.white; renderer.sharedMaterial = material;
        renderer.sortingOrder = 9;
        var state = actor.CaptureRuntimeState(); state.health = 8; state.currentHealth = 8; state.potionBuffs = default;
        actor.ApplyRuntimeState(state); return actor;
    }
    static void ApplyVeil(ZeldaCharacterData actor)
    { Check(CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Veil), "Veil can be applied"); Visual(actor); }
    static void Visual(ZeldaCharacterData actor) => Call(actor.GetComponent<PotionPerceptionEffects>(), "UpdateVeilVisual");
    static void BreakVeil(ZeldaCharacterData actor) { CharacterPotionBuffs.NotifyAction(actor); Visual(actor); }
    static ZeldaAttackHitbox Hit(ZeldaCharacterData actor, ZeldaAttackVisualShape shape, float angle = 0f, bool direct = true, bool explosion = false)
    {
        var root = new GameObject("Visual test " + shape); root.transform.position = actor.transform.position + Vector3.up * .8f;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var hit = root.AddComponent<ZeldaAttackHitbox>();
        hit.Configure(10f, actor.AttackSize, Color.white, 1, actor, shape, explosion, direct);
        return hit;
    }
    static void Sweep(ZeldaAttackHitbox hit, float progress) => Call(hit, "UpdatePotionSweepVisual", progress);
    static IEnumerator Test()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        SceneManager.SetActiveScene(SceneManager.CreateScene("Potion Visual Smoke")); Time.timeScale = 0f;
        material = new Material(Shader.Find("Sprites/Default")); material.color = Color.white;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point;
        var pixels = new Color[16]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels); texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
        VeilChecks(); SweepChecks();
        bool gpu = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        if (SessionState.GetBool(RequireGpu, false)) Check(gpu, "RunGpu requires an actual graphics device; do not pass -nographics");
        if (gpu) { GpuChecks(); Preview(); }
        else Debug.Log("POTION_VISUAL_GPU_SKIPPED: use PotionVisualSmoke.RunGpu without -nographics for pixel assertions and preview");
        Time.timeScale = 1f;
    }
    static void VeilChecks()
    {
        var actor = Actor("Veil ordinary", Vector2.zero); var body = Body(actor);
        var tint = new Color(.72f, .86f, .94f, .8f); body.color = new Color(.7f, .6f, .5f, .6f);
        var original = new MaterialPropertyBlock(); original.SetColor(Tint, tint); original.SetFloat(Marker, 17f); body.SetPropertyBlock(original);
        int layer = body.gameObject.layer, sorting = body.sortingOrder, sortingLayer = body.sortingLayerID;
        Color originalColor = body.color; var shared = body.sharedMaterial;
        var icon = new GameObject("Unrelated skill icon").AddComponent<SpriteRenderer>(); icon.transform.SetParent(actor.transform, false);
        icon.sprite = whiteSprite; icon.sharedMaterial = material;
        var iconBefore = Properties(icon);
        PotionPerceptionEffects.Ensure(actor);
        Same(Properties(body).GetColor(Tint), tint, "Nonbuff body unchanged");
        ApplyVeil(actor); Near(Properties(body).GetColor(Tint).a, .8f * .45f, "Veil multiplies original shader alpha by precisely 45 percent");
        Same(body.color, originalColor, "Veil leaves renderer color/alpha ownership untouched");
        for (int i = 0; i < 25; i++) { CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Veil); Visual(actor); }
        Near(Properties(body).GetColor(Tint).a, .36f, "Repeated refresh cannot compound alpha");
        Near(Properties(body).GetFloat(Marker), 17f, "Unrelated property block fields retained");
        SameProperties(icon, iconBefore, "Veil does not touch other child renderers");
        Check(icon.color == Color.white, "Veil leaves icon renderer color untouched");
        Check(body.gameObject.layer == layer && body.sortingOrder == sorting && body.sortingLayerID == sortingLayer && body.sharedMaterial == shared,
            "Veil does not change layer, sorting or shared material");
        actor.TakeDamage(1); Call(actor, "UpdateDamageFeedback", .02f); Color damaged = body.color;
        Check(damaged != originalColor, "Real damage feedback modifies renderer color");
        Visual(actor); Same(body.color, damaged, "Veil preserves real damage feedback tint");
        body.color = new Color(.1f, 1f, .25f, .7f); Visual(actor);
        Same(body.color, new Color(.1f, 1f, .25f, .7f), "Veil leaves external regeneration/movement tint untouched");
        BreakVeil(actor); Same(Properties(body).GetColor(Tint), tint, "Action restores prior MPB tint");
        Near(Properties(body).GetFloat(Marker), 17f, "Action restores prior custom MPB property");
        ApplyVeil(actor); Get<float[]>(actor.GetComponent<CharacterPotionBuffs>(), "expiresAt")[(int)AlchemyPotionKind.Veil] = Time.time - 1f;
        Visual(actor); Same(Properties(body).GetColor(Tint), tint, "Natural expiry restores opacity");
        ApplyVeil(actor); var effects = actor.GetComponent<PotionPerceptionEffects>(); effects.enabled = false;
        Same(Properties(body).GetColor(Tint), tint, "Component disable immediately restores opacity");
        effects.enabled = true; Visual(actor); Near(Properties(body).GetColor(Tint).a, .36f, "Component re-enable reapplies active Veil once");
        actor.gameObject.SetActive(false); Same(Properties(body).GetColor(Tint), tint, "Body disable restores original properties");
        actor.gameObject.SetActive(true); Visual(actor); Near(Properties(body).GetColor(Tint).a, .36f, "Body resume restores active Veil once");
        // Swapping the body must restore the old renderer and tint only its replacement.
        body.gameObject.name = "Old Visual";
        var replacement = new GameObject("Visual").AddComponent<SpriteRenderer>(); replacement.transform.SetParent(actor.transform, false);
        replacement.sprite = whiteSprite; replacement.sharedMaterial = material;
        var replacementBefore = Properties(replacement);
        Visual(actor); Same(Properties(body).GetColor(Tint), tint, "Renderer swap restores old body");
        Near(Properties(replacement).GetColor(Tint).a, .45f, "Renderer swap applies to new body");
        BreakVeil(actor); SameProperties(replacement, replacementBefore, "New body restores original shader override state");
        var alternate = new Material(material); alternate.color = new Color(.6f, .7f, .8f, .5f);
        ApplyVeil(actor); replacement.sharedMaterial = alternate; Visual(actor);
        Near(Properties(replacement).GetColor(Tint).a, .225f, "Material swap uses replacement material base alpha");
        BreakVeil(actor); SameProperties(replacement, replacementBefore, "Material swap restores original shader overrides");
        Check(replacement.sharedMaterial == alternate, "Material swap remains owned by its caller after Veil");
        var fallback = Actor("Root sprite fallback", new Vector2(10, 0), false); var fallbackBefore = Properties(Body(fallback)); ApplyVeil(fallback);
        Near(Properties(Body(fallback)).GetColor(Tint).a, .45f, "Root SpriteRenderer fallback is supported");
        BreakVeil(fallback); SameProperties(Body(fallback), fallbackBefore, "Fallback root restores original MPB");
        var restored = Actor("Restored Veil", new Vector2(20, 0)); ApplyVeil(restored);
        string json = JsonUtility.ToJson(restored.CaptureRuntimeState()); BreakVeil(restored);
        restored.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(json)); Visual(restored);
        Near(Properties(Body(restored)).GetColor(Tint).a, .45f, "Saved Veil restores visible translucency");
        // Exercise the real zone entry path, including consumed-while-inside semantics.
        var zoned = Actor("Area Veil", new Vector2(30, 0));
        var zonedBefore = Properties(Body(zoned));
        var zone = new GameObject("Veil test area"); zone.transform.position = zoned.transform.position;
        var zoneCollider = zone.AddComponent<BoxCollider2D>(); zoneCollider.isTrigger = true; zoneCollider.size = Vector2.one * 2f;
        var area = zone.AddComponent<TemporaryCharacterEffectArea>(); Set(area, "animateBubbles", false);
        area.ApplyEffectSettings(TemporaryCharacterEffectArea.EffectSettings.ForPotion(AlchemyPotionKind.Veil));
        Physics2D.SyncTransforms(); Call(area, "TickEffects", .02f); Visual(zoned);
        Near(Properties(Body(zoned)).GetColor(Tint).a, .45f, "Area-granted Veil visibly uses same alpha");
        BreakVeil(zoned); Call(area, "TickEffects", .02f); Visual(zoned);
        SameProperties(Body(zoned), zonedBefore, "Area does not re-hide actor after a consuming action"); area.enabled = false;
        var dead = Actor("Veil death", new Vector2(40, 0)); var deadBefore = Properties(Body(dead)); ApplyVeil(dead); dead.TakeDamage(100); Visual(dead);
        Check(dead.IsDead, "Actual lethal damage entered death lifecycle");
        SameProperties(Body(dead), deadBefore, "Actual death restores Veil before deferred destruction");
        var ghost = Actor("Veil ghost-form", new Vector2(50, 0)); var ghostBefore = Properties(Body(ghost)); ApplyVeil(ghost);
        // Isolate form presentation lifecycle from shader availability in a script-only smoke project.
        var form = ghost.gameObject.AddComponent<GhostFormRuntime>(); form.enabled = false;
        Set(form, "<Remaining>k__BackingField", 5f); Set(ghost, "ghostForm", form); Visual(ghost);
        Check(ghost.IsGhostLike, "Ghost-form fixture uses actual IsGhostLike predicate");
        SameProperties(Body(ghost), ghostBefore, "Ghost form does not inherit stale Veil tint");
        Set(ghost, "ghostForm", null); Visual(ghost);
        Near(Properties(Body(ghost)).GetColor(Tint).a, .45f, "Returning from form reapplies still-active Veil");
    }
    static void SweepChecks()
    {
        var owner = Actor("Sweep owner", new Vector2(0, 20)); var body = Body(owner);
        var directions = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        var angles = new[] { 0f, -90f, 180f, 90f };
        foreach (ZeldaAttackVisualShape shape in Enum.GetValues(typeof(ZeldaAttackVisualShape)))
        {
            if (shape == ZeldaAttackVisualShape.Shockwave || shape == ZeldaAttackVisualShape.DirectionalWave) continue;
            for (int direction = 0; direction < directions.Length; direction++)
            {
                owner.GetComponent<CharacterPotionBuffs>()?.ApplyRuntimeState(default);
                owner.ApplyCharacterVisual(body, directions[direction], false, true);
                Sprite characterSprite = body.sprite; Color characterTint = body.color;
                var normal = Hit(owner, shape, angles[direction]); var normalRenderer = normal.GetComponent<SpriteRenderer>();
                Sprite selectedSprite = normalRenderer.sprite;
                Check(Get<SpriteRenderer>(normal, "potionSweepRenderer") == null && !normalRenderer.forceRenderingOff, "Normal " + shape + " has no sweep override");
                Vector3 normalPosition = normal.transform.position; Quaternion normalRotation = normal.transform.rotation;
                Call(normal, "Update");
                Check(normal.transform.position == normalPosition && normal.transform.rotation == normalRotation, "Normal attack remains static: " + shape);
                CharacterPotionBuffs.TryApply(owner, AlchemyPotionKind.Berserk);
                var hit = Hit(owner, shape, angles[direction]); var renderer = Get<SpriteRenderer>(hit, "potionSweepRenderer");
                Check(renderer != null && renderer.gameObject.activeInHierarchy && hit.GetComponent<SpriteRenderer>().forceRenderingOff, "Berserk uses visible-only child: " + shape);
                Check(renderer.sprite == selectedSprite, "Berserk preserves direction-selected weapon sprite: " + shape + "/" + direction);
                var collider = hit.GetComponent<BoxCollider2D>(); Vector2 contactSize = collider.size;
                Vector3 rootPosition = hit.transform.position, rootScale = hit.transform.localScale; Quaternion rootRotation = hit.transform.rotation;
                Vector3 start = Vector3.zero, middle = Vector3.zero, end = Vector3.zero;
                for (int phase = 0; phase < 3; phase++)
                {
                    Sweep(hit, phase * .5f);
                    Near(Mathf.DeltaAngle(rootRotation.eulerAngles.z, renderer.transform.eulerAngles.z), phase * 55f - 55f, "Berserk phase facing/arc angle", .001f);
                    Near(renderer.transform.TransformVector(Vector3.right).magnitude, Mathf.Abs(owner.AttackSize.x), "Sweep preserves world sprite width at every angle", .001f);
                    Near(renderer.transform.TransformVector(Vector3.up).magnitude, Mathf.Abs(owner.AttackSize.y), "Sweep preserves world sprite height at every angle", .001f);
                    Near(Vector3.Dot(renderer.transform.TransformVector(Vector3.right), renderer.transform.TransformVector(Vector3.up)), 0f,
                        "Sweep world axes stay orthogonal rather than shearing under nonuniform scale", .001f);
                    Check(hit.transform.position == rootPosition && hit.transform.rotation == rootRotation && hit.transform.localScale == rootScale && collider.size == contactSize,
                        "Visual arc never moves, rotates, scales or reshapes root damage collider");
                    if (phase == 0) start = renderer.transform.position; else if (phase == 1) middle = renderer.transform.position; else end = renderer.transform.position;
                }
                Check(Vector3.Distance(start, middle) > .1f && Vector3.Distance(middle, end) > .1f && Vector3.Distance(start, end) > .2f,
                    "Three actual weapon positions differ, including round shapes: " + shape);
                Check(body.sprite == characterSprite && body.color == characterTint, "All four character-facing sprites remain untouched");
                Check(hit.GetComponentsInChildren<Collider2D>().Length == 1, "Sweep child never adds another damage collider");
                if (shape == ZeldaAttackVisualShape.TwinBlades && direction == 1)
                {
                    hit.Configure(10f, owner.AttackSize, Color.white, 1, owner, shape, false, false);
                    Check(!renderer.gameObject.activeInHierarchy && !hit.GetComponent<SpriteRenderer>().forceRenderingOff,
                        "Reconfiguring a reused hitbox without sweep restores original renderer");
                }
                normal.gameObject.SetActive(false); hit.gameObject.SetActive(false);
            }
        }
        var shockwave = Hit(owner, ZeldaAttackVisualShape.Shockwave);
        var directional = Hit(owner, ZeldaAttackVisualShape.DirectionalWave);
        Check(Get<SpriteRenderer>(directional, "potionSweepRenderer") == null && directional.GetComponent<DirectionalPurpleWave>() != null,
            "Directional wave retains its travelling effect under Berserk instead of a melee sweep");
        var explosion = Hit(owner, ZeldaAttackVisualShape.Box, 0f, false, true);
        Check(Get<SpriteRenderer>(shockwave, "potionSweepRenderer") == null && Get<SpriteRenderer>(explosion, "potionSweepRenderer") == null,
            "Shockwave and explosion never acquire a Berserk sweep child");
        var victim = Actor("One-hit target", owner.transform.position + Vector3.right);
        var damageHit = Hit(owner, ZeldaAttackVisualShape.Sword); Physics2D.SyncTransforms();
        int health = victim.CurrentHealth;
        foreach (float phase in new[] { 0f, .5f, 1f }) { Sweep(damageHit, phase); Call(damageHit, "TryDamage", victim.GetComponent<Collider2D>()); }
        Check(victim.CurrentHealth == health - 1, "Visual phases do not duplicate damage per swing");
        var ai = owner.gameObject.AddComponent<ZeldaCharacterAiBase>(); owner.GetComponent<ZeldaFourWayMover>().enabled = false;
        var interrupted = Hit(owner, ZeldaAttackVisualShape.Sword); var interruptedRenderer = Get<SpriteRenderer>(interrupted, "potionSweepRenderer");
        ai.StunForDuration(3f); Check(ai.IsStunned, "Stun fixture enters real AI stun state");
        Call(interrupted, "Update");
        Check(!interrupted.GetComponent<BoxCollider2D>().enabled && !interruptedRenderer.enabled, "Stun cancels both damage and sweep visual immediately");
        int after = victim.CurrentHealth; Call(interrupted, "TryDamage", victim.GetComponent<Collider2D>());
        Check(victim.CurrentHealth == after, "Cancelled sweep cannot damage a later contact");
    }
    static Camera CameraAt(Vector2 point, float size, int layer)
    {
        var camera = new GameObject("Potion smoke render camera").AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(point.x, point.y, -10); camera.orthographic = true; camera.orthographicSize = size;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << layer; camera.allowHDR = false; camera.allowMSAA = false; return camera;
    }
    static Texture2D Render(Camera camera, int width, int height, bool linear = true)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
            linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default);
        var previous = RenderTexture.active; camera.targetTexture = target; camera.aspect = (float)width / height;
        camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); return image;
    }
    static Color Sample(Camera camera)
    { var image = Render(camera, 64, 64); var color = image.GetPixel(32, 32); UnityEngine.Object.Destroy(image); return color; }
    static void GpuChecks()
    {
        var actor = Actor("GPU alpha subject", new Vector2(500, 500)); var body = Body(actor); body.gameObject.layer = 30;
        var camera = CameraAt(actor.transform.position, 1f, 30); Color normal = Sample(camera);
        Check(normal.r > .95f && normal.g > .95f && normal.b > .95f, "Actual GPU sample sees opaque white fixture");
        ApplyVeil(actor); Color veil = Sample(camera);
        Near(veil.r / normal.r, .45f, "Actual GPU Veil red contribution is 45 percent", .015f);
        Near(veil.g / normal.g, .45f, "Actual GPU Veil green contribution is 45 percent", .015f);
        for (int i = 0; i < 20; i++) Visual(actor);
        Color repeated = Sample(camera); Near(repeated.r, veil.r, "Actual GPU repeated Veil stays stable", .005f);
        body.color = new Color(1f, .25f, .1f, 1f); Color tintedVeil = Sample(camera);
        BreakVeil(actor); Color tinted = Sample(camera);
        Near(tintedVeil.r / tinted.r, .45f, "GPU damage-like red tint remains multiplied by Veil alpha", .015f);
        Near(tintedVeil.g / tinted.g, .45f, "GPU damage-like green tint remains multiplied by Veil alpha", .04f);
        body.color = Color.white; Color restored = Sample(camera); Near(restored.r, normal.r, "GPU action restores opacity", .005f);
        // SpriteRenderer contributes _MainTex/_RendererColor to its property block on some backends.
        // Restoring our _Color must not roll back a facing sprite or feedback tint changed meanwhile.
        ApplyVeil(actor);
        var changedTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false); changedTexture.filterMode = FilterMode.Point;
        var changedPixels = new Color[16]; for (int i = 0; i < changedPixels.Length; i++) changedPixels[i] = new Color(.2f, .75f, 1f, 1f);
        changedTexture.SetPixels(changedPixels); changedTexture.Apply();
        var changedSprite = Sprite.Create(changedTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
        body.sprite = changedSprite; body.color = new Color(.8f, .9f, .6f, .7f); Visual(actor);
        Color changedVeil = Sample(camera);
        BreakVeil(actor); Color changedRestored = Sample(camera);
        var reference = new GameObject("GPU current sprite reference").AddComponent<SpriteRenderer>();
        reference.gameObject.layer = 30; reference.transform.position = actor.transform.position;
        reference.sprite = changedSprite; reference.sharedMaterial = material; reference.color = body.color;
        body.enabled = false; Color changedReference = Sample(camera); body.enabled = true; reference.enabled = false;
        Near(changedRestored.r, changedReference.r, "GPU restoration retains current sprite and current tint red", .01f);
        Near(changedRestored.g, changedReference.g, "GPU restoration retains current sprite and current tint green", .01f);
        Near(changedRestored.b, changedReference.b, "GPU restoration retains current sprite and current tint blue", .01f);
        Near(changedVeil.b / changedReference.b, .45f, "GPU Veil uses new sprite and tint while already active", .025f);
        Debug.Log("POTION_VISUAL_GPU_PASS normal=" + normal + " veil=" + veil + " ratio=" + (veil.r / normal.r));
        actor.gameObject.SetActive(false); camera.gameObject.SetActive(false);
    }
    static void SetLayer(GameObject root, int layer)
    { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer; }
    static void Label(string text, Vector2 point, float scale, Color color)
    {
        var root = new GameObject(text); root.layer = 31; root.transform.position = point;
        var label = root.AddComponent<TextMesh>(); label.text = text; label.fontSize = 32; label.characterSize = scale;
        label.anchor = TextAnchor.MiddleCenter; label.color = color; root.GetComponent<MeshRenderer>().sortingOrder = 100;
    }
    static void Preview()
    {
        Vector2 center = new Vector2(1000, 1000);
        string[] headings = { "NORMAL", "VEIL 45%", "NORMAL HIT", "BERSERK 0%", "BERSERK 50%", "BERSERK 100%" };
        Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
        float[] angles = { 0f, -90f, 180f, 90f };
        var camera = CameraAt(center, 6.1f, 31); camera.backgroundColor = new Color(.055f, .075f, .10f);
        for (int column = 0; column < headings.Length; column++)
        {
            float x = center.x + (column - 2.5f) * 1.8f;
            Label(headings[column], new Vector2(x, center.y + 5.3f), .045f, Color.white);
            for (int row = 0; row < directions.Length; row++)
            {
                Vector2 position = new Vector2(x, center.y + 3.2f - row * 2.3f);
                var actor = Actor("Preview " + headings[column] + " " + row, position);
                actor.ApplyCharacterVisual(Body(actor), directions[row], false, column >= 2);
                Body(actor).sortingOrder = 1;
                if (column == 1) ApplyVeil(actor);
                if (column >= 3) CharacterPotionBuffs.TryApply(actor, AlchemyPotionKind.Berserk);
                if (column >= 2)
                {
                    var hit = Hit(actor, ZeldaAttackVisualShape.Sword, angles[row]);
                    hit.transform.position = (Vector3)position + (Vector3)directions[row] * .58f;
                    if (column >= 3) Sweep(hit, (column - 3) * .5f);
                    hit.GetComponent<Collider2D>().enabled = false; SetLayer(hit.gameObject, 31);
                }
                SetLayer(actor.gameObject, 31);
            }
        }
        var image = Render(camera, 1320, 1200, false);
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string output = Path.Combine(directory, "PotionVisualSmoke-preview.png"); File.WriteAllBytes(output, image.EncodeToPNG());
        Debug.Log("POTION_VISUAL_PREVIEW " + output);
        UnityEngine.Object.Destroy(image); camera.gameObject.SetActive(false);
    }
}
