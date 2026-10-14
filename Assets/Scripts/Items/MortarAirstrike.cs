using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A fixed, scene-local landing point: neither the player nor scene travel can retarget a called shell.</summary>
public sealed class MortarAirstrike : MonoBehaviour
{
    public const int AttackPower = 7;
    public const float Radius = 5f, FallDuration = .55f;
    private ZeldaCharacterData caller;
    private float callDelay, age, fallHeight, investigationRadius = 7f;
    private bool falling, impacted, pausedAudio;
    private SpriteRenderer shell;
    private LineRenderer warning;
    private Material warningMaterial;
    private AudioSource voice;
    private CameraVisionStreamingRegion retention;
    private readonly List<SandwormZeldaCharacterData> worms = new List<SandwormZeldaCharacterData>(2);
    private readonly List<ZeldaCharacterAiBase> listeners = new List<ZeldaCharacterAiBase>();
    private static Sprite shellSprite;
    private static AudioClip descentClip, blastClip;
    public Vector2 LandingPoint => transform.position;
    public bool HasImpacted => impacted;
    public static bool TryCall(ZeldaCharacterData user, float delay)
    {
        if (user == null || user.IsDead || user.IsGhostLike) return false;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded || scene.name != MortarRemotePickupItem.AllowedScene) return false;
        var go = new GameObject("Remote Mortar Strike"); SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = user.transform.position;
        var strike = go.AddComponent<MortarAirstrike>();
        strike.caller = user; strike.callDelay = Mathf.Max(.1f, delay);
        strike.Initialize(); return true;
    }
    private void Initialize()
    {
        var bomb = Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem")?.GetComponent<BombPickupItem>();
        if (bomb != null) investigationRadius = bomb.InvestigationRadius;
        retention = gameObject.AddComponent<CameraVisionStreamingRegion>();
        retention.Configure(Mathf.Max(Radius, investigationRadius) + .5f);
        // Retain the whole falling visual even above the gameplay camera.
        gameObject.AddComponent<CameraVisionStreamingExempt>();
        var cam = Camera.main;
        fallHeight = cam != null && cam.orthographic ? Mathf.Max(16f, cam.orthographicSize * 2.6f) : 18f;
        var marker = new GameObject("Mortar Landing Warning"); marker.transform.SetParent(transform, false);
        warning = marker.AddComponent<LineRenderer>(); warning.useWorldSpace = false;
        warning.loop = true; warning.positionCount = 48; warning.widthMultiplier = .06f;
        warningMaterial = new Material(Shader.Find("Sprites/Default")); warning.sharedMaterial = warningMaterial;
        warning.sortingOrder = -5;
        for (int i = 0; i < 48; i++)
        { float angle = i * Mathf.PI * 2f / 48; warning.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Radius); }
        var projectile = new GameObject("Falling Mortar Shell", typeof(SpriteRenderer)); projectile.transform.SetParent(transform, false);
        shell = projectile.GetComponent<SpriteRenderer>(); shell.sprite = GetShellSprite(); shell.sortingOrder = 12;
        shell.transform.localScale = new Vector3(.68f, 1.15f, 1); shell.enabled = false;
        voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false;
        voice.spatialBlend = .85f; voice.minDistance = 2f; voice.maxDistance = 28f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic; voice.dopplerLevel = 0;
        if (descentClip == null) descentClip = Resources.Load<AudioClip>("Audio/MeteorDescent");
        if (blastClip == null) blastClip = Resources.Load<AudioClip>("Audio/BombExplosion");
        Advance(0f);
    }
    private void Update()
    {
        if (GameSaveSystem.IsLoading) { Destroy(gameObject); return; }
        bool pause = Time.timeScale <= 0f || AudioListener.pause;
        if (pause != pausedAudio && voice != null)
        { if (pause) voice.Pause(); else voice.UnPause(); pausedAudio = pause; }
        if (!pause) Advance(Time.deltaTime);
    }
    private void Advance(float delta)
    {
        age += Mathf.Max(0, delta);
        if (impacted) { if (age >= callDelay + FallDuration + 2f) Destroy(gameObject); return; }
        float fall = Mathf.Clamp01((age - callDelay) / FallDuration);
        Color red = new Color(1f,.25f,.18f, .32f + .28f * (.5f + .5f * Mathf.Sin(age * (fall > 0 ? 28f : 10f))));
        warning.startColor = warning.endColor = red;
        if (age >= callDelay)
        {
            if (!falling) { falling = true; shell.enabled = true; Play(descentClip, .38f); }
            shell.transform.localPosition = Vector3.up * (fallHeight * (1f - fall * fall));
            if (fall >= 1f) Impact();
        }
    }
    private void Impact()
    {
        if (impacted) return;
        impacted = true; shell.enabled = false; warning.enabled = false;
        var scene = gameObject.scene; Vector2 point = LandingPoint;
        retention.Configure(Mathf.Max(Radius, investigationRadius) + .5f);
        // Take the buried targets before damage changes registries or kills the caller.
        foreach (var worm in SandwormZeldaCharacterData.ActiveSandworms)
            if (worm != null && !worm.IsDead && worm.isActiveAndEnabled && ZeldaRuntimeRegistry.GetGameplayScene(worm.gameObject) == scene)
                worms.Add(worm);
        // Eject before the pulse: recovery keeps colliders disabled until the rising animation,
        // and the instantaneous pulse cannot linger and damage the same worm on emergence.
        foreach (var worm in worms)
            if (worm != null) worm.GetComponent<SandwormEruptionAttack>()?.TryApplyBuriedImpact(point, Radius, AttackPower, caller, true);
        ZeldaAttackHitbox.ApplyExplosionPulse(point, Radius, AttackPower, caller, scene);
        var effect = new GameObject("Mortar Explosion"); SceneManager.MoveGameObjectToScene(effect, scene); effect.transform.position = point;
        effect.AddComponent<CameraVisionStreamingExempt>();
        effect.AddComponent<BombExplosionVisual>().Configure(Vector2.one * (Radius * 2f), .6f, new Color(.88f,.89f,.84f), 0, 12, 16);
        Play(blastClip, .85f);
        Camera.main?.GetComponent<CameraFollowActiveZeldaMover>()?.PlayShake(.22f, .16f, 27f);
        foreach (var ai in ZeldaRuntimeRegistry.AiCharacters)
            if (ai != null && ai.isActiveAndEnabled && ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) == scene &&
                ((Vector2)ai.transform.position - point).sqrMagnitude <= investigationRadius * investigationRadius) listeners.Add(ai);
        foreach (var ai in listeners) if (ai != null) ai.InvestigatePosition(point);
    }
    private void Play(AudioClip clip, float volume)
    { if (voice == null || clip == null) return; voice.Stop(); voice.clip = clip; voice.volume = volume; voice.Play(); }
    private void OnDestroy() { if (warningMaterial != null) Destroy(warningMaterial); }
    private static Sprite GetShellSprite()
    {
        if (shellSprite != null) return shellSprite;
        string[] rows = { "..D....D..", "..DSSSSD..", "...SSSS...", "...SHSS...", "..DSHHSD..", "..DSHHSD..",
            "..DSHHSD..", "..DSHHSD..", "..DRRRRD..", "..DRRRRD..", "..DSSSSD..", "...DSSD...", "....DD...." };
        var texture = new Texture2D(10, rows.Length, TextureFormat.RGBA32, false) { name = "Mortar Shell Pixels", filterMode = FilterMode.Point };
        for (int y = 0; y < rows.Length; y++) for (int x = 0; x < 10; x++)
        {
            char p = rows[y][x]; Color c = p == 'D' ? new Color32(44,56,64,255) : p == 'S' ? new Color32(132,148,153,255) :
                p == 'H' ? new Color32(218,225,218,255) : p == 'R' ? new Color32(203,62,49,255) : new Color32(0,0,0,0);
            texture.SetPixel(x, rows.Length - 1 - y, c);
        }
        texture.Apply(false, true); shellSprite = Sprite.Create(texture, new Rect(0,0,10,rows.Length), Vector2.one * .5f, 13f);
        return shellSprite;
    }
}
