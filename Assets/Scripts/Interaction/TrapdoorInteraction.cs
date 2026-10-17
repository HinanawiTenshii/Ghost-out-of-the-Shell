using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Password-locked, persistent two-way-capable passage. Each end configures its own exit.</summary>
[DisallowMultipleComponent, AddComponentMenu("Interaction/Trapdoor (活板门)")]
public sealed class TrapdoorInteraction : MonoBehaviour
{
    public enum TravelMode { SameScene, CrossScene }
    [Header("连接 / Connection")]
    [SerializeField] private TravelMode travelMode;
    [SerializeField, Tooltip("同场景模式：直接拖入目的活板门。往返使用时，两端分别绑定。")]
    private TrapdoorInteraction localTarget;
    [SerializeField, Tooltip("本门的跨场景定位资源，每个目标活板门使用独立资源；不跨场景可留空。")]
    private TrapdoorDestination ownEndpoint;
    [SerializeField, Tooltip("跨场景模式：填入目的活板门的定位资源。")]
    private TrapdoorDestination crossSceneTarget;
    [SerializeField, Tooltip("从本门到达时的局部偏移，默认落在门中心。")]
    private Vector2 arrivalOffset;
    [Header("密码锁 / Password")]
    [SerializeField] private bool isLocked;
    [SerializeField] private string password = "1111";
    [Header("交互 / Interaction")]
    [SerializeField, Min(.1f)] private float interactionDistance = 1.75f;
    [SerializeField] private Font promptFont;
    private DoorPasswordPanel passwordPanel;
    private TextMesh prompt;
    private Material promptMaterial;
    private ZeldaFourWayMover passwordUser;
    private int suppressInputFrame = -1;
    public bool IsLocked => isLocked;
    public TravelMode Mode => travelMode;
    public TrapdoorDestination OwnEndpoint => ownEndpoint;
    public Vector2 ArrivalPosition => transform.TransformPoint(arrivalOffset);
    private bool InputBlocked => DocumentReader.IsInputBlocked || Time.timeScale <= 0f ||
        CharacterPotionBuffs.IsRestoringWorld || ClockworkPuppetRuntime.BlocksCharacterInput;

    private ZeldaFourWayMover Nearby()
    {
        if (ArenaChallengeController.BlocksTransition(this)) return null;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled || mover.CharacterData == null ||
            mover.CharacterData.IsDead || !mover.CharacterData.CanInteractWithDoors ||
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene ||
            ((Vector2)(mover.transform.position - transform.position)).sqrMagnitude > interactionDistance * interactionDistance)
            return null;
        // A nearby hatch on the other side of a wall is not reachable.
        if (Physics2D.Linecast(mover.transform.position, transform.position, LayerMask.GetMask("Blocks")).collider != null)
            return null;
        return mover;
    }
    private void Update()
    {
        if (InputBlocked || Time.frameCount <= suppressInputFrame || !Input.GetKeyDown(KeyCode.E)) return;
        var mover = Nearby();
        if (mover != null) ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, mover.transform.position, () => TryInteract());
    }
    public bool TryInteract()
    {
        if (!isActiveAndEnabled || InputBlocked) return false;
        var mover = Nearby();
        if (mover == null || mover.IsAttacking) return false;
        if (!ValidateDestination(out string reason)) { Notify(reason); return false; }
        suppressInputFrame = Time.frameCount;
        if (isLocked)
        {
            if (!CharacterSkillRequirements.Require(mover.CharacterData, CharacterSkillRequirements.DoorUnlock)) return false;
            if (passwordPanel == null) passwordPanel = GetComponent<DoorPasswordPanel>() ?? gameObject.AddComponent<DoorPasswordPanel>();
            passwordUser = mover;
            passwordPanel.Open(promptFont, password, OnPasswordSubmitted);
            ShowPrompt(false);
            return DoorPasswordPanel.IsOpen;
        }
        return Travel(mover);
    }
    private void OnPasswordSubmitted(bool correct)
    {
        suppressInputFrame = Time.frameCount;
        var mover = passwordUser;
        passwordUser = null;
        if (!isActiveAndEnabled || mover == null || mover != Nearby() || InputBlocked) return;
        if (!correct) { Notify("密码错误"); return; }
        if (!CharacterSkillRequirements.Require(mover.CharacterData, CharacterSkillRequirements.DoorUnlock)) return;
        isLocked = false;
        Travel(mover);
    }
    public void ApplyPersistentState(bool locked)
    {
        if (passwordPanel != null) passwordPanel.CloseWithoutSubmission();
        passwordUser = null;
        isLocked = locked;
    }
    private bool ValidateDestination(out string reason)
    {
        reason = null;
        if (travelMode == TravelMode.SameScene)
        {
            if (localTarget == null || localTarget == this || !localTarget.enabled || localTarget.gameObject.scene != gameObject.scene)
                reason = "请先绑定同场景中的另一扇活板门";
        }
        else if (crossSceneTarget == null || string.IsNullOrWhiteSpace(crossSceneTarget.SceneName))
            reason = "活板门尚未配置跨场景目标";
        else if (!Application.CanStreamedLevelBeLoaded(crossSceneTarget.SceneName))
            reason = "活板门的目标场景未加入构建列表";
        else if (crossSceneTarget.SceneName == gameObject.scene.name &&
            (!TryFindDestination(gameObject.scene, crossSceneTarget, out var target) || target == this))
            reason = "目标活板门不存在或定位资源重复";
        return reason == null;
    }
    private bool Travel(ZeldaFourWayMover mover)
    {
        if (!ValidateDestination(out string reason)) { Notify(reason); return false; }
        ShowPrompt(false);
        if (travelMode == TravelMode.SameScene || crossSceneTarget.SceneName == gameObject.scene.name)
        {
            var target = localTarget;
            if (travelMode == TravelMode.CrossScene) TryFindDestination(gameObject.scene, crossSceneTarget, out target);
            return RetroSceneLoadReveal.BeginLocalTransition(() => {
                if (mover == null || mover != ZeldaRuntimeRegistry.GetControlledMover() || mover.CharacterData.IsDead) return;
                if (target == null) { Notify("目标活板门已不存在"); return; }
                CameraVisionObjectStreaming.RestoreForGameplay(target.gameObject);
                PlaceTraveler(mover, target.ArrivalPosition);
            });
        }
        return SceneTravelStateManager.GetOrCreate().TravelToTrapdoor(crossSceneTarget);
    }
    internal static bool TryFindDestination(Scene scene, TrapdoorDestination identity, out TrapdoorInteraction target)
    {
        target = null;
        if (identity == null || !scene.IsValid() || !scene.isLoaded) return false;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var candidate in root.GetComponentsInChildren<TrapdoorInteraction>(true))
            if (candidate.ownEndpoint == identity && candidate.enabled)
            {
                if (target != null) { target = null; return false; }
                target = candidate;
            }
        return target != null;
    }
    internal static Vector2 ResolveArrival(Scene scene, TrapdoorDestination identity)
    {
        if (TryFindDestination(scene, identity, out var target))
        {
            CameraVisionObjectStreaming.RestoreForGameplay(target.gameObject);
            return target.ArrivalPosition;
        }
        // Misconfigured/removed targets must never strand the player behind a black screen.
        Debug.LogWarning("目标活板门缺失或定位重复，使用定位资源最后同步的位置：" + identity.name);
        Notify("目标活板门定位异常，已到达备用出口位置");
        return identity.FallbackPosition;
    }
    internal static void PlaceTraveler(ZeldaFourWayMover mover, Vector2 position)
    {
        mover.CancelAttackForStun();
        var body = mover.GetComponent<Rigidbody2D>();
        if (body != null) { body.velocity = Vector2.zero; body.angularVelocity = 0f; body.position = position; }
        mover.transform.position = new Vector3(position.x, position.y, mover.transform.position.z);
        Physics2D.SyncTransforms();
        // One-off local retention: preload the destination without waking the whole level.
        float radius = 4f;
        foreach (var follow in Object.FindObjectsOfType<CameraFollowActiveZeldaMover>())
        {
            follow.SnapToControlledCharacter();
            var camera = follow.GetComponent<Camera>();
            radius = Mathf.Max(radius, camera.orthographicSize * Mathf.Sqrt(1f + camera.aspect * camera.aspect) + 2f);
            follow.GetComponent<CameraCircularVision>()?.RefreshSceneCameraSettings();
        }
        var preload = new GameObject("Trapdoor Arrival Preload") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(preload, SceneManager.GetActiveScene());
        preload.transform.position = position;
        preload.AddComponent<CameraVisionStreamingRegion>().Configure(radius);
        Destroy(preload, 1.5f);
        ActiveZeldaCharacterDataSource.Instance?.Refresh();
    }
    private static void Notify(string message) => ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(message);
    private void LateUpdate()
    {
        var mover = InputBlocked ? null : Nearby();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, mover.transform.position, ShowPrompt);
        EnsurePrompt();
        if (prompt == null) return;
        string message = isLocked ? (CharacterSkillRequirements.Has(mover.CharacterData, CharacterSkillRequirements.DoorUnlock)
            ? "按[E]输入密码" : "能力值不足") : "按[E]进入活板门";
        if (prompt.text != message) { prompt.text = message; prompt.font.RequestCharactersInTexture(message, 72, FontStyle.Normal); }
        promptMaterial.mainTexture = prompt.font.material.mainTexture;
        prompt.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, .9f));
        prompt.transform.rotation = Quaternion.identity;
    }
    private void EnsurePrompt()
    {
        if (prompt != null) return;
        var font = promptFont != null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (font == null) return;
        var go = new GameObject("Trapdoor Interaction Prompt") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        prompt = go.AddComponent<TextMesh>(); prompt.font = font; prompt.fontSize = 72; prompt.characterSize = .035f;
        prompt.anchor = TextAnchor.MiddleCenter; prompt.alignment = TextAlignment.Center; prompt.color = ZeldaUiPalette.Primary;
        promptMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = promptMaterial;
        int highest = int.MinValue;
        foreach (var layer in SortingLayer.layers) if (layer.value > highest) { highest = layer.value; renderer.sortingLayerID = layer.id; }
        renderer.sortingOrder = short.MaxValue - 2; ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        go.SetActive(false);
    }
    private void ShowPrompt(bool visible) { if (prompt != null) prompt.gameObject.SetActive(visible); }
    private void OnDisable()
    {
        ShowPrompt(false);
        if (passwordPanel != null) passwordPanel.CloseWithoutSubmission();
        passwordUser = null;
    }
    private void OnDestroy() { if (prompt != null) Destroy(prompt.gameObject); if (promptMaterial != null) Destroy(promptMaterial); }
    private void OnValidate() { password = DoorPasswordPanel.NormalizePassword(password); interactionDistance = Mathf.Max(.1f, interactionDistance); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, interactionDistance);
        Gizmos.DrawWireSphere(ArrivalPosition, .15f);
        if (travelMode == TravelMode.SameScene && localTarget != null) Gizmos.DrawLine(transform.position, localTarget.ArrivalPosition);
    }
}
