using System.Collections.Generic;
using UnityEngine;

/// <summary>One shared network, unique undirected edges, one energy transaction per entrant.</summary>
public sealed class ElectricGateNetwork : MonoBehaviour
{
    private static readonly HashSet<ElectricGatePillar> Pillars = new HashSet<ElectricGatePillar>();
    private static ElectricGateNetwork instance;
    private readonly Dictionary<string, Field> fields = new Dictionary<string, Field>();
    private readonly HashSet<string> wanted = new HashSet<string>();
    private readonly List<string> obsolete = new List<string>();
    private readonly List<ZeldaFourWayMover> characters = new List<ZeldaFourWayMover>();
    private Material material;
    private Material particleMaterial;
    private float nextPairing;
    private static ZeldaCharacterData struckCharacter;
    private static Vector2 reboundPosition;
    private static readonly RaycastHit2D[] VisibilityHits = new RaycastHit2D[64];
    private readonly List<BoxCollider2D> sleepingWalls = new List<BoxCollider2D>();
    private CameraVisionObjectStreaming[] streamers;
    private float nextWallScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Pillars.Clear(); instance = null; struckCharacter = null; }
    public static void Register(ElectricGatePillar pillar)
    {
        if (!Application.isPlaying) return;
        Pillars.Add(pillar);
        if (instance == null)
        {
            var root = new GameObject("Electric Gate Network") { hideFlags = HideFlags.HideAndDontSave };
            instance = root.AddComponent<ElectricGateNetwork>(); DontDestroyOnLoad(root);
        }
        instance.nextPairing = 0;
    }
    public static void Unregister(ElectricGatePillar pillar)
    {
        Pillars.Remove(pillar);
        if (instance != null) instance.nextPairing = 0;
    }
    private void Awake()
    {
        // Resources keeps the shader in player builds, without changing global CRT/bloom settings.
        Shader glow = Resources.Load<Shader>("ElectricGateGlow");
        bool supported = glow != null && glow.isSupported;
        material = new Material(supported ? glow : Shader.Find("Sprites/Default")) {
            name = "Electric Gate Line Glow", hideFlags = HideFlags.HideAndDontSave };
        particleMaterial = new Material(material) {
            name = "Electric Gate Spark Glow", hideFlags = HideFlags.HideAndDontSave };
        if (supported)
        {
            material.SetFloat("_CoreFraction", .15f);
            particleMaterial.SetFloat("_Radial", 1f);
            particleMaterial.SetFloat("_CoreFraction", .25f);
        }
    }
    private static string Key(ElectricGatePillar a, ElectricGatePillar b)
    {
        int x = a.GetInstanceID(), y = b.GetInstanceID();
        return x < y ? x + ":" + y : y + ":" + x;
    }
    public static bool Unobstructed(ElectricGatePillar a, ElectricGatePillar b)
    {
        // Ignore characters, triggers and floor/decor sprites; physical walls/closed doors block links.
        int count = Physics2D.LinecastNonAlloc(a.transform.position, b.transform.position, VisibilityHits);
        // Avoid truncating dense scenes, while keeping the common path allocation-free.
        var hits = count == VisibilityHits.Length ? Physics2D.LinecastAll(a.transform.position, b.transform.position) : VisibilityHits;
        if (hits != VisibilityHits) count = hits.Length;
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.isTrigger || c.transform.IsChildOf(a.transform) || c.transform.IsChildOf(b.transform)
                || c.GetComponentInParent<ZeldaCharacterData>() != null) continue;
            if (c.gameObject.scene != a.gameObject.scene) continue;
            if (c.gameObject.layer == LayerMask.NameToLayer("Blocks") || c.gameObject.layer == LayerMask.NameToLayer("Hidden Blocks")
                || c.GetComponentInParent<DoorHingeInteraction>() != null || c.GetComponentInParent<WaterwayGate>() != null) return false;
        }
        // Streaming may temporarily disable whole walls. Their absence from Physics2D must not open a link.
        if (instance != null && instance.streamers != null)
            foreach (var wall in instance.sleepingWalls)
            {
                if (wall == null || !wall.enabled || wall.isTrigger || wall.gameObject.activeInHierarchy || wall.gameObject.scene != a.gameObject.scene) continue;
                bool suspended = false;
                foreach (var streamer in instance.streamers)
                    if (streamer != null && streamer.IsSuspendedByStreamingOrAncestor(wall.gameObject)) { suspended = true; break; }
                if (!suspended) continue;
                Vector2 start = wall.transform.InverseTransformPoint(a.transform.position), end = wall.transform.InverseTransformPoint(b.transform.position);
                if (ElectricGateRules.IntersectsBox(start, end - start, wall.offset - wall.size * .5f, wall.offset + wall.size * .5f)) return false;
            }
        return true;
    }
    private void Pair()
    {
        if (Time.time >= nextWallScan)
        {
            nextWallScan = Time.time + 2f; sleepingWalls.Clear(); streamers = FindObjectsOfType<CameraVisionObjectStreaming>();
            var scenes = new HashSet<int>();
            foreach (var pillar in Pillars)
            {
                if (pillar == null || !scenes.Add(pillar.gameObject.scene.handle)) continue;
                foreach (var root in pillar.gameObject.scene.GetRootGameObjects())
                    foreach (var wall in root.GetComponentsInChildren<BoxCollider2D>(true))
                        if (wall.gameObject.layer == LayerMask.NameToLayer("Blocks") || wall.gameObject.layer == LayerMask.NameToLayer("Hidden Blocks")) sleepingWalls.Add(wall);
            }
        }
        wanted.Clear();
        foreach (var a in Pillars)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            ElectricGatePillar closest = null;
            float best = ElectricGateRules.Range * ElectricGateRules.Range + .0001f;
            foreach (var b in Pillars)
            {
                if (b == null || b == a || !b.isActiveAndEnabled || b.gameObject.scene != a.gameObject.scene) continue;
                float distance = ((Vector2)(a.transform.position - b.transform.position)).sqrMagnitude;
                if (!ElectricGateRules.NearerCandidate(distance, best, b.GetInstanceID(), closest != null ? closest.GetInstanceID() : int.MaxValue)) continue;
                if (!Unobstructed(a, b)) continue;
                closest = b; best = distance;
            }
            if (closest == null) continue;
            string key = Key(a, closest);
            wanted.Add(key);
            if (!fields.ContainsKey(key)) fields.Add(key, new Field(a, closest, material, particleMaterial));
        }
        obsolete.Clear();
        foreach (var pair in fields) if (!wanted.Contains(pair.Key)) obsolete.Add(pair.Key);
        foreach (string key in obsolete) { fields[key].Dispose(true); fields.Remove(key); }
    }
    private void FixedUpdate()
    {
        if (GameSaveSystem.IsLoading) return;
        if (Time.time >= nextPairing) { nextPairing = Time.time + .15f; Pair(); }
        // Death creates a new registered ghost synchronously. Never enumerate the live registry during damage.
        characters.Clear(); characters.AddRange(ZeldaRuntimeRegistry.Movers);
        foreach (var field in fields.Values) field.Tick(characters);
        // Publish once after ALL energy transactions, so a final charge consumed by
        // another link also turns both ends of this link dark in this physics tick.
        foreach (var pillar in Pillars) if (pillar != null) pillar.ResetSignal();
        foreach (var field in fields.Values) field.PublishSignal();
    }
    public static Vector2 ConstrainGhost(ZeldaCharacterData data, Vector2 start, Vector2 delta, float radius)
    {
        if (instance == null || data == null || !data.IsGhostLike) return delta;
        foreach (var field in instance.fields.Values)
            if (field.Active && field.A.gameObject.scene == ZeldaRuntimeRegistry.GetGameplayScene(data.gameObject))
                delta = ElectricGateRules.BlockGhost(start, delta, field.Start, field.End, radius);
        return delta;
    }
    public static void ReboundDeathGhost(ZeldaCharacterData dead, ZeldaFourWayMover ghost)
    {
        if (ghost == null || struckCharacter != dead) return;
        Vector2 from = ghost.transform.position, displacement = reboundPosition - from;
        var hit = Physics2D.CircleCast(from, .12f, displacement.normalized, displacement.magnitude,
            LayerMask.GetMask("Blocks", "Hidden Blocks"));
        if (hit.collider != null) reboundPosition = from + displacement.normalized * Mathf.Max(0, hit.distance - .03f);
        ghost.transform.position = new Vector3(reboundPosition.x, reboundPosition.y, ghost.transform.position.z);
        var body = ghost.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = reboundPosition; body.velocity = Vector2.zero; }
    }
    private void OnDestroy()
    {
        foreach (var field in fields.Values) field.Dispose(); fields.Clear();
        if (material != null) Destroy(material);
        if (particleMaterial != null) Destroy(particleMaterial);
        if (instance == this) instance = null;
    }

    private sealed class Field
    {
        public readonly ElectricGatePillar A, B;
        public Vector2 Start => A.transform.position;
        public Vector2 End => B.transform.position;
        public bool Active => A != null && B != null && A.isActiveAndEnabled && B.isActiveAndEnabled && A.Powered && B.Powered
            && Vector2.Distance(Start, End) <= ElectricGateRules.Range && Unobstructed(A, B);
        private readonly GameObject root;
        private readonly ParticleSystem sparks;
        private readonly LineRenderer[] strands = new LineRenderer[3], arcs = new LineRenderer[ElectricGateRules.ArcCount];
        private readonly Dictionary<ZeldaCharacterData, Vector2> previous = new Dictionary<ZeldaCharacterData, Vector2>();
        private readonly HashSet<ZeldaCharacterData> touching = new HashSet<ZeldaCharacterData>();
        private readonly List<ZeldaCharacterData> stale = new List<ZeldaCharacterData>();
        private float nextFlicker;
        private readonly float[] arcUntil = new float[2];
        private readonly Vector2[] arcPoints = new Vector2[2];
        private struct PendingStrike
        {
            public ZeldaCharacterData target;
            public Vector2 entryPosition;
            public float dueAt;
        }
        private readonly List<PendingStrike> pendingStrikes = new List<PendingStrike>();
        private readonly HashSet<ZeldaCharacterData> pendingTargets = new HashSet<ZeldaCharacterData>();
        private readonly bool softGlow;
        private readonly ElectricGateAudio audio;
        private ElectricGateRules.SignalState signal;
        private bool previouslyPassable;
        private string mapPointId;
        private float nextMapVisibilityCheck;
        public Field(ElectricGatePillar a, ElectricGatePillar b, Material material, Material particleMaterial)
        {
            A = a; B = b;
            root = new GameObject("Electric Current (0.5 wide)") { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            root.transform.SetParent(a.transform, false);
            audio = root.AddComponent<ElectricGateAudio>();
            audio.Initialize(a.transform, b.transform);
            softGlow = material.HasProperty("_CoreFraction");
            // Wider geometry contains only the soft halo; bright cores retain their previous width.
            for (int i = 0; i < strands.Length; i++) strands[i] = Line("Current", material, softGlow ? .18f : .028f, 0);
            for (int i = 0; i < arcs.Length; i++) arcs[i] = Line("Discharge", material, softGlow ? .6f : .09f, 10);
            var emitter = new GameObject("Gate Sparks", typeof(ParticleSystem)) { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            emitter.transform.SetParent(root.transform, false); sparks = emitter.GetComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main; main.playOnAwake = false; main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = .16f; main.startSpeed = 0; main.startSize = .045f; main.maxParticles = 256;
            var emission = sparks.emission; emission.enabled = false;
            var shape = sparks.shape; shape.enabled = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.7f, .4f), new GradientAlphaKey(0, 1) });
            var lifetimeColor = sparks.colorOverLifetime; lifetimeColor.enabled = true; lifetimeColor.color = fade;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = particleMaterial; renderer.sortingOrder = 1;
            sparks.Play();
        }
        private LineRenderer Line(string label, Material material, float width, int order)
        {
            var go = new GameObject(label, typeof(LineRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            go.transform.SetParent(root.transform, false);
            var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
            line.startWidth = line.endWidth = width; line.startColor = new Color(1,.14f,.08f,.9f); line.endColor = new Color(1,.38f,.25f,.85f);
            line.sortingOrder = order; line.enabled = false; return line;
        }
        private bool Allows(ZeldaCharacterData data)
        {
            // If both ends have panels, both policies must grant access. A bare receiver adds no restriction.
            if (A.HasPanel || B.HasPanel) return (!A.HasPanel || A.Allows(data)) && (!B.HasPanel || B.Allows(data));
            return A.Allows(data) && B.Allows(data);
        }
        public void Tick(List<ZeldaFourWayMover> movers)
        {
            if (A == null || B == null || !A.isActiveAndEnabled || !B.isActiveAndEnabled)
            {
                CancelStrikes();
                if (audio != null) audio.SetState(false, false);
                return;
            }
            // The whole burst was paid on entry. Its second pair still completes
            // if that transaction used the battery's last remaining charge.
            AdvancePendingStrikes();
            bool active = Active, warning = false, allowedNearby = false;
            if (!active) touching.Clear();
            foreach (var mover in movers)
            {
                if (mover == null || !mover.gameObject.activeInHierarchy || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != A.gameObject.scene) continue;
                var data = mover.GetComponent<ZeldaCharacterData>();
                if (data == null || data.IsDead) continue;
                Vector2 current = mover.transform.position;
                var collider = mover.GetComponent<BoxCollider2D>();
                float radius = collider != null ? Mathf.Min(.65f, Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y)) : .15f;
                if (!previous.TryGetValue(data, out Vector2 old)) old = current;
                previous[data] = current;
                if (!active || data.IsGhostLike) { touching.Remove(data); continue; }
                bool nearby = ElectricGateRules.Sweep(current, Vector2.zero, Start, End, radius + .8f, out _);
                if (Allows(data))
                {
                    allowedNearby |= nearby;
                    touching.Remove(data);
                    continue; // Permission/individual passage never adds warning particles or deals damage.
                }
                bool inside = ElectricGateRules.Sweep(current, Vector2.zero, Start, End, radius, out _);
                warning |= nearby;
                bool crossed = ElectricGateRules.Sweep(old, current - old, Start, End, radius, out _);
                if (!touching.Contains(data) && crossed && Active)
                {
                    // Remember reentry even while a burst is pending, otherwise
                    // completing that burst would immediately start another one.
                    touching.Add(data);
                    if (!pendingTargets.Contains(data) && BatterySocket.TryConsumeForMechanisms(A.HasPanel ? A : null, B.HasPanel ? B : null)) Strike(data, old, current);
                    nextFlicker = 0f; // Draw new discharge paths immediately, never flash the previous target's arcs.
                    active = Active; // Last unit immediately disables this field for subsequent entrants.
                }
                if (!inside) touching.Remove(data);
            }
            stale.Clear();
            foreach (var entry in previous) if (entry.Key == null || entry.Key.IsDead || !entry.Key.gameObject.activeInHierarchy) stale.Add(entry.Key);
            foreach (var data in stale) { previous.Remove(data); touching.Remove(data); }
            signal = ElectricGateRules.ResolveSignal(active, allowedNearby, warning);
            bool passable = signal == ElectricGateRules.SignalState.Passage;
            warning = signal == ElectricGateRules.SignalState.Warning;
            if (passable != previouslyPassable)
            {
                // Do not leave old red particles over a newly blue, safe passage.
                sparks.Clear(); nextFlicker = 0f; previouslyPassable = passable;
            }
            for (int i = 0; i < strands.Length; i++)
            {
                strands[i].enabled = active && (i == 0 || warning);
                strands[i].startColor = passable ? new Color(.12f,.72f,1f,.9f) : new Color(1,.14f,.08f,.9f);
                strands[i].endColor = passable ? new Color(.4f,.88f,1f,.85f) : new Color(1,.38f,.25f,.85f);
            }
            for (int i = 0; i < arcs.Length; i++)
            {
                float until = arcUntil[i / ElectricGateRules.ArcsPerPair];
                float dischargeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((until - Time.time) / ElectricGateRules.ArcLifetime));
                arcs[i].enabled = Time.time < until;
                arcs[i].startColor = new Color(1,.18f,.1f,dischargeFade);
                arcs[i].endColor = new Color(1,.4f,.25f,dischargeFade);
            }
            if (audio != null) audio.SetState(active, warning);
            if (!active) sparks.Clear();
            if (Time.time < nextFlicker) return;
            nextFlicker = Time.time + .045f;
            if (active)
            {
                for (int i = 0; i < strands.Length; i++) if (strands[i].enabled) Zigzag(strands[i], Start, End, .22f);
                int count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(Start, End) * (warning ? 2f : .45f)), 2, 32);
                for (int i = 0; i < count; i++)
                {
                    float size = warning ? .065f : .045f;
                    float halfSpan = (ElectricGateRules.Width - size) * .5f;
                    Vector2 position = Vector2.Lerp(Start, End, Random.value) + ElectricGateRules.Normal(Start, End) * Random.Range(-halfSpan,halfSpan);
                    var emit = new ParticleSystem.EmitParams { position = new Vector3(position.x, position.y, A.transform.position.z),
                        startColor = passable ? new Color(.12f, Random.Range(.68f,.85f), 1f) : new Color(1, Random.Range(.14f,.45f), .1f),
                        startSize = size * (softGlow ? 4f : 1f), startLifetime = .13f };
                    sparks.Emit(emit, 1);
                }
            }
            for (int i = 0; i < arcs.Length; i++)
                if (arcs[i].enabled)
                    Zigzag(arcs[i], i % 2 == 0 ? Start : End, arcPoints[i / ElectricGateRules.ArcsPerPair], .12f, i < 2 ? -.14f : .14f);
        }
        public void PublishSignal()
        {
            if (A == null || B == null || !A.isActiveAndEnabled || !B.isActiveAndEnabled) return;
            var state = A.Powered && B.Powered ? signal : ElectricGateRules.SignalState.Off;
            A.ReportSignal(state); B.ReportSignal(state);
            UpdateMapMarker(state != ElectricGateRules.SignalState.Off);
        }
        private void UpdateMapMarker(bool powered)
        {
            if (mapPointId == null)
            {
                string a = SceneTravelStateManager.GetMapObjectId(A.transform), b = SceneTravelStateManager.GetMapObjectId(B.transform);
                mapPointId = A.gameObject.scene.name + ":electric-field:" +
                    (string.CompareOrdinal(a,b) < 0 ? a + "|" + b : b + "|" + a);
            }
            var manager = MapPointOfInterestManager.GetOrCreate();
            bool seen = false;
            if (powered && !manager.IsDiscovered(mapPointId) && Time.unscaledTime >= nextMapVisibilityCheck)
            {
                nextMapVisibilityCheck = Time.unscaledTime + .2f;
                seen = MapPointOfInterest.IsElectricFieldVisible(A, Start, End);
            }
            manager.UpdateElectricField(mapPointId, A.gameObject.scene.name, Start, End, powered, seen);
        }
        private void Strike(ZeldaCharacterData data, Vector2 old, Vector2 current)
        {
            // Register before dealing damage: hit/death callbacks cannot enqueue
            // the same body a second time while this paid burst is still pending.
            pendingTargets.Add(data);
            pendingStrikes.Add(new PendingStrike { target = data, entryPosition = old, dueAt = Time.time + ElectricGateRules.PairDelay });
            ReleasePair(data, old, current, 0, ElectricGateRules.FirstPairDamage);
        }
        private void AdvancePendingStrikes()
        {
            for (int i = pendingStrikes.Count - 1; i >= 0; i--)
            {
                var pending = pendingStrikes[i];
                var target = pending.target;
                bool valid = target != null && !target.IsDead && !target.IsGhostLike && target.gameObject.activeInHierarchy &&
                    ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) == A.gameObject.scene;
                if (valid && Time.time < pending.dueAt) continue;
                pendingStrikes.RemoveAt(i); pendingTargets.Remove(target);
                if (valid) ReleasePair(target, pending.entryPosition, target.transform.position, 1, ElectricGateRules.SecondPairDamage);
            }
        }
        private void ReleasePair(ZeldaCharacterData data, Vector2 old, Vector2 current, int pair, int damage)
        {
            arcPoints[pair] = current; arcUntil[pair] = Time.time + ElectricGateRules.ArcLifetime;
            nextFlicker = 0f;
            if (audio != null) audio.PlayStrike(current);
            DamagePair(data, old, current, damage);
        }
        private void DamagePair(ZeldaCharacterData data, Vector2 old, Vector2 current, int damage)
        {
            Vector2 normal = ElectricGateRules.Normal(Start, End);
            float side = Vector2.Dot(old - Start, normal);
            if (Mathf.Abs(side) < .001f) side = -Vector2.Dot(current - old, normal);
            float sign = side < 0 ? -1 : 1;
            Vector2 axis = (End - Start).normalized;
            Vector2 onLine = Start + axis * Mathf.Clamp(Vector2.Dot(current - Start, axis), 0, Vector2.Distance(Start, End));
            struckCharacter = data; reboundPosition = onLine + normal * sign * .95f;
            try
            {
                // Preserve individual hit/shield handling and ghost rebound in
                // BOTH pairs, without ever charging again or attacking a corpse.
                for (int i = 0; i < ElectricGateRules.ArcsPerPair && data != null && !data.IsDead; i++)
                    data.TakeDamage(damage, null);
            }
            finally { struckCharacter = null; }
        }
        private void Zigzag(LineRenderer line, Vector2 a, Vector2 b, float spread, float bow = 0f)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a,b) / .28f) + 1, bow == 0f ? 2 : 3, 48);
            line.positionCount = count; Vector2 normal = ElectricGateRules.Normal(a,b);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count-1);
                Vector2 p = Vector2.Lerp(a,b,t) + normal * (i == 0 || i == count-1 ? 0 : Random.Range(-spread,spread) + bow * 4f * t * (1f-t));
                line.SetPosition(i, new Vector3(p.x,p.y,A.transform.position.z));
            }
        }
        private void CancelStrikes()
        {
            pendingStrikes.Clear(); pendingTargets.Clear();
            for (int i = 0; i < arcUntil.Length; i++) arcUntil[i] = 0f;
            foreach (var arc in arcs) if (arc != null) arc.enabled = false;
        }
        public void Dispose(bool removedByPairing = false)
        {
            // Scene unload retains discovered geometry. A real link change in a
            // live scene (e.g. closing a blocking door) removes only that link.
            if (removedByPairing && A != null && B != null && A.isActiveAndEnabled && B.isActiveAndEnabled &&
                A.gameObject.scene.isLoaded && mapPointId != null && MapPointOfInterestManager.Instance != null)
                MapPointOfInterestManager.Instance.SetPointVisible(mapPointId, false);
            CancelStrikes(); if (root != null) { root.SetActive(false); Object.Destroy(root); }
        }
    }
}
