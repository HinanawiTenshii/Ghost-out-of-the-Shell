using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local links. Unlink the group before death propagation to prevent recursion.</summary>
[DefaultExecutionOrder(1000)]
public sealed class DominoSkillRuntime : MonoBehaviour
{
    private static readonly Dictionary<ZeldaCharacterData, DominoSkillRuntime> links = new Dictionary<ZeldaCharacterData, DominoSkillRuntime>();
    private readonly List<ZeldaCharacterData> members = new List<ZeldaCharacterData>();
    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
    private readonly List<CameraVisionStreamingExempt> exemptions = new List<CameraVisionStreamingExempt>();
    private ZeldaCharacterData caster;
    public const float BaseRange = 3f;
    private static int SkillLevel => PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.DominoLevel : 1;
    public static float Range => SkillLevel >= 3 ? 4f : BaseRange;
    public static int TargetLimit => SkillLevel >= 2 ? 3 : 2;
    public static bool HasActiveMark(ZeldaCharacterData data)
    {
        return data != null && !data.IsDead && links.TryGetValue(data, out var group) && group != null;
    }

    // Used by both the preview and the cast, including the same tie-break rule.
    public static void FindTargets(ZeldaFourWayMover user, IEnumerable<ZeldaFourWayMover> pool, List<ZeldaFourWayMover> result)
    {
        result.Clear();
        if (user == null) return;
        int limit = TargetLimit;
        float rangeSquared = Range * Range;
        var skillScene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        foreach (var mover in pool)
        {
            if (mover == null || mover == user || mover.enabled ||
                ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != skillScene || mover.GetComponent<GhostZeldaCharacterData>() != null) continue;
            var data = mover.GetComponent<ZeldaCharacterData>();
            float distance = ((Vector2)(mover.transform.position - user.transform.position)).sqrMagnitude;
            if (data == null || data.IsDead || distance > rangeSquared || result.Contains(mover)) continue;
            int index = 0;
            while (index < result.Count)
            {
                float other = ((Vector2)(result[index].transform.position - user.transform.position)).sqrMagnitude;
                if (distance < other || (distance == other && mover.GetInstanceID() < result[index].GetInstanceID())) break;
                index++;
            }
            if (index < limit) result.Insert(index, mover);
            if (result.Count > limit) result.RemoveAt(limit);
        }
    }

    public static void Use(ZeldaFourWayMover user)
    {
        if (user == null || SoulMarkRuntime.IsTransferring) return;
        var source = user.GetComponent<ZeldaCharacterData>();
        if (source == null || source.IsDead || source.IsGhostForm) return;
        var candidates = new List<ZeldaFourWayMover>();
        // A cast is infrequent. Include streamed-out NPCs so visibility is not an extra targeting rule.
        FindTargets(user, Object.FindObjectsOfType<ZeldaFourWayMover>(true), candidates);
        if (candidates.Count == 0 || !source.TrySpendPossessionEnergy(2)) return;
        var group = new GameObject("Domino Links").AddComponent<DominoSkillRuntime>();
        SceneManager.MoveGameObjectToScene(group.gameObject, ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject));
        group.caster = source;
        group.AddMember(source);
        for (int i = 0; i < candidates.Count; i++) group.AddMember(candidates[i].GetComponent<ZeldaCharacterData>());
        group.LateUpdate();
    }

    private void AddMember(ZeldaCharacterData data)
    {
        RemoveMark(data);
        SoulMarkRuntime.ClearAfterDirectPossession(data.GetComponent<ZeldaFourWayMover>());
        links[data] = this;
        members.Add(data);
        var marker = new GameObject("Domino Mark Visual").AddComponent<SpriteRenderer>();
        marker.sprite = SkillPageArt.GetDominoWorldSprite();
        marker.transform.localScale = Vector3.one * 0.48f;
        markers.Add(marker);
        exemptions.Add(data.gameObject.AddComponent<CameraVisionStreamingExempt>());
        data.gameObject.SetActive(true);
    }

    public static void RemoveMark(ZeldaCharacterData data)
    {
        if (data == null || !links.TryGetValue(data, out var group)) return;
        int i = group.members.IndexOf(data);
        links.Remove(data);
        if (i >= 0)
        {
            if (group.markers[i] != null) Destroy(group.markers[i].gameObject);
            if (group.exemptions[i] != null) Destroy(group.exemptions[i]);
            group.members.RemoveAt(i); group.markers.RemoveAt(i); group.exemptions.RemoveAt(i);
        }
        if (group.members.Count < 2) group.Dissolve();
    }

    public static void ShareDamage(ZeldaCharacterData origin, int damage, ZeldaCharacterData attacker)
    {
        if (!links.TryGetValue(origin, out var group)) return;
        foreach (var target in group.members.ToArray())
            if (target != null && target != origin && !target.IsDead) target.TakeDominoDamage(damage, attacker);
    }

    public static void ShareDeath(ZeldaCharacterData origin)
    {
        if (!links.TryGetValue(origin, out var group)) return;
        var targets = group.members.ToArray();
        // Ghosts have no death-ghost prefab; a linked body supplies the fallback.
        var controlled = ZeldaRuntimeRegistry.GetControlledMover();
        GameObject ghostPrefab = null;
        Vector3 ghostPosition = Vector3.zero;
        if (controlled != null && controlled.isActiveAndEnabled && controlled.GetComponent<GhostZeldaCharacterData>() != null &&
            group.members.Contains(controlled.GetComponent<ZeldaCharacterData>()))
        {
            ghostPosition = controlled.transform.position;
            controlled.enabled = false;
            foreach (var target in targets)
            {
                var mover = target != null ? target.GetComponent<ZeldaFourWayMover>() : null;
                if (mover != null && mover.SoulTransferFallbackPrefab != null) { ghostPrefab = mover.SoulTransferFallbackPrefab; break; }
            }
        }
        group.Dissolve();
        foreach (var target in targets)
            if (target != null && target != origin && !target.IsDead) target.DieFromDomino();
        SpawnGhost(ghostPrefab, ghostPosition);
    }

    public static bool TryPossess(ZeldaFourWayMover user, ZeldaFourWayMover target)
    {
        var targetData = target.GetComponent<ZeldaCharacterData>();
        if (targetData == null || !links.ContainsKey(targetData)) return false;
        var prefab = user.SoulTransferFallbackPrefab != null ? user.SoulTransferFallbackPrefab : target.SoulTransferFallbackPrefab;
        Vector3 position = target.transform.position;
        user.enabled = false; // Suppress the normal source-position ghost.
        SoulMarkRuntime.ClearAfterDirectPossession(target);
        targetData.DieFromDomino();
        var source = user.GetComponent<ZeldaCharacterData>();
        if (source != null && !source.IsDead) source.DieFromDomino();
        SpawnGhost(prefab, position);
        return true;
    }

    private static void SpawnGhost(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        var ghost = Instantiate(prefab, position, Quaternion.identity).GetComponent<ZeldaFourWayMover>();
        if (ghost != null) ghost.ReceiveControl(Vector2.down);
    }

    private void LateUpdate()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            var data = members[i];
            if (data == null) { Dissolve(); return; }
            if (data.IsDead) { ShareDeath(data); return; }
            var visual = data.GetComponentInChildren<SpriteRenderer>();
            var marker = markers[i];
            marker.enabled = data != caster && !DominoSkillPreview.IsPreviewTarget(data);
            marker.color = ZeldaUiPalette.Primary;
            marker.transform.position = (visual != null ? visual.bounds.center : data.transform.position) + Vector3.up * 0.15f;
            if (visual != null) { marker.sortingLayerID = visual.sortingLayerID; marker.sortingOrder = visual.sortingOrder + 5; }
        }
    }

    public static void ClearForSceneTransition()
    {
        foreach (var group in new HashSet<DominoSkillRuntime>(links.Values)) if (group != null) group.Dissolve();
        links.Clear();
    }
    private void OnEnable() => SceneManager.activeSceneChanged += SceneChanged;
    private void OnDisable() => SceneManager.activeSceneChanged -= SceneChanged;
    private void SceneChanged(Scene a, Scene b) { if (a != b) Dissolve(); }
    private void Dissolve() { Cleanup(); Destroy(gameObject); }
    private void Cleanup()
    {
        foreach (var member in members)
            if (links.TryGetValue(member, out var owner) && owner == this) links.Remove(member);
        foreach (var marker in markers) if (marker != null) Destroy(marker.gameObject);
        foreach (var exemption in exemptions) if (exemption != null) Destroy(exemption);
        members.Clear(); markers.Clear(); exemptions.Clear();
    }
    private void OnDestroy() => Cleanup();
}

/// <summary>Reusable, world-only targeting graphics; does not change character state.</summary>
public sealed class DominoSkillPreview : MonoBehaviour
{
    private static DominoSkillPreview current;
    private readonly List<ZeldaFourWayMover> pool = new List<ZeldaFourWayMover>();
    private readonly HashSet<ZeldaFourWayMover> known = new HashSet<ZeldaFourWayMover>();
    private readonly List<ZeldaFourWayMover> targets = new List<ZeldaFourWayMover>(3);
    private readonly SpriteRenderer[] icons = new SpriteRenderer[3];
    private MeshRenderer rangeRenderer;
    private Mesh rangeMesh;
    private Material rangeMaterial;
    private Scene scene;
    private float phase;

    public static bool IsPreviewTarget(ZeldaCharacterData data)
    {
        if (DominoSkillRuntime.HasActiveMark(data)) return false;
        if (current == null || !current.gameObject.activeInHierarchy) return false;
        foreach (var target in current.targets)
            if (target != null && target.gameObject == data.gameObject) return true;
        return false;
    }

    private void Awake()
    {
        current = this;
        gameObject.layer = 2; // Ignore Raycast; these graphics never affect targeting or visibility queries.
        var rangeObject = new GameObject("Domino Range");
        rangeObject.layer = 2;
        rangeObject.transform.SetParent(transform, false);
        rangeRenderer = rangeObject.AddComponent<MeshRenderer>();
        rangeMaterial = new Material(Shader.Find("Sprites/Default"));
        rangeMaterial.color = new Color(ZeldaUiPalette.Primary.r, ZeldaUiPalette.Primary.g, ZeldaUiPalette.Primary.b, 0.65f);
        rangeRenderer.sharedMaterial = rangeMaterial;
        rangeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRenderer.receiveShadows = false;
        rangeMesh = new Mesh { name = "Domino Dashed Range" };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int dashes = 64;
        const float halfWidth = 0.007f;
        for (int dash = 0; dash < dashes; dash++)
            for (int step = 0; step < 2; step++)
            {
                float a = (dash + step * 0.3f) * Mathf.PI * 2f / dashes;
                float b = (dash + (step + 1) * 0.3f) * Mathf.PI * 2f / dashes;
                Vector3 va = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                Vector3 vb = new Vector3(Mathf.Cos(b), Mathf.Sin(b));
                int index = vertices.Count;
                vertices.Add(va * (DominoSkillRuntime.BaseRange - halfWidth));
                vertices.Add(va * (DominoSkillRuntime.BaseRange + halfWidth));
                vertices.Add(vb * (DominoSkillRuntime.BaseRange + halfWidth));
                vertices.Add(vb * (DominoSkillRuntime.BaseRange - halfWidth));
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
        rangeMesh.SetVertices(vertices); rangeMesh.SetTriangles(triangles, 0);
        var colors = new Color[vertices.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        rangeMesh.colors = colors;
        rangeMesh.RecalculateBounds();
        rangeObject.AddComponent<MeshFilter>().sharedMesh = rangeMesh;
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = new GameObject("Domino Target Preview").AddComponent<SpriteRenderer>();
            icons[i].gameObject.layer = 2;
            icons[i].transform.SetParent(transform, false);
            icons[i].sprite = SkillPageArt.GetDominoWorldSprite();
        }
    }

    public void Show(ZeldaFourWayMover user)
    {
        var skillScene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        bool rescan = !gameObject.activeSelf || scene != skillScene || pool.Count == 0;
        gameObject.SetActive(true);
        if (rescan)
        {
            scene = skillScene;
            pool.Clear(); known.Clear();
            foreach (var mover in Object.FindObjectsOfType<ZeldaFourWayMover>(true))
                if (mover != null && ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) == scene && known.Add(mover)) pool.Add(mover);
        }
        // Newly spawned movers register automatically; no per-frame whole-scene searches.
        foreach (var mover in ZeldaRuntimeRegistry.Movers)
            if (mover != null && ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) == scene && known.Add(mover)) pool.Add(mover);
        DominoSkillRuntime.FindTargets(user, pool, targets);
        transform.position = user.transform.position;
        rangeRenderer.transform.localScale = Vector3.one * (DominoSkillRuntime.Range / DominoSkillRuntime.BaseRange);
        var userVisual = user.GetComponentInChildren<SpriteRenderer>();
        if (userVisual != null)
        {
            rangeRenderer.sortingLayerID = userVisual.sortingLayerID;
            rangeRenderer.sortingOrder = userVisual.sortingOrder + 4;
        }
        phase = (phase + Time.deltaTime) % 1.2f;
        float breath = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f / 1.2f);
        for (int i = 0; i < icons.Length; i++)
        {
            var icon = icons[i];
            icon.enabled = i < targets.Count &&
                !DominoSkillRuntime.HasActiveMark(targets[i].GetComponent<ZeldaCharacterData>());
            if (!icon.enabled) continue;
            var visual = targets[i].GetComponentInChildren<SpriteRenderer>(true);
            icon.transform.position = (visual != null ? visual.bounds.center : targets[i].transform.position) + Vector3.up * 0.15f;
            icon.transform.localScale = Vector3.one * (0.48f * Mathf.Lerp(0.94f, 1.06f, breath));
            Color color = ZeldaUiPalette.Primary;
            color.a = Mathf.Lerp(0.25f, 0.95f, breath);
            icon.color = color;
            if (visual != null) { icon.sortingLayerID = visual.sortingLayerID; icon.sortingOrder = visual.sortingOrder + 5; }
        }
    }

    public void Hide() { targets.Clear(); gameObject.SetActive(false); }
    private void OnDestroy()
    {
        if (current == this) current = null;
        if (rangeMesh != null) Destroy(rangeMesh);
        if (rangeMaterial != null) Destroy(rangeMaterial);
    }
}
