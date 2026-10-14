using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>Optional, scene-local dialogue authoring. Attach to the character root.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-300)]
[RequireComponent(typeof(ZeldaCharacterData), typeof(ZeldaFourWayMover))]
[AddComponentMenu("Interaction/Character Dialogue (角色对话)")]
public sealed class CharacterDialogue : MonoBehaviour
{
    public enum ChoiceAction { Continue, Exit }

    [Serializable] public sealed class Choice
    {
        public string text = "再见。";
        public ChoiceAction action = ChoiceAction.Exit;
        public string nextNodeId = "";
        public DialogueOptionEffect effect;
        public UnityEvent onSelected = new UnityEvent();
    }

    [Serializable] public sealed class Node
    {
        public string id = "start";
        [TextArea(3, 12)] public string text = "你好。";
        [TextArea(3, 12), Tooltip("仅当本段作为开场且本次角色与上次交谈者不同才使用；留空沿用普通内容。同一窗口后续跳转与连续交易不重复播放。")]
        public string differentActorOpeningText;
        public Choice[] choices = { new Choice() };
    }

    [SerializeField, InspectorName("对话显示名称")] private string speakerName = "";
    [SerializeField, InspectorName("开始段落 ID")] private string startNodeId = "start";
    [SerializeField, InspectorName("任务阶段（可选）")] private DialogueQuestProgression questProgression;
    [SerializeField, InspectorName("固定交谈者约定（可选）")] private DialogueActorAgreement actorAgreement;
    [SerializeField, InspectorName("竞技场报名（可选）")] private ArenaChallengeController arenaChallenge;
    [SerializeField, InspectorName("交谈记忆 ID（留空关闭）"), Tooltip("每名 NPC 使用不同且稳定的 ID。记住上次成功开启对话时控制的角色，随任务存档跨场景保留。不要与另一名 NPC 复用。")]
    private string dialogueMemoryId;
    [SerializeField, Min(.2f), InspectorName("交互距离"), Tooltip("双方中心之间的最大对话距离。只有 Blocks 层障碍会阻挡对话，其他层的柜台等障碍不影响对话。")] private float interactionDistance = 1.8f;
    [SerializeField, InspectorName("字体（留空使用游戏字体）")] private Font displayFont;
    [SerializeField, Range(0, 1), InspectorName("背景压暗程度")] private float dimOpacity = .65f;
    [SerializeField, InspectorName("对话段落")] private Node[] nodes = { new Node() };

    private ZeldaCharacterData data;
    private ZeldaFourWayMover mover;
    private ZeldaCharacterAiBase ai;
    private SpriteRenderer icon;
    private Texture2D iconTexture;
    private Sprite iconSprite;
    private bool selected;
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];

    public string SpeakerName => string.IsNullOrWhiteSpace(speakerName) ? name.Replace("(Clone)", "") : speakerName;
    public Font DisplayFont => displayFont;
    public float DimOpacity => dimOpacity;
    public string DialogueMemoryId => dialogueMemoryId;
    public Node StartNode => GetStartNode(ZeldaRuntimeRegistry.GetControlledMover());

    public Node GetStartNode(ZeldaFourWayMover actor)
    {
        if (arenaChallenge != null) return FindNode(arenaChallenge.DialogueNode(actor));
        string id = questProgression != null && questProgression.isActiveAndEnabled
            ? questProgression.GetStartNodeId(startNodeId) : startNodeId;
        if (actorAgreement != null && actorAgreement.isActiveAndEnabled)
            id = actorAgreement.GetStartNodeId(id, actor);
        return FindNode(id);
    }

    public Node FindNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || nodes == null) return null;
        foreach (Node node in nodes) if (node != null && node.id == id) return node;
        return null;
    }

    internal bool HasDifferentPreviousActor(ZeldaFourWayMover actor)
    {
        if (string.IsNullOrWhiteSpace(dialogueMemoryId) || actor == null) return false;
        var actorData = actor.GetComponent<ZeldaCharacterData>();
        return actorData != null && QuestJournalManager.GetOrCreate()
            .HasDifferentDialogueActor(dialogueMemoryId, actorData.IndividualAccessId);
    }

    internal void RememberDialogueActor(ZeldaFourWayMover actor)
    {
        if (string.IsNullOrWhiteSpace(dialogueMemoryId) || actor == null) return;
        var actorData = actor.GetComponent<ZeldaCharacterData>();
        if (actorData != null) QuestJournalManager.GetOrCreate()
            .RememberDialogueActor(dialogueMemoryId, actorData.IndividualAccessId);
    }

    private void Awake()
    {
        data = GetComponent<ZeldaCharacterData>();
        mover = GetComponent<ZeldaFourWayMover>();
        ai = GetComponent<ZeldaCharacterAiBase>();
    }

    private void Start()
    {
        if (questProgression != null || actorAgreement != null || arenaChallenge != null)
            QuestJournalInteractionMarker.ConfigureDialogue(this);
    }

    internal bool HasAvailableQuest =>
        (questProgression != null && questProgression.CanTrigger &&
         !questProgression.IsAccepted && !questProgression.IsCompleted) ||
        (actorAgreement != null && actorAgreement.HasAvailableQuest) ||
        (arenaChallenge != null && arenaChallenge.HasAvailableQuest);

    internal bool TryGetQuestMarkerPosition(out Vector3 position)
    {
        position = default;
        if (!isActiveAndEnabled || icon == null || !icon.gameObject.activeInHierarchy) return false;
        // World-space gap: neither character scaling nor hover enlargement may
        // make the diamond overlap the speech bubble. The diamond is ~0.65 high.
        position = new Vector3(icon.transform.position.x, icon.bounds.max.y + .42f, icon.transform.position.z);
        return true;
    }

    private bool IsSpeakerAvailable()
    {
        if (!isActiveAndEnabled || data == null || data.IsDead || data.IsGhostLike ||
            mover == null || mover.IsAttacking || StartNode == null) return false;
        // The icon and E interaction share the same state rules, regardless of
        // whom the NPC is investigating/fighting. Merely offering F is not a lock.
        return ai == null || !ai.isActiveAndEnabled ||
               (!ai.IsStunned && !ai.IsPossessionLocked &&
                ai.CurrentState != ZeldaAiState.Suspicious &&
                ai.CurrentState != ZeldaAiState.Alert && ai.CurrentState != ZeldaAiState.Hostile);
    }

    private bool ShouldShowIcon()
    {
        // Keep distant eligible NPCs identifiable; interaction range/LOS is checked separately.
        return IsSpeakerAvailable() && mover != ZeldaRuntimeRegistry.GetControlledMover() &&
               !CharacterDialogueWindow.IsOpen;
    }

    public bool CanConverse(ZeldaFourWayMover player)
    {
        if (!IsSpeakerAvailable() || player == null || player == mover || !player.isActiveAndEnabled) return false;
        var playerData = player.GetComponent<ZeldaCharacterData>();
        if (playerData == null || playerData.IsDead || playerData.IsGhostLike || player.IsAttacking) return false;
        if (ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject).handle != gameObject.scene.handle ||
            Vector2.Distance(player.transform.position, transform.position) > interactionDistance) return false;
        // Conversation can cross counters/furniture regardless of their collision.
        // Filter in the physics query so non-Blocks clutter cannot fill sightHits.
        Vector2 origin = transform.position;
        Vector2 delta = (Vector2)player.transform.position - origin;
        int blocks = LayerMask.NameToLayer("Blocks");
        int count = Physics2D.RaycastNonAlloc(origin, delta.normalized, sightHits, delta.magnitude,
            blocks >= 0 ? 1 << blocks : 0);
        if (count == sightHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = sightHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform) ||
                ZeldaRuntimeRegistry.GetGameplayScene(hit.gameObject).handle != gameObject.scene.handle) continue;
            if (hit.GetComponentInParent<ZeldaCharacterData>() != null) continue;
            return false;
        }
        return true;
    }

    private void Update()
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput || !CanConverse(player))
        { selected = false; return; }
        ZeldaInteractionArbiter.OfferInteraction(this, player, KeyCode.E, transform.position, value => selected = value);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this, player, KeyCode.E, transform.position, BeginDialogue);
    }

    public void BeginDialogue()
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput || !CanConverse(player)) return;
        // Permission gates the attempt, not discovery or the E interaction offer.
        if (!HasDialoguePermission(player))
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("权限不足，无法进行对话。");
            return;
        }
        Vector2 direction = player.transform.position - transform.position;
        if (ai != null && ai.isActiveAndEnabled) ai.FaceForDialogue(direction);
        else mover.FaceForDialogue(direction);
        CharacterDialogueWindow.Open(this, player);
    }

    public bool HasDialoguePermission(ZeldaFourWayMover player)
    {
        if (arenaChallenge != null && arenaChallenge.ChallengeActive && arenaChallenge.Entrant == player) return true;
        var playerData = player != null ? player.GetComponent<ZeldaCharacterData>() : null;
        if (playerData == null) return false;
        int sceneHandle = ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject).handle;
        // Only the actor's position at the opening attempt matters. A shopkeeper
        // behind a restricted counter must still serve a customer on the public side.
        // All overlapping areas must permit access, equivalent to taking the highest rank.
        foreach (PermissionArea area in ZeldaRuntimeRegistry.PermissionAreas)
        {
            if (area == null || !area.isActiveAndEnabled ||
                ZeldaRuntimeRegistry.GetGameplayScene(area.gameObject).handle != sceneHandle ||
                !area.Contains(player.transform.position)) continue;
            if (playerData.PermissionLevel < area.PermissionLevel) return false;
        }
        return true;
    }

    private void LateUpdate()
    {
        if (!ShouldShowIcon())
        {
            selected = false;
            if (icon != null) icon.gameObject.SetActive(false);
            return;
        }
        if (icon == null) BuildIcon();
        SpriteRenderer body = transform.Find("Visual")?.GetComponent<SpriteRenderer>() ?? GetComponent<SpriteRenderer>();
        float top = body != null && body.sprite != null ? body.bounds.max.y : transform.position.y + .6f;
        icon.transform.position = new Vector3(transform.position.x, top + .3f, transform.position.z);
        if (body != null)
        {
            icon.gameObject.layer = body.gameObject.layer;
            icon.sortingLayerID = body.sortingLayerID;
            icon.sortingOrder = body.sortingOrder + 2;
            icon.maskInteraction = body.maskInteraction;
        }
        icon.color = selected ? Color.white : ZeldaUiPalette.Primary;
        icon.transform.localScale = Vector3.one * (selected ? 1.15f : 1f);
        icon.gameObject.SetActive(true);
    }

    private void BuildIcon()
    {
        iconTexture = new Texture2D(24, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[24 * 16];
        for (int y = 4; y < 15; y++) for (int x = 1; x < 23; x++)
            pixels[y * 24 + x] = (y == 4 || y == 14 || x == 1 || x == 22) ? Color.white : new Color(.03f,.08f,.18f,1);
        for (int y = 1; y < 4; y++) for (int x = 5; x < 5 + y; x++) pixels[y * 24 + x] = Color.white;
        for (int x = 6; x <= 16; x += 5) for (int y = 8; y <= 9; y++)
        { pixels[y * 24 + x] = Color.white; pixels[y * 24 + x + 1] = Color.white; }
        iconTexture.SetPixels(pixels); iconTexture.Apply();
        iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, 24, 16), new Vector2(.5f,.5f), 40);
        iconSprite.hideFlags = HideFlags.HideAndDontSave;
        var go = new GameObject("Dialogue Available Icon") { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        icon = go.AddComponent<SpriteRenderer>(); icon.sprite = iconSprite;
    }

    private void OnDisable()
    {
        selected = false;
        CharacterDialogueWindow.CloseFor(this);
        if (icon != null) icon.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        CharacterDialogueWindow.CloseFor(this);
        if (icon != null) Destroy(icon.gameObject);
        if (iconSprite != null) Destroy(iconSprite);
        if (iconTexture != null) Destroy(iconTexture);
    }
}
