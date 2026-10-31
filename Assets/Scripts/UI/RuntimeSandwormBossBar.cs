using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-visit-local boss HUD. First actual sight latches it until defeat/scene exit, independent of streaming.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(220)]
public sealed class RuntimeSandwormBossBar : MonoBehaviour
{
    public static event System.Action<SandwormZeldaCharacterData> EncounterStarted;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEncounterListeners() => EncounterStarted = null;

    [SerializeField, Min(80f)] private float barWidth = 380f;
    [SerializeField, Min(0f)] private float permissionGap = 10f;
    [SerializeField, Min(0f)] private float damageTrailDelay = .55f;
    [SerializeField, Min(.01f)] private float damageTrailDuration = .65f;
    private const float NameBlinkTime = .6f, NameHoldTime = .12f, BracketHoldTime = .12f;
    private const float ExpandTime = .4f, FillTime = .55f;
    private const float ExpandStart = NameBlinkTime + NameHoldTime + BracketHoldTime;
    private const float FillStart = ExpandStart + ExpandTime;
    private const float IntroDuration = FillStart + FillTime;
    private const float DefeatHoldTime = .7f, DefeatFadeTime = .25f;

    private RectTransform root, permissionStatus;
    private Text title;
    private SandwormBossBarGraphic graphic;
    private CanvasGroup group;
    private SandwormZeldaCharacterData target;
    private readonly Plane[] frustum = new Plane[6];
    private int visitScene = -1, lastHealth, lastMaximum;
    private float introAge, actual, trail, trailStart, trailDelay, trailAge, defeatAge;
    private bool encountered, subscribed;

    public bool IsEncounterVisible => encountered;
    public SandwormZeldaCharacterData Target => target;
    public float HealthFraction => actual;
    public float DelayedFraction => trail;
    public SandwormBossBarGraphic BarGraphic => graphic;
    public bool IntroComplete => introAge >= IntroDuration;

    public void Build(RectTransform areaPermissionStatus, Font font)
    {
        if (root != null) return;
        root = (RectTransform)transform; permissionStatus = areaPermissionStatus;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, 1f);
        group = gameObject.AddComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
        var label = new GameObject("Boss Name", typeof(RectTransform), typeof(Text));
        label.layer = gameObject.layer; label.transform.SetParent(root, false);
        title = label.GetComponent<Text>(); title.text = "沙虫"; title.font = font;
        title.fontSize = 18; title.alignment = TextAnchor.MiddleCenter;
        title.horizontalOverflow = HorizontalWrapMode.Overflow; title.verticalOverflow = VerticalWrapMode.Overflow;
        title.color = ZeldaUiPalette.Primary; title.raycastTarget = false;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(160f, 24f);
        var bar = new GameObject("Bracket Health Track", typeof(RectTransform), typeof(SandwormBossBarGraphic));
        bar.layer = gameObject.layer; bar.transform.SetParent(root, false);
        graphic = bar.GetComponent<SandwormBossBarGraphic>(); graphic.raycastTarget = false;
        graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = graphic.rectTransform.pivot = new Vector2(.5f, 1f);
        graphic.rectTransform.anchoredPosition = new Vector2(0f, -28f);
        ResetEncounter(); Layout();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += SceneChanged;
        SceneManager.sceneUnloaded += SceneUnloaded;
        Subscribe();
    }
    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= SceneChanged;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        Unsubscribe();
    }
    private void OnDestroy() => Unsubscribe();
    private void SceneChanged(Scene previous, Scene next) { ResetEncounter(); visitScene = next.handle; }
    private void SceneUnloaded(Scene scene) { if (scene.handle == visitScene) { ResetEncounter(); visitScene = -1; } }
    private void LateUpdate() => Advance(Time.deltaTime);

    public void Advance(float deltaTime)
    {
        if (root == null || permissionStatus == null) return;
        Layout();
        Scene scene = SceneManager.GetActiveScene();
        if (visitScene != scene.handle) { ResetEncounter(); visitScene = scene.handle; }
        if (!encountered) TryDiscover(scene);
        if (!encountered) return;
        if (target != null && ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != scene)
        { ResetEncounter(); return; }
        // Temporary streaming disable preserves the reference and health. Actual
        // destruction without a lethal hit is removal, not a fabricated kill.
        if (target == null && actual > 0f) { ResetEncounter(); return; }
        if (target != null) ReadHealth();
        float dt = Mathf.Max(0f, deltaTime);
        introAge += dt;
        float waiting = Mathf.Min(trailDelay, dt); trailDelay -= waiting;
        if (trailDelay <= 0f && trail > actual)
        {
            trailAge += dt - waiting;
            trail = Mathf.Max(actual, Mathf.Lerp(trailStart, actual,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(trailAge / Mathf.Max(.01f, damageTrailDuration)))));
        }
        if (actual <= 0f && trail <= 0f && IntroComplete)
        {
            defeatAge += dt;
            if (defeatAge >= DefeatHoldTime + DefeatFadeTime) { ResetEncounter(); return; }
        }
        Present();
    }

    private void TryDiscover(Scene scene)
    {
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        if (player == null || !player.isActiveAndEnabled || ZeldaRuntimeRegistry.GetGameplayScene(player.gameObject) != scene ||
            CharacterPotionBuffs.IsRestoringWorld || GameSaveSystem.IsLoading || RetroSceneLoadReveal.IsPreparingGameplay ||
            SandwormZeldaCharacterData.ActiveSandworms.Count == 0) return;
        Camera camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled) return;
        var vision = camera.GetComponent<CameraCircularVision>();
        GeometryUtility.CalculateFrustumPlanes(camera, frustum);
        SandwormZeldaCharacterData closest = null; float distance = float.PositiveInfinity;
        foreach (var boss in SandwormZeldaCharacterData.ActiveSandworms)
        {
            if (boss == null || ZeldaRuntimeRegistry.GetGameplayScene(boss.gameObject) != scene || !boss.HasVisibleBodyPart(camera, vision, frustum)) continue;
            float candidate = ((Vector2)boss.AttributeWindowAnchor.position - (Vector2)camera.transform.position).sqrMagnitude;
            if (candidate < distance) { closest = boss; distance = candidate; }
        }
        if (closest == null) return;
        target = closest; encountered = true; introAge = defeatAge = trailAge = trailDelay = 0f;
        lastHealth = target.CurrentHealth; lastMaximum = target.Health;
        actual = trail = trailStart = Mathf.Clamp01(lastHealth / (float)Mathf.Max(1, lastMaximum));
        Subscribe();
        EncounterStarted?.Invoke(target);
    }

    private void Subscribe()
    {
        if (subscribed || target == null) return;
        target.ValuesChanged += OnHealthChanged; subscribed = true;
    }
    private void Unsubscribe()
    {
        if (subscribed && !ReferenceEquals(target, null)) target.ValuesChanged -= OnHealthChanged;
        subscribed = false;
    }
    private void OnHealthChanged() { ReadHealth(); Present(); }
    private void ReadHealth()
    {
        if (target == null) return;
        int hp = target.CurrentHealth, max = target.Health;
        if (hp == lastHealth && max == lastMaximum) return;
        float next = Mathf.Clamp01(hp / (float)Mathf.Max(1, max));
        if (hp < lastHealth)
        {
            trail = Mathf.Max(trail, actual);
            trailStart = trail; trailDelay = Mathf.Max(0f, damageTrailDelay); trailAge = 0f;
        }
        else if (max != lastMaximum) { trail = trailStart = next; trailDelay = trailAge = 0f; }
        else { trail = Mathf.Max(trail, next); trailStart = Mathf.Max(trailStart, next); }
        actual = next; trail = Mathf.Max(trail, actual);
        lastHealth = hp; lastMaximum = max;
        if (hp > 0) defeatAge = 0f;
    }

    private void Layout()
    {
        float canvasWidth = root.parent is RectTransform parent ? parent.rect.width : 1280f;
        float width = Mathf.Min(barWidth, Mathf.Max(120f, canvasWidth * .46f));
        root.anchoredPosition = permissionStatus.anchoredPosition + Vector2.down * (permissionStatus.rect.height + permissionGap);
        root.sizeDelta = new Vector2(width, 54f);
        graphic.rectTransform.sizeDelta = new Vector2(width, 26f);
    }

    private void Present()
    {
        if (title == null) return;
        bool blinkOn = introAge >= NameBlinkTime || Mathf.FloorToInt(introAge / .1f) % 2 == 0;
        title.enabled = encountered && blinkOn;
        graphic.enabled = encountered && introAge >= NameBlinkTime + NameHoldTime;
        float expansion = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((introAge - ExpandStart) / ExpandTime));
        float fill = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((introAge - FillStart) / FillTime));
        graphic.SetDisplay(expansion, actual * fill, trail * fill);
        group.alpha = encountered ? 1f - Mathf.Clamp01((defeatAge - DefeatHoldTime) / DefeatFadeTime) : 0f;
    }

    private void ResetEncounter()
    {
        Unsubscribe(); target = null; encountered = false;
        actual = trail = trailStart = introAge = trailAge = trailDelay = defeatAge = 0f;
        Present();
    }
}
