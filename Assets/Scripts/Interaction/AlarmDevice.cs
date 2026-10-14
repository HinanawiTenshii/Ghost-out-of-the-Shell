using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Powered, explicit NPC dispatch. Camera streaming retains the AI until it returns home.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Alarm Device")]
public sealed class AlarmDevice : MonoBehaviour
{
    public const int MaximumResponders = 10;
    [Header("Responders / 响应角色（最多十名）")]
    [SerializeField, Tooltip("拖入本场景中的角色。允许距离加载暂时卸载的角色；重复引用只响应一次。")]
    private ZeldaCharacterAiBase[] responders = new ZeldaCharacterAiBase[MaximumResponders];
    [Header("Interaction / 交互")]
    [SerializeField, Min(.1f)] private float interactionDistance = 1.4f;
    [SerializeField, Min(.1f)] private float activationCooldown = 1.2f;
    [SerializeField] private Font promptFont;
    private float nextActivationTime;
    private float flashUntil;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;
    private readonly HashSet<ZeldaCharacterAiBase> dispatched = new HashSet<ZeldaCharacterAiBase>();

    public bool HasPower => BatterySocket.HasConnectedSocket(this) && BatterySocket.IsPowered(this);
    public bool IsFlashing => Application.isPlaying && Time.time < flashUntil;
    private bool InputBlocked => DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput || Time.timeScale <= 0f;

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(.1f, interactionDistance);
        activationCooldown = Mathf.Max(.1f, activationCooldown);
        if (responders == null) responders = new ZeldaCharacterAiBase[0];
        if (responders.Length > MaximumResponders) Array.Resize(ref responders, MaximumResponders);
    }

    private void Update()
    {
        var mover = InputBlocked || !HasPower || Time.time < nextActivationTime ? null : GetNearbyCharacter();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, Interact);
    }

    private ZeldaFourWayMover GetNearbyCharacter()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled) return null;
        var data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead && !data.IsGhostForm &&
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject).handle == gameObject.scene.handle &&
            Vector2.Distance(mover.transform.position, transform.position) <= interactionDistance ? mover : null;
    }

    private void Interact()
    {
        if (!isActiveAndEnabled || InputBlocked || !HasPower || Time.time < nextActivationTime) return;
        var mover = GetNearbyCharacter();
        if (mover == null || !mover.TryBeginItemAttack(.24f)) return;
        TryActivate();
    }

    // UnityEvent entry point; external activation uses the same power requirement.
    public void ActivateAlarm() => TryActivate();

    public bool TryActivate()
    {
        if (!isActiveAndEnabled || !HasPower || Time.time < nextActivationTime) return false;
        nextActivationTime = Time.time + activationCooldown;
        flashUntil = Time.time + 1.2f;
        dispatched.Clear();
        int count = responders == null ? 0 : Mathf.Min(responders.Length, MaximumResponders);
        for (int i = 0; i < count; i++)
        {
            var ai = responders[i];
            if (ai == null || !dispatched.Add(ai) || ai.gameObject.scene != gameObject.scene) continue;
            var data = ai.GetComponent<ZeldaCharacterData>();
            var mover = ai.GetComponent<ZeldaFourWayMover>();
            // Disabled player movement is normal for NPCs; disabled AI is not streaming.
            if (data == null || data.IsDead || data.IsGhostForm || mover == null || mover.enabled ||
                !ai.enabled || ai.IsPossessionLocked) continue;
            CameraVisionObjectStreaming.RestoreForGameplay(ai.gameObject);
            // Do not enable arbitrary author-hidden hierarchies or revive dead actors.
            if (ai.isActiveAndEnabled) ai.RespondToAlarm(transform.position);
        }
        return true;
    }

    private void ShowPrompt(bool visible)
    {
        var mover = visible && !InputBlocked && HasPower && Time.time >= nextActivationTime ? GetNearbyCharacter() : null;
        if (mover == null || promptFont == null)
        {
            if (promptObject != null) promptObject.SetActive(false);
            return;
        }
        if (promptObject == null)
        {
            promptObject = new GameObject("Alarm Interaction Prompt", typeof(TextMesh)) { hideFlags = HideFlags.HideAndDontSave };
            promptObject.transform.SetParent(transform, false);
            promptText = promptObject.GetComponent<TextMesh>();
            promptText.font = promptFont;
            promptText.fontSize = 72;
            promptText.characterSize = .035f;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.color = ZeldaUiPalette.Primary;
            promptText.text = "按[E]启动警报器";
            promptMaterial = new Material(promptFont.material) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = promptObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = promptMaterial;
            renderer.sortingOrder = short.MaxValue - 2;
            ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptFont.RequestCharactersInTexture(promptText.text, 72, FontStyle.Normal);
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        promptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, .9f));
        promptObject.transform.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        promptObject.transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        promptObject.SetActive(true);
    }

    private void OnDisable() { if (promptObject != null) promptObject.SetActive(false); }
    private void OnDestroy()
    {
        if (promptObject != null) Destroy(promptObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }
}
