using System;
using UnityEngine;

/// <summary>Immobile, possessable sentry with independently rotating overhead gun.</summary>
[ExecuteAlways]
public sealed class TurretZeldaCharacterData : ZeldaCharacterData
{
    [Header("Turret / 炮台")]
    [SerializeField, Min(1f)] private float maximumTurnSpeed = 120f;
    [SerializeField, Min(.05f)] private float playerFireCooldown = .25f;
    [SerializeField, Min(1f)] private float bulletSpeed = 14f;
    [SerializeField, Min(.2f)] private float suicideHoldDuration = 1.5f;
    [SerializeField] private float headAngle;
    [SerializeField] private AudioClip firingSound;
    [SerializeField, Range(0f, 1f)] private float firingVolume = .65f;
    private static Sprite baseSprite, headSprite;
    private SpriteRenderer headRenderer;
    private SpriteRenderer baseRenderer;
    private ZeldaFourWayMover mover;
    private ZeldaPossessionProgressBar exitProgress;
    private float shotCooldown, recoil, exitHold;
    private bool hadControl, awaitReleaseF;
    private bool priorCursor;
    private CursorLockMode priorLock;
    public override int AttackPower => DisposableHandgunBullet.AttackPower;
    public override bool CanAttack => !IsGhostForm;
    protected override bool UsesSingleRendererDamageFeedback => false;
    private Vector2 LocalAim => Quaternion.Euler(0, 0, headAngle) * Vector2.up;
    public Vector2 AimDirection => transform.TransformDirection(LocalAim);
    public Vector3 MuzzlePosition => transform.TransformPoint((Vector3)(LocalAim * .82f));
    public static bool PlayerNeedsCursor
    {
        get
        {
            var player = ZeldaRuntimeRegistry.GetControlledMover();
            return player != null && player.CharacterData is TurretZeldaCharacterData && !player.CharacterData.IsDead;
        }
    }

    protected override void Awake()
    {
        if (!Application.isPlaying) return;
        moveSpeed = 0f;
        base.Awake();
        mover = GetComponent<ZeldaFourWayMover>();
        if (firingSound == null) firingSound = Resources.Load<AudioClip>("Audio/TurretFire");
    }
    protected override void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyCharacterVisual(GetComponent<SpriteRenderer>(), Vector2.up, false, false);
            return;
        }
        base.Update();
        shotCooldown = Mathf.Max(0f, shotCooldown - Time.deltaTime);
        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * .65f);
        if (mover == null) mover = GetComponent<ZeldaFourWayMover>();
        bool controlled = mover != null && mover.isActiveAndEnabled && !IsDead;
        if (controlled && !hadControl)
        {
            priorCursor = Cursor.visible; priorLock = Cursor.lockState;
            awaitReleaseF = true;
        }
        if (controlled) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
        else if (hadControl) ReleaseCursor();
        hadControl = controlled;
        if (!controlled || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) CancelPlayerInput();
        if (headRenderer != null) UpdateHead();
    }
    public void AimAt(Vector2 point, float deltaTime)
    {
        Vector2 direction = point - (Vector2)transform.position;
        if (direction.sqrMagnitude < .0001f) return;
        direction = transform.InverseTransformDirection(direction);
        float wanted = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        headAngle = Mathf.MoveTowardsAngle(headAngle, wanted, maximumTurnSpeed * Mathf.Max(0f, deltaTime));
        UpdateHead();
    }
    public void UpdatePlayerControl(float deltaTime)
    {
        if (IsDead || deltaTime <= 0f) return;
        var camera = Camera.main;
        if (camera != null) AimAt(camera.ScreenToWorldPoint(Input.mousePosition), deltaTime);
        if (Input.GetMouseButtonDown(0)) TryFire();
        AdvanceExitHold(Input.GetKey(KeyCode.F), deltaTime);
    }
    private void AdvanceExitHold(bool held, float deltaTime)
    {
        // The F press that possessed this body must be released before self-destruct.
        if (awaitReleaseF) { if (!held) awaitReleaseF = false; CancelPlayerInput(); return; }
        if (!held) { CancelPlayerInput(); return; }
        if (exitProgress == null)
        {
            var go = new GameObject("Turret Exit Hold"); go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            exitProgress = go.AddComponent<ZeldaPossessionProgressBar>();
        }
        exitHold += Mathf.Max(0f, deltaTime);
        exitProgress.SetProgress(exitHold / suicideHoldDuration);
        if (exitHold >= suicideHoldDuration) { CancelPlayerInput(); KillFromPotion(); }
    }
    public void CancelPlayerInput() { exitHold = 0f; if (exitProgress != null) exitProgress.Hide(); }
    public bool TryFire(bool aiBurst = false)
    {
        if (!Application.isPlaying || IsDead || !CanAttack || shotCooldown > 0f ||
            DocumentReader.IsInputBlocked || Time.timeScale <= 0f) return false;
        var ai = GetComponent<TurretCharacterAi>();
        if (ai != null && ai.IsStunned) return false;
        if (!DisposableHandgunBullet.FireFromMuzzle(this, AimDirection, bulletSpeed, MuzzlePosition, false, false)) return false;
        shotCooldown = aiBurst ? .10f : playerFireCooldown;
        recoil = .065f;
        CharacterPotionBuffs.NotifyAction(this);
        TurretShotAudio.Play(ZeldaRuntimeRegistry.GetGameplayScene(gameObject), MuzzlePosition, firingSound, firingVolume);
        return true;
    }
    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        if (renderer == null) return;
        EnsureSprites(); baseRenderer = renderer;
        renderer.sprite = baseSprite; renderer.color = GetDamageFeedbackTint(Color.white);
        if (headRenderer == null)
        {
            var go = new GameObject("Turret Rotating Head") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            headRenderer = go.AddComponent<SpriteRenderer>(); headRenderer.sprite = headSprite;
        }
        if (headRenderer.transform.parent != renderer.transform)
            headRenderer.transform.SetParent(renderer.transform, false);
        headRenderer.sortingLayerID = renderer.sortingLayerID;
        headRenderer.sortingOrder = renderer.sortingOrder + 1;
        UpdateHead();
    }
    private void UpdateHead()
    {
        if (headRenderer == null) return;
        headRenderer.transform.localRotation = Quaternion.Euler(0, 0, headAngle);
        headRenderer.transform.localPosition = -(Vector3)LocalAim * recoil + GetDamageShakeOffset();
        headRenderer.color = GetDamageFeedbackTint(Color.white);
        if (baseRenderer != null) baseRenderer.color = GetDamageFeedbackTint(Color.white);
    }
    public static void EnsureSprites()
    {
        if (baseSprite != null && headSprite != null) return;
        baseSprite = MakeSprite(false); headSprite = MakeSprite(true);
    }
    private static Sprite MakeSprite(bool head)
    {
        // Coarse 16-PPU machinery: four feet, a turntable, red housing and broad barrel.
        var texture = new Texture2D(24, 28, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color32 dark = new Color32(43, 53, 57, 255), steel = new Color32(113, 132, 139, 255), light = new Color32(181, 199, 198, 255);
        Color32 red = new Color32(170, 38, 43, 255), bright = new Color32(240, 71, 65, 255);
        for (int y = 0; y < 28; y++) for (int x = 0; x < 24; x++)
        {
            float dx = Mathf.Abs(x - 11.5f), dy = Mathf.Abs(y - 11.5f);
            Color c = Color.clear;
            if (!head)
            {
                if ((dx <= 3 && dy <= 10) || (dy <= 3 && dx <= 10)) c = dark;
                if ((dx <= 2 && dy >= 7 && dy <= 9) || (dy <= 2 && dx >= 7 && dx <= 9)) c = steel;
                if (dx <= 6 && dy <= 6 && dx + dy <= 10) c = dark;
                if (dx <= 5 && dy <= 5 && dx + dy <= 8) c = steel;
                if (dx <= 4 && dy <= 4 && dx + dy <= 6) c = light;
                if (dx <= 1 && dy >= 8 && dy <= 9) c = red;
            }
            else
            {
                if (dx <= 5 && y >= 7 && y <= 16 && dx + Mathf.Abs(y - 11.5f) <= 8) c = dark;
                if (dx <= 4 && y >= 8 && y <= 15) c = red;
                if (dx <= 3 && y >= 12 && y <= 23) c = dark;
                if (dx <= 2 && y >= 13 && y <= 22) c = steel;
                if (x == 10 && y >= 14 && y <= 21) c = light;
                if (dx <= 3 && y >= 21 && y <= 23) c = light;
                if (dx <= 2 && y == 23) c = dark;
                if (dx <= 1 && y >= 9 && y <= 11) c = bright;
                if (dx > 3 && dx <= 4 && (y == 10 || y == 13)) c = light;
            }
            texture.SetPixel(x, y, c);
        }
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 24, 28), new Vector2(.5f, 12f / 28f), 16f);
        sprite.name = head ? "Turret Red Gun Head" : "Turret Steel Base";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
    [Serializable] private sealed class State { public float angle, cooldown; public string builderOwner; public TurretCharacterAi.PersistentState ai; }
    public override RuntimeState CaptureRuntimeState()
    {
        var state = base.CaptureRuntimeState();
        var ai = GetComponent<TurretCharacterAi>();
        state.characterSpecificState = JsonUtility.ToJson(new State { angle = headAngle, cooldown = shotCooldown,
            builderOwner = GetComponent<BuilderTurretBond>()?.OwnerId, ai = ai != null ? ai.CaptureTurretState() : null });
        return state;
    }
    public override void ApplyRuntimeState(RuntimeState state)
    {
        base.ApplyRuntimeState(state); moveSpeed = 0f;
        if (string.IsNullOrEmpty(state.characterSpecificState)) return;
        var saved = JsonUtility.FromJson<State>(state.characterSpecificState);
        if (saved == null) return;
        headAngle = saved.angle; shotCooldown = Mathf.Max(0f, saved.cooldown);
        BuilderTurretBond.Restore(gameObject, saved.builderOwner);
        GetComponent<TurretCharacterAi>()?.ApplyTurretState(saved.ai);
        UpdateHead();
    }
    private void ReleaseCursor() { Cursor.visible = priorCursor; Cursor.lockState = priorLock; CancelPlayerInput(); }
    private void OnDisable() { if (hadControl) ReleaseCursor(); hadControl = false; }
    private void OnDestroy()
    {
        if (headRenderer != null)
        {
            if (Application.isPlaying) Destroy(headRenderer.gameObject); else DestroyImmediate(headRenderer.gameObject);
        }
    }
}
