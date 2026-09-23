using System;
using System.Collections.Generic;
using UnityEngine;

public enum ZeldaInitialFacingDirection
{
    Down,
    Up,
    Left,
    Right
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(ZeldaCharacterData))]
public class ZeldaFourWayMover : MonoBehaviour
{
    private const float MaximumDownwardAttackUpwardAdjustment = 0.1f;

    private const string VisualObjectName = "Visual";

    [Header("Initial Facing")]
    [SerializeField] private ZeldaInitialFacingDirection initialFacingDirection = ZeldaInitialFacingDirection.Down;
    [SerializeField, Min(0f), Tooltip("Distance from the character centre to the possession-area centre.")]
    private float controlSwitchDistance = 1.1f;
    [SerializeField, Min(0.05f), Tooltip("Half of the possession area's forward length. Its full length remains twice this value.")]
    private float controlSwitchRadius = 0.45f;
    [SerializeField, Min(0.05f)] private float controlSwitchHoldDuration = 1f;
    [Tooltip("Additional possession hold time applied while the currently controlled character is not a Ghost.")]
    [SerializeField, Min(0f)] private float nonGhostPossessionDurationBonus = 2f;
    [SerializeField] private Vector2 controlSwitchProgressOffset = new Vector2(0f, 1.1f);
    [SerializeField, Min(0.1f)] private float controlSwitchProgressScale = 0.8f;
    [SerializeField, Min(0.1f)] private float controlSwitchFailureDuration = 0.9f;
    [SerializeField, Min(1)] private int controlSwitchFailureFlashCount = 3;
    [SerializeField] private Vector2 possessionPromptOffset = new Vector2(0f, 0.9f);
    [SerializeField] private LayerMask controlSwitchLayers = ~0;
    [SerializeField] private LayerMask solidCollisionLayers = ~0;
    [SerializeField] private float collisionSkinWidth = 0.01f;
    [SerializeField] private GameObject deathGhostPrefab;

    [Header("Player Sprint (Non-Ghost Only)")]
    [SerializeField] private KeyCode sprintKey = KeyCode.Space;
    [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 2.00f;
    [SerializeField, Min(0.1f)] private float maximumStamina = 15f;
    [SerializeField, Min(0.01f)] private float staminaConsumptionPerSecond = 1f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1.25f;
    [SerializeField, Min(0.01f)] private float staminaRecoveryPerSecond = 1.5f;
    [SerializeField, Range(0f, 1f)] private float exhaustedResumeThreshold = 0.25f;
    [SerializeField] private Vector2 staminaProgressOffset = new Vector2(0f, 1.3f);
    [SerializeField, Min(0.1f)] private float staminaProgressScale = 0.8f;
    [SerializeField] private Color staminaProgressColor =
        new Color(0.22f, 0.86f, 0.4f, 1f);

    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private ZeldaCharacterData characterData;
    private Vector2 moveDirection;
    private Vector2 facingDirection = Vector2.down;
    private float attackTimer;
    private float controlSwitchHoldTimer;
    private ZeldaFourWayMover pendingControlTarget;
    private PossessionTransferParticles possessionTransferParticles;
    private ZeldaPossessionProgressBar possessionProgressBar;
    private ZeldaPossessionProgressBar staminaProgressBar;
    private float currentStamina;
    private float staminaRecoveryTimer;
    private bool isSprinting;
    private bool staminaExhausted;
    private bool possessionAttemptRejected;
    private GameObject possessionPromptObject;
    private TextMesh possessionPromptText;
    private Material possessionPromptMaterial;
    private Font possessionPromptFont;
    private bool suppressPossessionPromptFromArbiter;
    private bool possessionInteractionSelected;
    private CardboardBoxWearState activeCardboardBox;
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];
    private readonly Collider2D[] controlSwitchHits = new Collider2D[32];
    private readonly Dictionary<Sprite, Vector2> possessionVisualSizeCache =
        new Dictionary<Sprite, Vector2>();

    public Vector2 FacingDirection => facingDirection;
    public Vector3 PossessionVisualCenter => spriteRenderer != null && spriteRenderer.sprite != null
        ? spriteRenderer.bounds.center : transform.position;
    // Camera systems run before this mover's Awake on initial scene creation.
    public ZeldaCharacterData CharacterData => characterData != null
        ? characterData : (characterData = GetComponent<ZeldaCharacterData>());
    public bool IsMoving => moveDirection.sqrMagnitude > 0f;
    public bool IsAttacking => attackTimer > 0f;
    public void CancelAttackForStun()
    {
        attackTimer = 0f;
        moveDirection = Vector2.zero;
        ApplyFacingVisual();
    }
    public bool IsSprinting => isSprinting;
    public CardboardBoxWearState ActiveCardboardBox => activeCardboardBox;
    public bool IsWearingCardboardBox => activeCardboardBox != null;
    public float CurrentStamina => currentStamina;
    public void RestoreSavedMovement(Vector2 facing, float stamina)
    {
        facingDirection = facing.sqrMagnitude > 0f ? facing.normalized : Vector2.down;
        currentStamina = Mathf.Clamp(stamina, 0f, maximumStamina);
        moveDirection = Vector2.zero;
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.velocity = Vector2.zero;
        ApplyFacingVisual();
    }
    public float StaminaNormalized =>
        maximumStamina > 0f
            ? Mathf.Clamp01(currentStamina / maximumStamina)
            : 0f;
    public bool IsStaminaBarVisible => ShouldDisplayStaminaBar();
    public bool IsPossessionInProgress => pendingControlTarget != null && controlSwitchHoldTimer > 0f;
    public float PossessionProgress => IsPossessionInProgress
        ? Mathf.Clamp01(controlSwitchHoldTimer / GetActivePossessionDuration())
        : 0f;
    public Vector2 InitialFacingVector => GetInitialFacingVector();
    public event Action PositionChanged;

    private void Awake()
    {
        ZeldaRuntimeRegistry.Register(this);
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        characterData = GetComponent<ZeldaCharacterData>();
        facingDirection = GetInitialFacingVector();
        spriteRenderer = EnsureVisualRenderer(spriteRenderer);

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.useFullKinematicContacts = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        bodyCollider.isTrigger = false;

        spriteRenderer.sortingOrder = 1;
        CreatePossessionProgressBar();
        CreateStaminaProgressBar();
        currentStamina = Mathf.Max(0.1f, maximumStamina);
        ApplyFacingVisual();
    }

    private Vector2 GetInitialFacingVector()
    {
        switch (initialFacingDirection)
        {
            case ZeldaInitialFacingDirection.Up:
                return Vector2.up;
            case ZeldaInitialFacingDirection.Left:
                return Vector2.left;
            case ZeldaInitialFacingDirection.Right:
                return Vector2.right;
            default:
                return Vector2.down;
        }
    }

    private void OnEnable()
    {
        ZeldaRuntimeRegistry.NotifyMoverEnabled(this);
        DisableCharacterAi();
        ResetControlSwitchProgress();
        possessionAttemptRejected = false;
        if (possessionProgressBar != null)
        {
            possessionProgressBar.Hide();
        }
        StopSprintingAndRefreshStaminaBar();
    }

    private void OnDisable()
    {
        if (characterSkillProgressBar != null) characterSkillProgressBar.Hide();
        ZeldaRuntimeRegistry.NotifyMoverDisabled(this);
        possessionInteractionSelected = false;
        SetPossessionPromptVisible(false);
        ResetControlSwitchProgress();
        possessionAttemptRejected = false;
        if (possessionProgressBar != null)
        {
            possessionProgressBar.Hide();
        }
        StopSprintingAndHideBar();
    }

    private void LateUpdate()
    {
        UpdatePossessionPrompt();
        RefreshCharacterSkillProgress();
    }

    private ZeldaPossessionProgressBar characterSkillProgressBar;
    private void RefreshCharacterSkillProgress()
    {
        if (characterData == null || !characterData.IsCombatExpertiseActive)
        {
            if (characterSkillProgressBar != null) characterSkillProgressBar.Hide();
            return;
        }
        if (characterSkillProgressBar == null)
        {
            var progress = new GameObject("Combat Expertise Progress");
            progress.transform.SetParent(transform, false);
            characterSkillProgressBar = progress.AddComponent<ZeldaPossessionProgressBar>();
        }
        float scale = Mathf.Max(0.1f, staminaProgressScale);
        // The shared bar fills only its bottom 3/14 units. Leave a small gap above that visible part.
        characterSkillProgressBar.transform.localPosition = GetStaminaProgressLocalPosition() +
            Vector3.up * ((3f / 14f) * scale + 0.06f);
        characterSkillProgressBar.transform.localScale = Vector3.one * scale;
        characterSkillProgressBar.SetProgress(characterData.CombatExpertiseProgress, new Color(1f, 0.08f, 0.06f, 1f));
    }

    private void UpdatePossessionPrompt()
    {
        if (ClockworkPuppetRuntime.BlocksCharacterInput)
        {
            possessionInteractionSelected = false;
            SetPossessionPromptVisible(false);
            return;
        }

        bool failureVisible = possessionAttemptRejected ||
            (possessionProgressBar != null && possessionProgressBar.IsFailureActive);
        suppressPossessionPromptFromArbiter = IsPossessionInProgress ||
            Input.GetKey(KeyCode.F) || failureVisible;
        ZeldaFourWayMover candidate = failureVisible
            ? null
            : (pendingControlTarget != null
                ? pendingControlTarget
                : FindControlSwitchCandidate());
        // Offer F for nearby characters even when energy or AI state prevents
        // possession. Eligibility is checked when the selected action is used.
        bool hasPossessionTarget = !DocumentReader.IsInputBlocked
            && characterData != null
            && !characterData.IsDead
            && IsPotentialControlSwitchTarget(candidate);

        if (!hasPossessionTarget)
        {
            possessionInteractionSelected = false;
            SetPossessionPromptVisible(false);
            return;
        }

        EnsurePossessionPrompt();
        if (possessionPromptObject != null)
        {
            possessionPromptObject.transform.position =
                GetOverheadWorldPosition(possessionPromptOffset);
            possessionPromptObject.transform.rotation = Quaternion.identity;
        }

        // Interaction selection must not depend on the prompt font/UI having
        // finished initialising.  In particular, a newly possessed non-Ghost
        // can become active before the persistent HUD singleton is available.
        // Skipping the offer in that frame left F permanently unselected and
        // silently cancelled every possession attempt.
        ZeldaInteractionArbiter.OfferInteraction(
            candidate,
            this,
            KeyCode.F,
            candidate.transform.position,
            SetPossessionPromptFromArbiter);

        if (possessionPromptFont != null && possessionPromptMaterial != null)
        {
            possessionPromptFont.RequestCharactersInTexture(
                "按[F]附身角色",
                72,
                FontStyle.Normal);
            possessionPromptMaterial.mainTexture = possessionPromptFont.material.mainTexture;
        }
    }

    private void SetPossessionPromptFromArbiter(bool visible)
    {
        possessionInteractionSelected = visible;
        SetPossessionPromptVisible(
            visible && !suppressPossessionPromptFromArbiter);
    }

    private void EnsurePossessionPrompt()
    {
        if (possessionPromptObject != null)
            return;

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        possessionPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (possessionPromptFont == null)
            return;

        possessionPromptFont.RequestCharactersInTexture(
            "按[F]附身角色",
            72,
            FontStyle.Normal);

        possessionPromptObject = new GameObject(name + " Possession Prompt");
        possessionPromptText = possessionPromptObject.AddComponent<TextMesh>();
        possessionPromptText.text = "按[F]附身角色";
        possessionPromptText.font = possessionPromptFont;
        possessionPromptText.fontSize = 72;
        possessionPromptText.characterSize = 0.035f;
        possessionPromptText.anchor = TextAnchor.MiddleCenter;
        possessionPromptText.alignment = TextAlignment.Center;
        possessionPromptText.color = ZeldaUiPalette.Primary;

        MeshRenderer promptRenderer = possessionPromptObject.GetComponent<MeshRenderer>();
        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestSortingLayerValue)
            {
                highestSortingLayerValue = sortingLayer.value;
                highestSortingLayerId = sortingLayer.id;
            }
        }

        promptRenderer.sortingLayerID = highestSortingLayerId;
        promptRenderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        possessionPromptMaterial = new Material(possessionPromptFont.material)
        {
            name = name + " Possession Prompt Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        possessionPromptMaterial.mainTexture = possessionPromptFont.material.mainTexture;
        promptRenderer.sharedMaterial = possessionPromptMaterial;
        possessionPromptObject.SetActive(false);
    }

    private void SetPossessionPromptVisible(bool visible)
    {
        if (possessionPromptObject != null
            && possessionPromptObject.activeSelf != visible)
        {
            possessionPromptObject.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        ZeldaRuntimeRegistry.Unregister(this);
        if (possessionPromptObject != null)
        {
            Destroy(possessionPromptObject);
        }

        if (possessionPromptMaterial != null)
        {
            Destroy(possessionPromptMaterial);
        }
    }

    private SpriteRenderer EnsureVisualRenderer(SpriteRenderer rootRenderer)
    {
        if (rootRenderer.transform != transform)
        {
            return rootRenderer;
        }

        Transform visualTransform = transform.Find(VisualObjectName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(VisualObjectName);
            visualObject.transform.SetParent(transform, false);
            visualTransform = visualObject.transform;
        }

        SpriteRenderer visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        visualRenderer.sortingLayerID = rootRenderer.sortingLayerID;
        visualRenderer.sortingOrder = rootRenderer.sortingOrder;
        visualRenderer.color = rootRenderer.color;

        rootRenderer.enabled = false;
        return visualRenderer;
    }

    private void Update()
    {
        if (characterData != null) characterData.ApplyPlayerHealthUpgrade();
        if (DocumentReader.IsInputBlocked)
        {
            moveDirection = Vector2.zero;
            StopSprintingAndRefreshStaminaBar();
            ResetControlSwitchProgress();
            ApplyFacingVisual();
            return;
        }

        if (ClockworkPuppetRuntime.BlocksCharacterInput)
        {
            moveDirection = Vector2.zero;
            StopSprintingAndRefreshStaminaBar();
            ResetControlSwitchProgress();
            possessionAttemptRejected = false;
            ApplyFacingVisual();
            return;
        }

        if (characterData.IsDead)
        {
            moveDirection = Vector2.zero;
            StopSprintingAndHideBar();
            ResetControlSwitchProgress();
            return;
        }

        if (UpdateControlSwitchProgress(Time.deltaTime))
        {
            StopSprintingAndRefreshStaminaBar();
            return;
        }

        if (activeCardboardBox != null && Input.GetKeyDown(sprintKey))
        {
            activeCardboardBox.RequestExit();
        }

        if (characterData.CanAttack && !characterData.IsGhostForm && Input.GetMouseButtonDown(0) && !IsAttacking)
        {
            TryPerformAttack(facingDirection);
        }

        if (IsAttacking)
        {
            attackTimer -= Time.deltaTime;
            moveDirection = Vector2.zero;
            UpdateSprint(false, Time.deltaTime);
            ApplyFacingVisual();
            return;
        }

        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveDirection = ToMoveDirection(rawInput);
        UpdateSprint(moveDirection.sqrMagnitude > 0f, Time.deltaTime);

        if (rawInput.sqrMagnitude > 0f)
        {
            facingDirection = rawInput.normalized;
        }

        ApplyFacingVisual();
    }

    private void FixedUpdate()
    {
        if (IsAttacking || characterData.IsDead)
        {
            return;
        }

        float movementSpeed = characterData.MoveSpeed *
            (isSprinting ? Mathf.Max(1f, sprintSpeedMultiplier) : 1f) *
            (activeCardboardBox != null
                ? activeCardboardBox.MovementSpeedMultiplier
                : 1f);
        Vector2 displacement =
            moveDirection * movementSpeed * Time.fixedDeltaTime;
        Vector2 allowedDisplacement = GetBlockedDisplacement(displacement);
        if (allowedDisplacement.sqrMagnitude <= 0f)
        {
            return;
        }

        rb.MovePosition(rb.position + allowedDisplacement);
        PositionChanged?.Invoke();
    }

    private Vector2 GetBlockedDisplacement(Vector2 displacement)
    {
        Vector2 directDisplacement =
            GetLinearBlockedDisplacement(displacement, out Vector2 blockingNormal);
        if ((directDisplacement - displacement).sqrMagnitude <= 0.0000001f)
        {
            return directDisplacement;
        }

        Vector2 desiredDirection = displacement.normalized;
        Vector2 bestDisplacement = directDisplacement;
        float bestProgress = Vector2.Dot(bestDisplacement, desiredDirection);

        // At a true corner the blocking normal is diagonal. Project the
        // remaining motion onto its tangent; at a flat wall this naturally
        // produces no unwanted sideways movement.
        if (blockingNormal.sqrMagnitude > 0.0001f)
        {
            Vector2 remaining = displacement - directDisplacement;
            Vector2 tangent =
                remaining - blockingNormal * Vector2.Dot(remaining, blockingNormal);
            Vector2 tangentDisplacement =
                GetLinearBlockedDisplacement(tangent, out _);
            float tangentProgress =
                Vector2.Dot(tangentDisplacement, desiredDirection);
            if (tangentProgress > bestProgress)
            {
                bestDisplacement = tangentDisplacement;
                bestProgress = tangentProgress;
            }
        }

        // A diagonal cast can touch a convex corner even though one component
        // of the requested movement is clear.
        Vector2 horizontal =
            GetLinearBlockedDisplacement(new Vector2(displacement.x, 0f), out _);
        Vector2 vertical =
            GetLinearBlockedDisplacement(new Vector2(0f, displacement.y), out _);
        float horizontalProgress = Vector2.Dot(horizontal, desiredDirection);
        float verticalProgress = Vector2.Dot(vertical, desiredDirection);

        if (horizontalProgress > bestProgress &&
            horizontalProgress >= verticalProgress)
        {
            return horizontal;
        }

        return verticalProgress > bestProgress ? vertical : bestDisplacement;
    }

    private Vector2 GetLinearBlockedDisplacement(
        Vector2 displacement,
        out Vector2 blockingNormal)
    {
        blockingNormal = Vector2.zero;
        float distance = displacement.magnitude;
        if (distance <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 direction = displacement / distance;
        Vector2 castCenter = bodyCollider.transform.TransformPoint(bodyCollider.offset);
        Vector3 lossyScale = bodyCollider.transform.lossyScale;
        Vector2 castSize = new Vector2(
            Mathf.Max(0.02f,
                bodyCollider.size.x * Mathf.Abs(lossyScale.x) -
                collisionSkinWidth * 2f),
            Mathf.Max(0.02f,
                bodyCollider.size.y * Mathf.Abs(lossyScale.y) -
                collisionSkinWidth * 2f));
        float castAngle = bodyCollider.transform.eulerAngles.z;
        int hitCount = Physics2D.BoxCastNonAlloc(
            castCenter,
            castSize,
            castAngle,
            direction,
            movementHits,
            distance + collisionSkinWidth,
            solidCollisionLayers);
        float allowedDistance = distance;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = movementHits[i].collider;
            if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger)
            {
                continue;
            }

            ZeldaCharacterData hitCharacter = hitCollider.GetComponentInParent<ZeldaCharacterData>();
            if (hitCharacter != null &&
                (!characterData.ParticipatesInCharacterCollision || !hitCharacter.ParticipatesInCharacterCollision))
            {
                continue;
            }

            if (IsMovingAwayFromCharacter(hitCollider, direction))
            {
                continue;
            }

            // Collider casts can report a zero-distance contact while moving
            // parallel to or away from a surface near its corner. Only a
            // surface facing into the requested motion is actually blocking.
            if (Vector2.Dot(direction, movementHits[i].normal) >= -0.001f)
            {
                continue;
            }

            float hitAllowedDistance =
                Mathf.Max(0f, movementHits[i].distance - collisionSkinWidth);
            if (hitAllowedDistance < allowedDistance)
            {
                allowedDistance = hitAllowedDistance;
                blockingNormal = movementHits[i].normal;
            }
        }

        return direction * allowedDistance;
    }

    private bool IsMovingAwayFromCharacter(Collider2D hitCollider, Vector2 movementDirection)
    {
        ZeldaCharacterData otherCharacter = hitCollider.GetComponentInParent<ZeldaCharacterData>();
        if (otherCharacter == null || otherCharacter == characterData)
        {
            return false;
        }

        Vector2 awayFromOther = (Vector2)bodyCollider.bounds.center - (Vector2)hitCollider.bounds.center;
        if (awayFromOther.sqrMagnitude <= 0.0001f)
        {
            float escapeSide = GetInstanceID() < otherCharacter.GetInstanceID() ? -1f : 1f;
            awayFromOther = Vector2.right * escapeSide;
        }

        // A zero-distance cast caused by touching corners must not prevent a
        // character from separating. Movement toward the other character is
        // still treated as solid collision.
        return Vector2.Dot(movementDirection, awayFromOther.normalized) > 0.001f;
    }

    private Vector2 ToMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        return input.normalized;
    }

    private void ApplyFacingVisual()
    {
        characterData.ApplyCharacterVisualWithMovement(spriteRenderer, facingDirection, IsMoving, IsAttacking);
        if (characterData.IsGhostForm) characterData.GhostForm.ApplyVisual(spriteRenderer, IsMoving);
    }

    public void RefreshFormVisuals()
    {
        if (characterData == null) return;
        isSprinting = false;
        ApplyFacingVisual();
        RefreshStaminaProgressBar();
    }

    private Vector2 ToNearestCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            return Vector2.down;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0f ? Vector2.right : Vector2.left;
        }

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    public bool TryPerformAttack(Vector2 requestedFacingDirection)
    {
        return TryPerformAttack(requestedFacingDirection, null);
    }

    public ZeldaAttackHitbox.AttackOutcome LastAttackOutcome { get; private set; }

    public bool TryPerformAttack(Vector2 requestedFacingDirection, UnityEngine.Object observedTarget)
    {
        if (characterData == null || characterData.IsDead || characterData.IsGhostForm || !characterData.CanAttack || IsAttacking)
        {
            return false;
        }

        if (requestedFacingDirection.sqrMagnitude > 0f)
        {
            facingDirection = requestedFacingDirection.normalized;
        }

        attackTimer = characterData.AttackDuration;
        moveDirection = Vector2.zero;

        if (characterData.AttackPrefab == null)
        {
            Debug.LogWarning("ZeldaFourWayMover needs an attack prefab before it can spawn an attack hitbox.", this);
            attackTimer = 0f;
            return false;
        }

        Vector2 attackDirection = ToNearestCardinalDirection(facingDirection);
        Quaternion attackRotation = Quaternion.FromToRotation(Vector3.up, attackDirection);
        Vector3 rotatedSpawnOffset = attackRotation * characterData.AttackSpawnOffset;
        Vector3 attackPosition = transform.position + rotatedSpawnOffset;
        if (attackDirection == Vector2.down &&
            rotatedSpawnOffset.sqrMagnitude > 0.0001f)
        {
            // Pull downward attacks slightly back toward the character.
            // Scaling by the authored offset keeps short-range hitboxes from
            // crossing the character, while centered radial attacks stay put.
            float upwardAdjustment = Mathf.Min(
                MaximumDownwardAttackUpwardAdjustment,
                rotatedSpawnOffset.magnitude * 0.25f);
            attackPosition += Vector3.up * upwardAdjustment;
        }

        GameObject attackObject = Instantiate(characterData.AttackPrefab, attackPosition, attackRotation);
        LastAttackOutcome = null;

        ZeldaAttackHitbox hitbox = attackObject.GetComponent<ZeldaAttackHitbox>();
        if (hitbox != null)
        {
            hitbox.Configure(
                characterData.AttackDuration,
                characterData.AttackSize,
                characterData.AttackVisualTint,
                characterData.FinalAttackPower,
                characterData,
                characterData.AttackVisualShape,
                false,
                true);
            if (observedTarget != null)
                LastAttackOutcome = hitbox.ObserveTarget(observedTarget);
        }
        else if (observedTarget != null)
        {
            // A visual-only attack cannot confirm contact; do not lock AI waiting for it.
            LastAttackOutcome = new ZeldaAttackHitbox.AttackOutcome
                { Target = observedTarget, IsComplete = true };
        }

        characterData.PlayAttackSound();
        ApplyFacingVisual();
        return true;
    }

    public void AdvanceExternalAttackTimer(float deltaTime)
    {
        if (enabled || attackTimer <= 0f)
        {
            return;
        }

        attackTimer = Mathf.Max(0f, attackTimer - Mathf.Max(0f, deltaTime));
    }

    private bool UpdateControlSwitchProgress(float deltaTime)
    {
        if (!Input.GetKey(KeyCode.F))
        {
            ResetControlSwitchProgress();
            possessionAttemptRejected = false;
            return false;
        }

        if (possessionAttemptRejected)
        {
            return false;
        }

        ZeldaFourWayMover target = FindControlSwitchCandidate();
        if (target == null)
        {
            ResetControlSwitchProgress();
            return false;
        }

        if (!possessionInteractionSelected &&
            !ZeldaInteractionArbiter.IsSelected(
                target,
                this,
                KeyCode.F))
        {
            ResetControlSwitchProgress();
            possessionAttemptRejected = false;
            return false;
        }

        if (!IsValidControlSwitchTarget(target))
        {
            RejectPossessionAttempt();
            return false;
        }

        if (target != pendingControlTarget)
        {
            StopPossessionTransferParticles(false);
            ReleasePendingControlTarget();
            pendingControlTarget = target;
            controlSwitchHoldTimer = 0f;
            GhostZeldaCharacterData ghostVisual = CharacterData as GhostZeldaCharacterData;
            if (ghostVisual == null && deathGhostPrefab != null)
                ghostVisual = deathGhostPrefab.GetComponent<GhostZeldaCharacterData>();
            Color particleTint = ghostVisual != null ? ghostVisual.ConsciousnessParticleTint
                : new Color(0.72f, 0.95f, 1f, 0.78f);
            possessionTransferParticles = PossessionTransferParticles.Create(this, target, particleTint);

            ZeldaCharacterAiBase targetAi = pendingControlTarget.GetComponent<ZeldaCharacterAiBase>();
            if (targetAi != null)
            {
                targetAi.SetPossessionLock(true);
            }
        }

        controlSwitchHoldTimer += Mathf.Max(0f, deltaTime);
        float activePossessionDuration = GetActivePossessionDuration();
        float progress = Mathf.Clamp01(controlSwitchHoldTimer / activePossessionDuration);
        if (possessionProgressBar != null)
        {
            possessionProgressBar.transform.localPosition =
                GetOverheadLocalPosition(controlSwitchProgressOffset);
            possessionProgressBar.SetProgress(progress);
        }

        if (controlSwitchHoldTimer < activePossessionDuration)
        {
            return false;
        }

        ZeldaFourWayMover completedTarget = pendingControlTarget;
        StopPossessionTransferParticles(true);
        ResetControlSwitchProgress();
        return TrySwitchControl(completedTarget);
    }

    private float GetActivePossessionDuration()
    {
        float duration = Mathf.Max(0.05f, controlSwitchHoldDuration);
        if (!characterData.IsGhostLike)
        {
            duration += Mathf.Max(0f, nonGhostPossessionDurationBonus);
        }

        return duration;
    }

    private bool TrySwitchControl(ZeldaFourWayMover target)
    {
        if (!IsValidControlSwitchTarget(target))
        {
            RejectPossessionAttempt();
            return false;
        }

        ZeldaCharacterData targetData = target.GetComponent<ZeldaCharacterData>();

        if (DominoSkillRuntime.TryPossess(this, target)) return true;
        target.ReceiveControl(facingDirection);
        PlayerGrowthAttributes.Instance?.RecordPossessedCharacter(target.GetComponent<ZeldaCharacterData>());
        SoulMarkRuntime.ClearAfterDirectPossession(target);
        NotifyAiWitnessesOfPossession(target);
        moveDirection = Vector2.zero;
        attackTimer = 0f;
        ApplyFacingVisual();
        enabled = false;

        QuestJournalManager.GetOrCreate().RecordControlledBehemoth(target);
        characterData.DestroyAfterSuccessfulPossession();
        return true;
    }

    private void NotifyAiWitnessesOfPossession(ZeldaFourWayMover newControlledMover)
    {
        if (newControlledMover == null)
        {
            return;
        }

        foreach (ZeldaCharacterAiBase witness in ZeldaRuntimeRegistry.AiCharacters)
        {
            if (witness == null || !witness.isActiveAndEnabled)
            {
                continue;
            }

            witness.OnPossessionWitnessed(this, newControlledMover);
        }
    }

    public void InterruptPossessionByDamage()
    {
        if (!IsPossessionInProgress)
        {
            return;
        }

        ResetControlSwitchProgress();
        // Require F to be released before another attempt can begin, otherwise
        // holding the key would restart possession immediately after the hit.
        possessionAttemptRejected = true;
    }

    private void CreatePossessionProgressBar()
    {
        const string progressObjectName = "Possession Progress";
        Transform existing = transform.Find(progressObjectName);
        GameObject progressObject = existing != null
            ? existing.gameObject
            : new GameObject(progressObjectName);
        progressObject.transform.SetParent(transform, false);
        progressObject.transform.localPosition =
            GetOverheadLocalPosition(controlSwitchProgressOffset);
        progressObject.transform.localScale = Vector3.one * controlSwitchProgressScale;

        possessionProgressBar = progressObject.GetComponent<ZeldaPossessionProgressBar>();
        if (possessionProgressBar == null)
        {
            possessionProgressBar = progressObject.AddComponent<ZeldaPossessionProgressBar>();
        }

        possessionProgressBar.Hide();
    }

    private void CreateStaminaProgressBar()
    {
        const string progressObjectName = "Sprint Stamina";
        Transform existing = transform.Find(progressObjectName);
        GameObject progressObject = existing != null
            ? existing.gameObject
            : new GameObject(progressObjectName);
        progressObject.transform.SetParent(transform, false);
        progressObject.transform.localPosition = GetStaminaProgressLocalPosition();
        progressObject.transform.localRotation = Quaternion.identity;
        progressObject.transform.localScale =
            Vector3.one * Mathf.Max(0.1f, staminaProgressScale);

        staminaProgressBar =
            progressObject.GetComponent<ZeldaPossessionProgressBar>();
        if (staminaProgressBar == null)
        {
            staminaProgressBar =
                progressObject.AddComponent<ZeldaPossessionProgressBar>();
        }

        staminaProgressBar.Hide();
    }

    private void UpdateSprint(bool movementRequested, float deltaTime)
    {
        if (characterData.IsGhostForm)
        {
            isSprinting = false;
            RefreshStaminaProgressBar();
            return;
        }
        bool isGhost = characterData is GhostZeldaCharacterData;
        bool canSprint =
            !isGhost &&
            movementRequested &&
            !IsAttacking &&
            !IsPossessionInProgress &&
            !PauseMenuController.IsPaused &&
            Input.GetKey(sprintKey) &&
            !staminaExhausted &&
            currentStamina > 0f;

        if (canSprint)
        {
            isSprinting = true;
            staminaRecoveryTimer = 0f;
            currentStamina = Mathf.Max(
                0f,
                currentStamina -
                Mathf.Max(0.01f, staminaConsumptionPerSecond) *
                Mathf.Max(0f, deltaTime));

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                staminaExhausted = true;
            }

            if (staminaProgressBar != null)
            {
                RefreshStaminaProgressBar();
            }
            return;
        }

        isSprinting = false;

        if (isGhost)
        {
            currentStamina = Mathf.Max(0.1f, maximumStamina);
            staminaRecoveryTimer = 0f;
            staminaExhausted = false;
            RefreshStaminaProgressBar();
            return;
        }

        if (activeCardboardBox != null)
        {
            staminaRecoveryTimer = 0f;
            RefreshStaminaProgressBar();
            return;
        }

        staminaRecoveryTimer += Mathf.Max(0f, deltaTime);
        if (staminaRecoveryTimer >= Mathf.Max(0f, staminaRecoveryDelay))
        {
            currentStamina = Mathf.Min(
                Mathf.Max(0.1f, maximumStamina),
                currentStamina +
                Mathf.Max(0.01f, staminaRecoveryPerSecond) *
                Mathf.Max(0f, deltaTime));
        }

        float resumeStamina =
            Mathf.Max(0.1f, maximumStamina) *
            Mathf.Clamp01(exhaustedResumeThreshold);
        if (staminaExhausted && currentStamina >= resumeStamina)
        {
            staminaExhausted = false;
        }

        RefreshStaminaProgressBar();
    }

    private bool ShouldDisplayStaminaBar()
    {
        if (!isActiveAndEnabled ||
            characterData == null ||
            characterData.IsDead ||
            characterData is GhostZeldaCharacterData)
        {
            return false;
        }

        if (characterData.IsGhostForm) return true;
        float staminaMaximum = Mathf.Max(0.1f, maximumStamina);
        return currentStamina < staminaMaximum - 0.001f;
    }

    private void RefreshStaminaProgressBar()
    {
        if (staminaProgressBar == null)
        {
            return;
        }

        if (!ShouldDisplayStaminaBar())
        {
            staminaProgressBar.Hide();
            return;
        }

        staminaProgressBar.transform.localPosition =
            GetStaminaProgressLocalPosition();
        staminaProgressBar.transform.localRotation = Quaternion.identity;
        staminaProgressBar.transform.localScale =
            Vector3.one * Mathf.Max(0.1f, staminaProgressScale);
        staminaProgressBar.SetProgress(characterData.IsGhostForm ? characterData.GhostForm.Remaining / characterData.GhostForm.TotalDuration : StaminaNormalized,
            characterData.IsGhostForm ? new Color(0.08f, 0.2f, 0.65f, 1f) : staminaProgressColor);
    }

    private void StopSprintingAndRefreshStaminaBar()
    {
        isSprinting = false;
        RefreshStaminaProgressBar();
    }

    public void RegisterCardboardBox(CardboardBoxWearState boxState)
    {
        if (boxState != null)
        {
            activeCardboardBox = boxState;
        }
    }

    public void UnregisterCardboardBox(CardboardBoxWearState boxState)
    {
        if (activeCardboardBox == boxState)
        {
            activeCardboardBox = null;
        }
    }

    public float ConsumeStamina(float amount)
    {
        if (amount <= 0f || characterData.IsGhostLike)
        {
            return currentStamina;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        staminaRecoveryTimer = 0f;
        if (currentStamina <= 0f)
        {
            staminaExhausted = true;
        }

        RefreshStaminaProgressBar();
        return currentStamina;
    }

    public Vector3 GetOverheadWorldPosition(Vector2 configuredOffset)
    {
        return transform.position + GetOverheadLocalPosition(configuredOffset);
    }

    public Vector3 GetOverheadLocalPosition(Vector2 configuredOffset)
    {
        return configuredOffset;
    }

    private Vector3 GetStaminaProgressLocalPosition()
    {
        // Keep the stamina bar immediately above the possession bar. Larger
        // characters can still raise both through their configured offsets.
        // Unlike the previous calculation, there is no hard-coded 1.25 floor
        // or serialized clearance that can retain a stale value after reload.
        const float progressBarSeparation = 0.2f;
        float minimumStaminaY = controlSwitchProgressOffset.y +
            progressBarSeparation;

        return new Vector3(
            staminaProgressOffset.x,
            Mathf.Max(staminaProgressOffset.y, minimumStaminaY),
            0f);
    }

    private void StopSprintingAndHideBar()
    {
        isSprinting = false;
        if (staminaProgressBar != null)
        {
            staminaProgressBar.Hide();
        }
    }

    private void ResetControlSwitchProgress()
    {
        StopPossessionTransferParticles(false);
        ReleasePendingControlTarget();
        pendingControlTarget = null;
        controlSwitchHoldTimer = 0f;
        if (possessionProgressBar != null)
        {
            possessionProgressBar.HideProgress();
            possessionProgressBar.transform.localPosition =
                GetOverheadLocalPosition(controlSwitchProgressOffset);
            possessionProgressBar.transform.localScale = Vector3.one * controlSwitchProgressScale;
        }
    }

    private void RejectPossessionAttempt()
    {
        ResetControlSwitchProgress();
        possessionAttemptRejected = true;
        if (possessionProgressBar != null)
        {
            possessionProgressBar.ShowFailure(
                controlSwitchFailureDuration,
                controlSwitchFailureFlashCount);
        }
    }

    private void StopPossessionTransferParticles(bool completed)
    {
        if (possessionTransferParticles != null)
            possessionTransferParticles.StopEmission(completed);
        possessionTransferParticles = null;
    }

    private void ReleasePendingControlTarget()
    {
        if (pendingControlTarget == null)
        {
            return;
        }

        ZeldaCharacterAiBase targetAi = pendingControlTarget.GetComponent<ZeldaCharacterAiBase>();
        if (targetAi != null)
        {
            targetAi.SetPossessionLock(false);
        }
    }

    private ZeldaFourWayMover FindControlSwitchCandidate()
    {
        Vector2 possessionDirection = ToNearestCardinalDirection(
            facingDirection);
        Vector2 searchCenter = rb.position +
            possessionDirection * controlSwitchDistance;
        float forwardLength = Mathf.Max(0.1f, controlSwitchRadius * 2f);
        float visualWidth = GetPossessionAreaVisualWidth(
            possessionDirection);
        Vector2 searchSize = new Vector2(visualWidth, forwardLength);
        float searchAngle = Vector2.SignedAngle(
            Vector2.up,
            possessionDirection);
        int hitCount = Physics2D.OverlapBoxNonAlloc(
            searchCenter,
            searchSize,
            searchAngle,
            controlSwitchHits,
            controlSwitchLayers);

        ZeldaFourWayMover closestTarget = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = controlSwitchHits[i];
            if (hit == null)
            {
                continue;
            }
            ZeldaFourWayMover candidate =
                hit.GetComponentInParent<ZeldaFourWayMover>();
            if (!IsPotentialControlSwitchTarget(candidate))
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, candidate.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = candidate;
            }
        }

        // Also evaluate registered characters directly.  Physics overlap
        // queries can miss a solid non-Ghost target at contact distance when
        // collision separation, collider offsets, or a very narrow authored
        // sprite places the colliders just outside the query for one frame.
        // This projection uses the same configured forward length and the
        // controlled character's visual width, while including the target's
        // actual collider extents.
        foreach (ZeldaFourWayMover candidate in ZeldaRuntimeRegistry.Movers)
        {
            if (!IsPotentialControlSwitchTarget(candidate))
            {
                continue;
            }

            BoxCollider2D candidateCollider = candidate.bodyCollider != null
                ? candidate.bodyCollider
                : candidate.GetComponent<BoxCollider2D>();
            if (candidateCollider == null || !candidateCollider.enabled)
            {
                continue;
            }

            Vector2 origin = bodyCollider != null
                ? (Vector2)bodyCollider.bounds.center
                : rb.position;
            Vector2 targetCenter = candidateCollider.bounds.center;
            Vector2 offset = targetCenter - origin;
            Vector2 lateralDirection = new Vector2(
                -possessionDirection.y,
                possessionDirection.x);
            float targetForwardExtent =
                Mathf.Abs(possessionDirection.x) * candidateCollider.bounds.extents.x +
                Mathf.Abs(possessionDirection.y) * candidateCollider.bounds.extents.y;
            float targetLateralExtent =
                Mathf.Abs(lateralDirection.x) * candidateCollider.bounds.extents.x +
                Mathf.Abs(lateralDirection.y) * candidateCollider.bounds.extents.y;
            float forward = Vector2.Dot(offset, possessionDirection);
            float lateral = Mathf.Abs(Vector2.Dot(offset, lateralDirection));
            float maximumForward = controlSwitchDistance +
                controlSwitchRadius + targetForwardExtent;
            float maximumLateral = visualWidth * 0.5f +
                targetLateralExtent + collisionSkinWidth;
            if (forward < -targetForwardExtent ||
                forward > maximumForward ||
                lateral > maximumLateral)
            {
                continue;
            }

            float distance = offset.sqrMagnitude;
            if (distance < closestDistance * closestDistance)
            {
                closestDistance = Mathf.Sqrt(distance);
                closestTarget = candidate;
            }
        }

        return closestTarget;
    }

    private float GetPossessionAreaVisualWidth(Vector2 direction)
    {
        Vector2 visualSize = GetTightVisualWorldSize();
        float width = Mathf.Abs(direction.y) >= Mathf.Abs(direction.x)
            ? visualSize.x
            : visualSize.y;
        if (width <= 0.01f && bodyCollider != null)
        {
            width = Mathf.Abs(direction.y) >= Mathf.Abs(direction.x)
                ? bodyCollider.bounds.size.x
                : bodyCollider.bounds.size.y;
        }
        return Mathf.Max(0.1f, width);
    }

    private Vector2 GetTightVisualWorldSize()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return bodyCollider != null
                ? bodyCollider.bounds.size
                : Vector2.one * 0.5f;
        }

        Sprite sprite = spriteRenderer.sprite;
        Texture2D texture = sprite.texture;
        if (texture == null || !texture.isReadable)
        {
            // Several character visuals (Civilian, Prisoner, Noble,
            // Blacksmith and Behemoth) upload their generated pixels with
            // makeNoLongerReadable=true.  Querying those pixels throws before
            // possession prompting/input can run, so use the authored body
            // footprint as the tight gameplay width for those characters.
            return bodyCollider != null
                ? bodyCollider.bounds.size
                : Vector2.Scale(
                    sprite.bounds.size,
                    new Vector2(
                        Mathf.Abs(spriteRenderer.transform.lossyScale.x),
                        Mathf.Abs(spriteRenderer.transform.lossyScale.y)));
        }

        if (!possessionVisualSizeCache.TryGetValue(
                sprite,
                out Vector2 localSize))
        {
            localSize = sprite.bounds.size;
            try
            {
                Rect rect = sprite.rect;
                int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
                int originX = Mathf.RoundToInt(rect.x);
                int originY = Mathf.RoundToInt(rect.y);
                Color32[] pixels = texture.GetPixels32();
                int textureWidth = texture.width;
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int pixelIndex =
                            (originY + y) * textureWidth + originX + x;
                        if (pixels[pixelIndex].a <= 8)
                        {
                            continue;
                        }
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
                if (maxX >= minX && maxY >= minY)
                {
                    localSize = new Vector2(
                        (maxX - minX + 1) / sprite.pixelsPerUnit,
                        (maxY - minY + 1) / sprite.pixelsPerUnit);
                }
            }
            catch (Exception exception) when (
                exception is UnityException ||
                exception is ArgumentException)
            {
                // Invalid/unavailable pixel data falls back to the authored
                // sprite bounds instead of interrupting possession input.
            }
            possessionVisualSizeCache[sprite] = localSize;
        }

        Vector3 scale = spriteRenderer.transform.lossyScale;
        return new Vector2(
            localSize.x * Mathf.Abs(scale.x),
            localSize.y * Mathf.Abs(scale.y));
    }

    private bool IsValidControlSwitchTarget(ZeldaFourWayMover candidate)
    {
        if (!IsPotentialControlSwitchTarget(candidate))
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        if (characterData.MaxPossessionEnergy < candidateData.PossessionCost)
        {
            return false;
        }

        return true;
    }

    private bool IsPotentialControlSwitchTarget(ZeldaFourWayMover candidate)
    {
        if (candidate == null || candidate == this || candidate.enabled)
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        return candidateData != null && !candidateData.IsDead;
    }

    public ZeldaFourWayMover GetSoulMarkCandidate()
    {
        var candidate = FindControlSwitchCandidate();
        return IsValidControlSwitchTarget(candidate) ? candidate : null;
    }

    public GameObject SoulTransferFallbackPrefab => deathGhostPrefab;

    public void ReceiveControl(Vector2 inheritedFacingDirection)
    {
        DisableCharacterAi();
        facingDirection = inheritedFacingDirection.sqrMagnitude > 0f ? inheritedFacingDirection : Vector2.down;
        moveDirection = Vector2.zero;
        attackTimer = 0f;
        enabled = true;
        ApplyFacingVisual();
    }

    private void DisableCharacterAi()
    {
        ZeldaCharacterAiBase characterAi = GetComponent<ZeldaCharacterAiBase>();
        if (characterAi != null && characterAi.enabled)
        {
            characterAi.enabled = false;
        }
    }

    private void EnableCharacterAi()
    {
        ZeldaCharacterAiBase characterAi = GetComponent<ZeldaCharacterAiBase>();
        if (characterAi != null && !characterAi.enabled)
        {
            characterAi.enabled = true;
        }
    }

    public void OnCharacterDied(ZeldaCharacterData deadCharacterData)
    {
        if (!enabled || deadCharacterData != characterData || deathGhostPrefab == null)
        {
            return;
        }

        GameObject ghostObject = Instantiate(deathGhostPrefab, transform.position, Quaternion.identity);
        ghostObject.name = deathGhostPrefab.name;

        ZeldaFourWayMover ghostMover = ghostObject.GetComponent<ZeldaFourWayMover>();
        if (ghostMover != null)
        {
            ghostMover.ReceiveControl(facingDirection);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        controlSwitchDistance = Mathf.Max(0f, controlSwitchDistance);
        controlSwitchRadius = Mathf.Max(0.05f, controlSwitchRadius);
        controlSwitchHoldDuration = Mathf.Max(0.05f, controlSwitchHoldDuration);
        nonGhostPossessionDurationBonus =
            Mathf.Max(0f, nonGhostPossessionDurationBonus);
        controlSwitchProgressScale = Mathf.Max(0.1f, controlSwitchProgressScale);
        controlSwitchFailureDuration = Mathf.Max(0.1f, controlSwitchFailureDuration);
        controlSwitchFailureFlashCount = Mathf.Max(1, controlSwitchFailureFlashCount);
        sprintSpeedMultiplier = Mathf.Max(1f, sprintSpeedMultiplier);
        maximumStamina = Mathf.Max(0.1f, maximumStamina);
        staminaConsumptionPerSecond =
            Mathf.Max(0.01f, staminaConsumptionPerSecond);
        staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
        staminaRecoveryPerSecond =
            Mathf.Max(0.01f, staminaRecoveryPerSecond);
        exhaustedResumeThreshold =
            Mathf.Clamp01(exhaustedResumeThreshold);
        staminaProgressScale = Mathf.Max(0.1f, staminaProgressScale);
        if (deathGhostPrefab == null && GetComponent<GhostZeldaCharacterData>() == null)
        {
            deathGhostPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ghost.prefab");
        }
    }
#endif

}

/// <summary>Scene-local soul mark and delayed transfer, independent of the dying body.</summary>
public sealed class SoulMarkRuntime : MonoBehaviour
{
    private static SoulMarkRuntime instance;
    public static bool IsTransferring => instance != null && instance.transferring;
    public static bool IsVisionCovered => instance != null && instance.visionCovered;
    public static void ClearAfterDirectPossession(ZeldaFourWayMover target)
    {
        if (instance != null && !instance.transferring && instance.marked == target)
            instance.ClearMark();
    }
    public static void ClearForSceneTransition()
    {
        if (instance != null) Destroy(instance.gameObject);
    }
    private void OnEnable() => UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    private void OnDisable() => UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
    private void OnSceneChanged(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene next)
    {
        if (previous != next) ClearForSceneTransition();
    }
    private ZeldaFourWayMover source, marked;
    private SpriteRenderer marker;
    private CameraVisionStreamingExempt exemption;
    private bool transferring;
    private bool visionCovered;
    private float markerBlinkTime;

    public static void Use(ZeldaFourWayMover user)
    {
        if (user == null || IsTransferring || user.GetComponent<ZeldaCharacterData>().IsGhostForm) return;
        if (instance == null)
        {
            instance = new GameObject("Soul Mark Runtime").AddComponent<SoulMarkRuntime>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance.gameObject, ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject));
        }
        instance.ValidateMark();
        if (instance.marked != null)
        {
            if (user.GetComponent<GhostZeldaCharacterData>() != null) return;
            instance.StartCoroutine(instance.Transfer(user));
            return;
        }
        var candidate = user.GetSoulMarkCandidate();
        if (candidate == null) return;
        var data = user.GetComponent<ZeldaCharacterData>();
        int level = PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.SoulMarkLevel : 1;
        int energyCost = Mathf.Max(0, candidate.GetComponent<ZeldaCharacterData>().PossessionCost - (level >= 3 ? 1 : 0));
        if (!data.TrySpendPossessionEnergy(energyCost)) return;
        DominoSkillRuntime.RemoveMark(candidate.GetComponent<ZeldaCharacterData>());
        instance.source = user;
        instance.marked = candidate;
        // Own a separate exemption: a replaced mark removes its component at frame end.
        instance.exemption = candidate.gameObject.AddComponent<CameraVisionStreamingExempt>();
        instance.marker = new GameObject("Soul Mark Visual").AddComponent<SpriteRenderer>();
        instance.marker.sprite = SkillPageArt.GetSoulMarkWorldSprite();
        instance.marker.color = ZeldaUiPalette.Primary;
        instance.markerBlinkTime = 0f;
        instance.marker.transform.localScale = Vector3.one * 0.48f;
        instance.LateUpdate();
    }

    private void ValidateMark()
    {
        if (transferring || marked == null && source == null) return;
        if (marked == null ||
            marked.GetComponent<ZeldaCharacterData>().IsDead ||
            ZeldaRuntimeRegistry.GetGameplayScene(marked.gameObject) != gameObject.scene)
            ClearMark();
    }

    private void LateUpdate()
    {
        ValidateMark();
        if (marker == null || marked == null) return;
        markerBlinkTime = (markerBlinkTime + Time.deltaTime) % 1f;
        marker.color = markerBlinkTime < 0.5f ? ZeldaUiPalette.Primary : Color.white;
        var visual = marked.GetComponentInChildren<SpriteRenderer>();
        marker.transform.position = (visual != null ? visual.bounds.center : marked.transform.position)
            + Vector3.up * 0.15f;
        if (visual != null)
        {
            marker.sortingLayerID = visual.sortingLayerID;
            marker.sortingOrder = visual.sortingOrder + 5;
        }
    }

    private System.Collections.IEnumerator Transfer(ZeldaFourWayMover user)
    {
        // Reuse the ordinary bomb prefab's authored blast and investigation ranges.
        var bombPrefab = Resources.Load<BombPickupItem>("PickupItems/BombPickupItem");
        if (bombPrefab == null) { Debug.LogError("Soul mark requires the ordinary bomb pickup prefab."); yield break; }
        transferring = true;
        var destination = marked;
        var fallback = user.SoulTransferFallbackPrefab;
        Vector3 origin = user.transform.position;
        var data = user.GetComponent<ZeldaCharacterData>();
        var bomb = bombPrefab.CreateConfiguredPlacedBomb(origin, data);
        user.enabled = false; // Death notification must not spawn the source body's ghost.
        data.DieFromSoulDetonation();
        bomb.DetonateSoulMark(PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.SoulMarkLevel : 1);
        yield return new WaitForSeconds(0.5f);
        // Let the explosion remain visible before covering the actual control/camera handoff.
        visionCovered = true;
        if (destination != null && !destination.GetComponent<ZeldaCharacterData>().IsDead)
        {
            destination.gameObject.SetActive(true);
            destination.ReceiveControl(Vector2.down);
            var targetData = destination.GetComponent<ZeldaCharacterData>();
            PlayerGrowthAttributes.Instance?.RecordPossessedCharacter(targetData);
            targetData.ClearCurrentPossessionEnergy();
            QuestJournalManager.GetOrCreate().RecordControlledBehemoth(destination);
        }
        else if (fallback != null)
        {
            var ghost = Instantiate(fallback, origin, Quaternion.identity).GetComponent<ZeldaFourWayMover>();
            if (ghost != null) ghost.ReceiveControl(Vector2.down);
        }
        var controlled = ZeldaRuntimeRegistry.GetControlledMover();
        var camera = Camera.main;
        if (camera != null && controlled != null)
        {
            var follow = camera.GetComponent<CameraFollowActiveZeldaMover>();
            if (follow != null) follow.BeginSoulTransferFollow(controlled);
            var streaming = camera.GetComponent<CameraVisionObjectStreaming>();
            if (streaming != null) streaming.PrepareSoulTransferView();
        }
        // Keep the curtain through camera/streaming/vision LateUpdates before reopening it.
        yield return null;
        yield return new WaitForEndOfFrame();
        ClearMark();
        visionCovered = false;
        transferring = false;
    }

    private void ClearMark()
    {
        if (marker != null) Destroy(marker.gameObject);
        if (exemption != null) Destroy(exemption);
        marker = null; exemption = null; marked = null; source = null;
    }
    private void OnDestroy()
    {
        ClearMark();
        if (instance == this) instance = null;
    }
}
