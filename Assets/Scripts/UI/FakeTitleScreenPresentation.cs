using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-local presentation only. Menu actions are intentionally not wired yet.</summary>
[DisallowMultipleComponent]
public sealed class FakeTitleScreenPresentation : MonoBehaviour
{
    public const int PaletteStageCount = 5;

    [System.Serializable]
    private sealed class PaletteLayer
    {
        public Image image;
        [Tooltip("Five baked palettes, from deep blue to the original full-colour sprite.")]
        public Sprite[] stages;
    }

    [Header("Authored scene UI")]
    [SerializeField] private CanvasGroup menu;
    [SerializeField] private CanvasGroup blackCurtain;
    [SerializeField] private GameObject firstOption;
    [SerializeField] private CanvasGroup enterPrompt;
    [SerializeField] private FakeTitleMenuButton[] menuOptions;
    [SerializeField] private LoopingBackgroundMusicPlayer music;
    [Header("Retro power-on")]
    [Min(0f)] [SerializeField] private float darkHoldSeconds = .2f;
    [Min(.06f)] [SerializeField] private float paletteHoldSeconds = .24f;
    [Min(0f)] [SerializeField] private float starlightDelaySeconds = .12f;
    [SerializeField] private PaletteLayer[] paletteLayers;
    [Header("Press Enter / short menu reveal")]
    [Min(.04f)] [SerializeField] private float menuStepSeconds = .07f;
    [Min(.2f)] [SerializeField] private float promptBlinkSeconds = .65f;
    [Header("Quiet chiptune feedback")]
    [SerializeField] private AudioSource feedbackSource;
    [SerializeField] private AudioClip enterSound;
    [SerializeField] private AudioClip confirmSound;
    [Range(0f, 1f)] [SerializeField] private float feedbackVolume = .3f;

    private static FakeTitleScreenPresentation active;
    private Coroutine reveal;
    private Coroutine menuReveal;
    private float promptStarted;
    private float nextFeedbackAt = -1f;
    public static bool IsOpen => active != null && active.isActiveAndEnabled;
    public bool IsReady { get; private set; }
    public bool IsAwaitingEnter { get; private set; }
    public bool IsStarlightReady { get; private set; }
    public int PaletteStage { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; }

    private void OnEnable()
    {
        active = this;
        IsReady = false;
        IsAwaitingEnter = false;
        IsStarlightReady = false;
        PaletteStage = -1;
        nextFeedbackAt = -1f;
        SetMenuRevealStep(0);
        if (menu != null) { menu.alpha = 0f; menu.interactable = false; menu.blocksRaycasts = false; }
        if (enterPrompt != null) { enterPrompt.alpha = 0f; enterPrompt.interactable = false; enterPrompt.blocksRaycasts = false; }
        var events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null &&
            events.currentSelectedGameObject.transform.IsChildOf(transform))
            events.SetSelectedGameObject(null);
        if (blackCurtain != null)
        {
            blackCurtain.gameObject.SetActive(true);
            blackCurtain.alpha = 1f;
            blackCurtain.blocksRaycasts = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        reveal = StartCoroutine(PowerOn());
    }

    private void Update()
    {
        if (!IsAwaitingEnter) return;
        if (enterPrompt != null)
            enterPrompt.alpha = Mathf.FloorToInt((Time.unscaledTime - promptStarted) / Mathf.Max(.2f, promptBlinkSeconds)) % 2 == 0 ? 1f : .4f;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            BeginMenuReveal();
    }

    private bool BeginMenuReveal()
    {
        // No mouse/Submit shortcut; the title explicitly waits for a fresh Enter.
        if (!IsAwaitingEnter || menuReveal != null || RetroSceneLoadReveal.IsBlockingInput) return false;
        IsAwaitingEnter = false;
        PlayFeedback(enterSound);
        if (enterPrompt != null) enterPrompt.alpha = 0f;
        menuReveal = StartCoroutine(RevealMenu());
        return true;
    }

    public bool PlayOptionConfirmSound()
    {
        return IsReady && !RetroSceneLoadReveal.IsBlockingInput && PlayFeedback(confirmSound);
    }

    private bool PlayFeedback(AudioClip clip)
    {
        if (!isActiveAndEnabled || feedbackSource == null || clip == null || feedbackVolume <= 0f ||
            Time.unscaledTime < nextFeedbackAt || feedbackSource.isPlaying) return false;
        // A dedicated single voice keeps rapid clicks from stacking or cutting
        // off notes, and never interrupts/restarts the separate BGM source.
        nextFeedbackAt = Time.unscaledTime + Mathf.Max(.06f, clip.length);
        feedbackSource.clip = clip;
        feedbackSource.volume = Mathf.Clamp01(feedbackVolume);
        feedbackSource.Play();
        return true;
    }

    private IEnumerator PowerOn()
    {
        // Keep our full black cover while the game's normal loading bands open.
        // The curtain is only ever opaque or transparent, never a brightness fade.
        yield return null;
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        float elapsed = 0f;
        while (elapsed < darkHoldSeconds)
        { elapsed += Time.unscaledDeltaTime; yield return null; }
        if (music != null) music.Play();
        for (int stage = 0; stage < PaletteStageCount; stage++)
        {
            ApplyPalette(stage);
            if (blackCurtain != null) blackCurtain.alpha = 0f;
            // Every palette gets a genuine held frame sequence, even at low FPS.
            // Baked sprite colours are processed by CRT; text stays on its crisp canvas.
            yield return new WaitForSecondsRealtime(Mathf.Max(.06f, paletteHoldSeconds));
        }
        if (blackCurtain != null) { blackCurtain.alpha = 0f; blackCurtain.blocksRaycasts = false; }
        promptStarted = Time.unscaledTime;
        if (enterPrompt != null) enterPrompt.alpha = 1f;
        IsAwaitingEnter = true;
        if (starlightDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(starlightDelaySeconds);
        IsStarlightReady = true;
        reveal = null;
    }

    private void SetMenuRevealStep(int step)
    {
        if (menuOptions == null) return;
        for (int i = 0; i < menuOptions.Length; i++)
            if (menuOptions[i] != null) menuOptions[i].SetRevealLevel(Mathf.Clamp(step - i, 0, 3));
    }

    private IEnumerator RevealMenu()
    {
        if (menu != null) menu.alpha = 1f;
        int steps = (menuOptions != null ? menuOptions.Length : 0) + 2;
        for (int step = 1; step <= steps; step++)
        {
            SetMenuRevealStep(step);
            yield return new WaitForSecondsRealtime(Mathf.Max(.04f, menuStepSeconds));
        }
        // Consume the opening key: a held Enter must not also submit the first
        // option. Keep navigation/raycasts blocked through a full release frame.
        while (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)) yield return null;
        yield return null;
        if (menu != null) { menu.interactable = true; menu.blocksRaycasts = true; }
        IsReady = true;
        if (EventSystem.current != null && firstOption != null)
            EventSystem.current.SetSelectedGameObject(firstOption);
        menuReveal = null;
    }

    private void ApplyPalette(int stage)
    {
        PaletteStage = stage;
        if (paletteLayers == null) return;
        foreach (var layer in paletteLayers)
        {
            if (layer == null || layer.image == null || layer.stages == null ||
                stage >= layer.stages.Length || layer.stages[stage] == null) continue;
            layer.image.sprite = layer.stages[stage];
        }
    }

    private void OnDisable()
    {
        if (reveal != null) { StopCoroutine(reveal); reveal = null; }
        if (menuReveal != null) { StopCoroutine(menuReveal); menuReveal = null; }
        if (music != null) music.Stop();
        if (feedbackSource != null) { feedbackSource.Stop(); feedbackSource.clip = null; }
        if (active == this) active = null;
        IsReady = false;
        IsAwaitingEnter = false;
        IsStarlightReady = false;
        ApplyPalette(PaletteStageCount - 1);
        SetMenuRevealStep((menuOptions != null ? menuOptions.Length : 0) + 2);
        if (enterPrompt != null) enterPrompt.alpha = 0f;
        if (menu != null) { menu.alpha = 1f; menu.interactable = true; menu.blocksRaycasts = true; }
        if (blackCurtain != null) { blackCurtain.alpha = 0f; blackCurtain.blocksRaycasts = false; }
    }
}
