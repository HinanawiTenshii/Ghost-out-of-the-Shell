using System.Collections.Generic;
using UnityEngine;

/// <summary>World behaviour for a deployed clockwork puppet.</summary>
public sealed class ClockworkPuppetRuntime : MonoBehaviour
{
    private static readonly HashSet<ClockworkPuppetRuntime> ActiveSet =
        new HashSet<ClockworkPuppetRuntime>();

    private LeverData targetLever;
    private Rigidbody2D physicsBody;
    private Collider2D bodyCollider;
    private ZeldaReusableGridNavigator navigator;
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
    private readonly List<Collider2D> ignoredLeverColliders = new List<Collider2D>();
    private Transform initialOwner;
    private SpriteRenderer bodyRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;
    private int lastVisibleEnergySegments = -1;
    private float magic;
    private float operationTimer;
    private bool attached;
    private bool broken;
    private string itemId;
    private string itemInstanceId;
    private string itemName;
    private string itemDescription;
    private bool hasVisualColor;
    private Color visualColor;
    private int maxStackSize;
    private float maximumMagic;
    private float moveSpeed;
    private float movementMagicPerSecond;
    private float operationMagicCost;
    private float operationInterval;
    private float leverContactDistance;
    private float leverSearchRadius;
    private float reclaimDistance;
    private float lockedDoorPryDistance;
    private GameObject reclaimPromptObject;
    private TextMesh reclaimPromptText;
    private Font reclaimPromptFont;
    private Material reclaimPromptMaterial;

    public LeverData TargetLever => targetLever;
    public float RemainingMagic => magic;
    public bool IsBroken => broken;
    public static IReadOnlyCollection<ClockworkPuppetRuntime> ActivePuppets =>
        ActiveSet;

    public bool DoesCurrentPathIntersect(
        Collider2D targetCollider,
        float lookAheadDistance)
    {
        return !broken && navigator != null &&
            navigator.DoesCurrentPathIntersect(
                targetCollider,
                lookAheadDistance);
    }

    private void OnEnable()
    {
        ActiveSet.Add(this);
    }

    private void OnDisable()
    {
        ActiveSet.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveSet.Clear();
    }

    public void SetInventoryIdentity(
        string configuredInstanceId,
        string configuredItemName,
        string configuredDescription,
        bool configuredHasVisualColor,
        Color configuredVisualColor)
    {
        if (!string.IsNullOrWhiteSpace(configuredInstanceId))
        {
            itemInstanceId = configuredInstanceId.Trim();
        }
        itemName = configuredItemName ?? string.Empty;
        itemDescription = configuredDescription ?? string.Empty;
        hasVisualColor = configuredHasVisualColor;
        visualColor = configuredHasVisualColor
            ? configuredVisualColor
            : Color.white;
    }

    public void Configure(
        ClockworkPuppetPickupItem configuredSource,
        ZeldaCharacterData deployingCharacter,
        LeverData preferredLever = null,
        float initialMagic = -1f)
    {
        itemId = configuredSource != null
            ? configuredSource.ItemId
            : ClockworkPuppetPickupItem.DefaultItemId;
        itemInstanceId = configuredSource != null
            ? configuredSource.UniqueInstanceId
            : System.Guid.NewGuid().ToString("N");
        itemName = configuredSource != null
            ? configuredSource.ItemName
            : string.Empty;
        itemDescription = configuredSource != null
            ? configuredSource.ItemDescription
            : string.Empty;
        hasVisualColor = configuredSource != null &&
            configuredSource.ItemVisual != null;
        visualColor = hasVisualColor
            ? configuredSource.ItemVisual.DisplayColor
            : Color.white;
        maxStackSize = configuredSource != null ? configuredSource.MaxStackSize : 1;
        maximumMagic = configuredSource != null ? configuredSource.MaximumMagic : 1f;
        moveSpeed = configuredSource != null ? configuredSource.MoveSpeed : 1.35f;
        movementMagicPerSecond = configuredSource != null ? configuredSource.MovementMagicPerSecond : 0.45f;
        operationMagicCost = configuredSource != null ? configuredSource.OperationMagicCost : 1f;
        operationInterval = configuredSource != null ? configuredSource.OperationInterval : 3f;
        leverContactDistance = configuredSource != null ? configuredSource.LeverContactDistance : 0.18f;
        leverSearchRadius = configuredSource != null
            ? configuredSource.LeverSearchRadius
            : 0f;
        reclaimDistance = configuredSource != null ? configuredSource.ReclaimDistance : 1.15f;
        lockedDoorPryDistance = configuredSource != null ? configuredSource.LockedDoorPryDistance : 0.7f;
        magic = initialMagic >= 0f
            ? Mathf.Clamp(initialMagic, 0f, maximumMagic)
            : maximumMagic;
        transform.localScale = Vector3.one * (configuredSource != null ? configuredSource.DeployedScale : 0.58f);
        physicsBody = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        navigator = GetComponent<ZeldaReusableGridNavigator>();
        if (navigator == null) navigator = gameObject.AddComponent<ZeldaReusableGridNavigator>();
        navigator.Configure(physicsBody, bodyCollider);
        navigator.ConfigureNavigationDebug(
            configuredSource != null && configuredSource.ShowNavigationDebug,
            configuredSource != null
                ? configuredSource.NavigationPathColor
                : new Color(0.15f, 0.85f, 1f, 0.9f),
            configuredSource != null
                ? configuredSource.NavigationTargetColor
                : new Color(1f, 0.25f, 0.75f, 1f),
            configuredSource != null
                ? configuredSource.NavigationPathWidth
                : 0.035f,
            configuredSource != null
                ? configuredSource.NavigationTargetMarkerSize
                : 0.18f,
            configuredSource != null
                ? configuredSource.NavigationDebugSortingOrder
                : 200);
        CameraVisionRevealSource revealSource =
            GetComponent<CameraVisionRevealSource>();
        if (revealSource == null)
        {
            revealSource = gameObject.AddComponent<CameraVisionRevealSource>();
        }
        revealSource.Configure(
            configuredSource != null
                ? configuredSource.VisionRevealRadius
                : 3f);
        if (GetComponent<CameraVisionStreamingExempt>() == null)
        {
            gameObject.AddComponent<CameraVisionStreamingExempt>();
        }
        IgnoreDeployingCharacterTemporarily(deployingCharacter);
        BuildVisual();
        if (magic <= 0f)
        {
            BreakPuppet();
            return;
        }
        AssignTargetLever(preferredLever != null
            ? preferredLever
            : FindNearestLever(transform.position, leverSearchRadius));
        operationTimer = 0f;
    }

    private void Update()
    {
        if (broken || DocumentReader.IsInputBlocked)
        {
            SetReclaimPromptVisible(false);
            return;
        }

        TryOfferReclaim();
    }

    private void FixedUpdate()
    {
        if (broken || DocumentReader.IsInputBlocked)
        {
            if (physicsBody != null) physicsBody.velocity = Vector2.zero;
            return;
        }

        RestoreOwnerCollisionWhenClear();
        if (TryPryNearestLockedDoor())
        {
            return;
        }

        if (targetLever == null || !targetLever.isActiveAndEnabled)
        {
            AssignTargetLever(FindNearestLever(
                transform.position,
                leverSearchRadius));
            attached = false;
        }
        if (targetLever == null)
        {
            return;
        }

        Vector2 mountPoint = targetLever.PuppetMountPoint;
        float distance = Vector2.Distance(transform.position, mountPoint);
        if (distance > leverContactDistance)
        {
            attached = false;
            if (physicsBody != null) physicsBody.SetRotation(0f);
            Vector2 direction = navigator != null
                ? navigator.GetDirection(mountPoint, leverContactDistance)
                : (mountPoint - (Vector2)transform.position).normalized;
            Vector2 displacement = navigator != null
                ? navigator.GetSafeDisplacement(direction, moveSpeed * Time.fixedDeltaTime)
                : direction * moveSpeed * Time.fixedDeltaTime;
            if (physicsBody != null)
                physicsBody.MovePosition(physicsBody.position + displacement);
            else
                transform.position += (Vector3)displacement;
            if (displacement.sqrMagnitude > 0.000001f)
                SpendMagic(movementMagicPerSecond * Time.fixedDeltaTime);
            return;
        }

        if (physicsBody != null)
        {
            physicsBody.velocity = Vector2.zero;
            physicsBody.position = mountPoint;
        }
        else transform.position = mountPoint;
        Vector2 stem = targetLever.StemDirection;
        float stemAngle = Vector2.SignedAngle(Vector2.up, stem);
        if (physicsBody != null) physicsBody.SetRotation(stemAngle);
        else transform.rotation = Quaternion.Euler(0f, 0f, stemAngle);
        if (!attached)
        {
            attached = true;
            operationTimer = 0f;
        }

        operationTimer -= Time.fixedDeltaTime;
        if (operationTimer <= 0f)
        {
            targetLever.ActivateFromClockworkPuppet();
            operationTimer = operationInterval;
            SpendMagic(operationMagicCost);
            if (!broken && physicsBody != null)
                physicsBody.position = targetLever.PuppetMountPoint;
        }
    }

    private void AssignTargetLever(LeverData nextTarget)
    {
        SetLeverCollisionIgnored(false);
        targetLever = nextTarget;
        if (navigator != null)
        {
            navigator.SetIgnoredTarget(targetLever);
            navigator.InvalidatePath();
        }
        SetLeverCollisionIgnored(true);
    }

    private void SetLeverCollisionIgnored(bool ignored)
    {
        if (!ignored)
        {
            for (int i = 0; i < ignoredLeverColliders.Count; i++)
                if (bodyCollider != null && ignoredLeverColliders[i] != null)
                    Physics2D.IgnoreCollision(bodyCollider, ignoredLeverColliders[i], false);
            ignoredLeverColliders.Clear();
            return;
        }
        if (targetLever == null || bodyCollider == null) return;
        Collider2D[] colliders = targetLever.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == bodyCollider) continue;
            Physics2D.IgnoreCollision(bodyCollider, colliders[i], true);
            ignoredLeverColliders.Add(colliders[i]);
        }
    }

    private void IgnoreDeployingCharacterTemporarily(ZeldaCharacterData character)
    {
        if (character == null || bodyCollider == null) return;
        initialOwner = character.transform;
        Collider2D[] colliders = character.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == bodyCollider) continue;
            Physics2D.IgnoreCollision(bodyCollider, colliders[i], true);
            ignoredOwnerColliders.Add(colliders[i]);
        }
    }

    private void RestoreOwnerCollisionWhenClear()
    {
        if (initialOwner == null || ignoredOwnerColliders.Count == 0 ||
            Vector2.Distance(transform.position, initialOwner.position) < 0.8f) return;
        for (int i = 0; i < ignoredOwnerColliders.Count; i++)
            if (bodyCollider != null && ignoredOwnerColliders[i] != null)
                Physics2D.IgnoreCollision(bodyCollider, ignoredOwnerColliders[i], false);
        ignoredOwnerColliders.Clear();
        initialOwner = null;
        if (navigator != null) navigator.InvalidatePath();
    }

    private void TryOfferReclaim()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || Vector2.Distance(mover.transform.position, transform.position) > reclaimDistance)
        {
            SetReclaimPromptVisible(false);
            return;
        }
        EnsureReclaimPrompt();
        if (reclaimPromptObject != null)
        {
            reclaimPromptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, 1.05f));
            reclaimPromptObject.transform.rotation = Quaternion.identity;
            ZeldaInteractionArbiter.OfferInteraction(
                this,
                mover,
                KeyCode.E,
                transform.position,
                SetReclaimPromptVisible);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, Reclaim);
        }
    }

    private void EnsureReclaimPrompt()
    {
        if (reclaimPromptObject != null) return;
        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        reclaimPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (reclaimPromptFont == null) return;
        const string message = "按[E]收回人偶";
        reclaimPromptFont.RequestCharactersInTexture(message, 72, FontStyle.Normal);
        reclaimPromptObject = new GameObject("Clockwork Puppet Reclaim Prompt");
        reclaimPromptText = reclaimPromptObject.AddComponent<TextMesh>();
        reclaimPromptText.text = message;
        reclaimPromptText.font = reclaimPromptFont;
        reclaimPromptText.fontSize = 72;
        reclaimPromptText.characterSize = 0.035f;
        reclaimPromptText.anchor = TextAnchor.MiddleCenter;
        reclaimPromptText.alignment = TextAlignment.Center;
        reclaimPromptText.color = ZeldaUiPalette.Primary;
        MeshRenderer renderer = reclaimPromptObject.GetComponent<MeshRenderer>();
        renderer.sortingOrder = short.MaxValue - 2;
        reclaimPromptMaterial = new Material(reclaimPromptFont.material)
        {
            name = "Clockwork Puppet Reclaim Prompt Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        reclaimPromptMaterial.mainTexture = reclaimPromptFont.material.mainTexture;
        renderer.sharedMaterial = reclaimPromptMaterial;
        reclaimPromptObject.SetActive(false);
    }

    private void SetReclaimPromptVisible(bool visible)
    {
        if (reclaimPromptObject != null && reclaimPromptObject.activeSelf != visible)
        {
            reclaimPromptObject.SetActive(visible);
        }
    }

    private void Reclaim()
    {
        if (broken || PersistentInventory.Instance == null)
        {
            return;
        }
        if (PersistentInventory.Instance.TryAddUniqueItem(
                itemId,
                itemInstanceId,
                1,
                maxStackSize,
                itemName,
                itemDescription,
                hasVisualColor,
                visualColor,
                true,
                magic))
        {
            Destroy(gameObject);
        }
    }

    private bool TryPryNearestLockedDoor()
    {
        DoorHingeInteraction closest = null;
        float best = lockedDoorPryDistance * lockedDoorPryDistance;
        foreach (DoorHingeInteraction door in DoorHingeInteraction.WorldDoors)
        {
            if (door == null || !door.isActiveAndEnabled || !door.IsLocked)
            {
                continue;
            }
            float sqr = ((Vector2)door.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                closest = door;
            }
        }
        if (closest == null)
        {
            return false;
        }
        closest.SetLocked(false);
        magic = 0f;
        BreakPuppet();
        return true;
    }

    private void SpendMagic(float amount)
    {
        magic = Mathf.Max(0f, magic - Mathf.Max(0f, amount));
        UpdateMagicVisual();
        if (magic <= 0f)
        {
            BreakPuppet();
        }
    }

    private void BreakPuppet()
    {
        if (broken)
        {
            return;
        }
        broken = true;
        if (physicsBody != null) physicsBody.velocity = Vector2.zero;
        SetReclaimPromptVisible(false);
        attached = false;
        transform.rotation = Quaternion.identity;
        UpdateMagicVisual();
        // An exhausted mechanism is no longer a reclaimable world object.
        // Destroying the runtime root also removes its collider, navigator,
        // prompt and generated visual resources through OnDestroy.
        Destroy(gameObject);
    }

    private static LeverData FindNearestLever(
        Vector2 position,
        float searchRadius)
    {
        LeverData closest = null;
        float best = searchRadius > 0f
            ? searchRadius * searchRadius
            : float.MaxValue;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled)
            {
                continue;
            }
            float sqr = (lever.PuppetMountPoint - position).sqrMagnitude;
            if (sqr <= best)
            {
                best = sqr;
                closest = lever;
            }
        }
        return closest;
    }

    private void BuildVisual()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();
        runtimeSprite = ClockworkPuppetPickupItemVisual.CreateRuntimeSprite(out runtimeTexture);
        bodyRenderer.sprite = runtimeSprite;
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = 20;
        UpdateMagicVisual();
    }

    private void UpdateMagicVisual()
    {
        if (runtimeTexture == null)
        {
            return;
        }
        float normalized = Mathf.Clamp01(magic / maximumMagic);
        int visibleSegments = Mathf.Clamp(Mathf.CeilToInt(normalized * 6f), 0, 6);
        if (visibleSegments == lastVisibleEnergySegments) return;
        lastVisibleEnergySegments = visibleSegments;
        ClockworkPuppetPickupItemVisual.ApplyRuntimeEnergy(runtimeTexture, normalized);
    }

    private void OnDestroy()
    {
        ActiveSet.Remove(this);
        SetLeverCollisionIgnored(false);
        for (int i = 0; i < ignoredOwnerColliders.Count; i++)
            if (bodyCollider != null && ignoredOwnerColliders[i] != null)
                Physics2D.IgnoreCollision(bodyCollider, ignoredOwnerColliders[i], false);
        if (reclaimPromptObject != null) Destroy(reclaimPromptObject);
        if (reclaimPromptMaterial != null) Destroy(reclaimPromptMaterial);
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (runtimeTexture != null) Destroy(runtimeTexture);
    }
}
