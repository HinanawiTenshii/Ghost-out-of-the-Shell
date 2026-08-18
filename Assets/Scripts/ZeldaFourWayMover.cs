using System;
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
    [SerializeField] private float controlSwitchDistance = 1.1f;
    [SerializeField] private float controlSwitchRadius = 0.45f;
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
    [SerializeField, Min(1f)] private float sprintSpeedMultiplier = 1.55f;
    [SerializeField, Min(0.1f)] private float maximumStamina = 9f;
    [SerializeField, Min(0.01f)] private float staminaConsumptionPerSecond = 1f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1.25f;
    [SerializeField, Min(0.01f)] private float staminaRecoveryPerSecond = 1.4f;
    [SerializeField, Range(0f, 1f)] private float exhaustedResumeThreshold = 0.25f;
    [SerializeField] private Vector2 staminaProgressOffset = new Vector2(0f, 1.25f);
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
    private readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];

    public Vector2 FacingDirection => facingDirection;
    public bool IsMoving => moveDirection.sqrMagnitude > 0f;
    public bool IsAttacking => attackTimer > 0f;
    public bool IsSprinting => isSprinting;
    public float CurrentStamina => currentStamina;
    public float StaminaNormalized =>
        maximumStamina > 0f
            ? Mathf.Clamp01(currentStamina / maximumStamina)
            : 0f;
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
        StopSprintingAndHideBar();
    }

    private void OnDisable()
    {
        ZeldaRuntimeRegistry.NotifyMoverDisabled(this);
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
    }

    private void UpdatePossessionPrompt()
    {
        bool failureVisible = possessionAttemptRejected ||
            (possessionProgressBar != null && possessionProgressBar.IsFailureActive);
        suppressPossessionPromptFromArbiter = IsPossessionInProgress ||
            Input.GetKey(KeyCode.F) || failureVisible;
        ZeldaFourWayMover candidate = failureVisible
            ? null
            : (pendingControlTarget != null
                ? pendingControlTarget
                : FindControlSwitchCandidate());
        bool canPossess = !DocumentReader.IsInputBlocked
            && characterData != null
            && !characterData.IsDead
            && IsValidControlSwitchTarget(candidate);

        if (!canPossess)
        {
            SetPossessionPromptVisible(false);
            return;
        }

        EnsurePossessionPrompt();
        if (possessionPromptObject == null)
            return;

        possessionPromptObject.transform.position =
            transform.position + (Vector3)possessionPromptOffset;
        possessionPromptObject.transform.rotation = Quaternion.identity;
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
        if (DocumentReader.IsInputBlocked)
        {
            moveDirection = Vector2.zero;
            StopSprintingAndHideBar();
            ResetControlSwitchProgress();
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
            StopSprintingAndHideBar();
            return;
        }

        if (characterData.CanAttack && Input.GetMouseButtonDown(0) && !IsAttacking)
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
            (isSprinting ? Mathf.Max(1f, sprintSpeedMultiplier) : 1f);
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
        characterData.ApplyCharacterVisual(spriteRenderer, facingDirection, IsMoving, IsAttacking);
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
        if (characterData == null || characterData.IsDead || !characterData.CanAttack || IsAttacking)
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

        ZeldaAttackHitbox hitbox = attackObject.GetComponent<ZeldaAttackHitbox>();
        if (hitbox != null)
        {
            hitbox.Configure(
                characterData.AttackDuration,
                characterData.AttackSize,
                characterData.AttackVisualTint,
                characterData.FinalAttackPower,
                characterData,
                characterData.AttackVisualShape);
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
        if (!IsValidControlSwitchTarget(target))
        {
            RejectPossessionAttempt();
            return false;
        }

        if (!ZeldaInteractionArbiter.IsSelected(
                target,
                this,
                KeyCode.F))
        {
            ResetControlSwitchProgress();
            possessionAttemptRejected = false;
            return false;
        }

        if (target != pendingControlTarget)
        {
            ReleasePendingControlTarget();
            pendingControlTarget = target;
            controlSwitchHoldTimer = 0f;

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
            possessionProgressBar.SetProgress(progress);
        }

        if (controlSwitchHoldTimer < activePossessionDuration)
        {
            return false;
        }

        ZeldaFourWayMover completedTarget = pendingControlTarget;
        ResetControlSwitchProgress();
        return TrySwitchControl(completedTarget);
    }

    private float GetActivePossessionDuration()
    {
        float duration = Mathf.Max(0.05f, controlSwitchHoldDuration);
        if (!(characterData is GhostZeldaCharacterData))
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
        if (!characterData.TrySpendPossessionEnergy(targetData.PossessionCost))
        {
            RejectPossessionAttempt();
            return false;
        }

        target.ReceiveControl(facingDirection);
        NotifyAiWitnessesOfPossession(target);
        moveDirection = Vector2.zero;
        attackTimer = 0f;
        ApplyFacingVisual();
        enabled = false;

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
        progressObject.transform.localPosition = controlSwitchProgressOffset;
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
        progressObject.transform.localPosition = staminaProgressOffset;
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
                staminaProgressBar.transform.localPosition =
                    staminaProgressOffset;
                staminaProgressBar.transform.localRotation =
                    Quaternion.identity;
                staminaProgressBar.transform.localScale =
                    Vector3.one * Mathf.Max(0.1f, staminaProgressScale);
                staminaProgressBar.SetProgress(
                    StaminaNormalized,
                    staminaProgressColor);
            }
            return;
        }

        isSprinting = false;
        if (staminaProgressBar != null)
        {
            staminaProgressBar.Hide();
        }

        if (isGhost)
        {
            currentStamina = Mathf.Max(0.1f, maximumStamina);
            staminaRecoveryTimer = 0f;
            staminaExhausted = false;
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
        ReleasePendingControlTarget();
        pendingControlTarget = null;
        controlSwitchHoldTimer = 0f;
        if (possessionProgressBar != null)
        {
            possessionProgressBar.HideProgress();
            possessionProgressBar.transform.localPosition = controlSwitchProgressOffset;
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
        Vector2 searchCenter = rb.position + facingDirection * controlSwitchDistance;
        Collider2D[] hits = Physics2D.OverlapCircleAll(searchCenter, controlSwitchRadius, controlSwitchLayers);

        ZeldaFourWayMover closestTarget = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            ZeldaFourWayMover candidate = hits[i].GetComponentInParent<ZeldaFourWayMover>();
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

        return closestTarget;
    }

    private bool IsValidControlSwitchTarget(ZeldaFourWayMover candidate)
    {
        if (!IsPotentialControlSwitchTarget(candidate))
        {
            return false;
        }

        ZeldaCharacterData candidateData = candidate.GetComponent<ZeldaCharacterData>();
        if (characterData.FinalPossessionEnergy < candidateData.PossessionCost)
        {
            return false;
        }

        ZeldaCharacterAiBase candidateAi = candidate.GetComponent<ZeldaCharacterAiBase>();
        return candidateAi != null && candidateAi.isActiveAndEnabled &&
            candidateAi.IsInPossessableState;
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

    private void ReceiveControl(Vector2 inheritedFacingDirection)
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
