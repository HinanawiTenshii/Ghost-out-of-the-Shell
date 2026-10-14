using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// Stage this file in Assets/Editor of an isolated project together with the unmodified
// production scripts/prefab and ProjectSettings/TagManager.asset. Never saves a scene.
// -executeMethod SandwormPrototypeSmoke.Run: deterministic mechanics + real PlayMode pause.
// -executeMethod SandwormPrototypeSmoke.RunGpu: mechanics, native GPU preview and PNG frames.
// RunGpu requires a graphics device (do not pass -nographics).
[InitializeOnLoad]
public static class SandwormPrototypeSmoke
{
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    const string Pending = "SandwormPrototypeSmoke.Pending";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<Object> Owned = new List<Object>();
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static float originalTimeScale;

    static SandwormPrototypeSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                checks = 0;
                originalTimeScale = Time.timeScale;
                test = TestPlayMode();
            }
            if (test == null || test.MoveNext()) return;
            test = null;
            Cleanup();
            Time.timeScale = originalTimeScale;
            Debug.Log("SANDWORM_PROTOTYPE_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            test = null;
            Cleanup();
            Time.timeScale = originalTimeScale;
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception("Sandworm: " + label);
    }

    static T Own<T>(T item) where T : Object { Owned.Add(item); return item; }
    static void Cleanup()
    {
        RenderTexture.active = null;
        for (int i = Owned.Count - 1; i >= 0; i--)
            if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
        Owned.Clear();
    }

    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static void Validate(object owner) => owner.GetType().GetMethod("OnValidate", Flags).Invoke(owner, null);
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static SandwormBossPrototype Worm()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "production prefab imports at original path");
        var root = Own(Object.Instantiate(prefab));
        root.name = "Sandworm smoke fixture";
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        var worm = root.GetComponent<SandwormBossPrototype>();
        Check(worm != null, "production controller bound");
        // Canonical 1x movement fixture. The shipped 3x world-space dimensions
        // and compensated world speed are covered by SandwormPixelVisualSmoke.
        Set(worm, "wanderSize", new Vector2(24, 18));
        Set(worm, "minDestinationDistance", 3f);
        Set(worm, "moveSpeed", 3f);
        Set(worm, "arrivalDistance", .2f);
        worm.SetMovementEnabled(true);
        worm.ResetPose();
        return worm;
    }

    static Transform[] Parts(SandwormBossPrototype worm)
    {
        var parts = new List<Transform> { worm.Head };
        foreach (var part in worm.BodySegments) parts.Add(part);
        parts.Add(worm.Tail);
        return parts.ToArray();
    }

    static Vector2[] Positions(SandwormBossPrototype worm)
    {
        var parts = Parts(worm);
        var points = new Vector2[parts.Length];
        for (int i = 0; i < parts.Length; i++) points[i] = parts[i].position;
        return points;
    }

    static void Unchanged(SandwormBossPrototype worm, Vector2[] before, string label)
    {
        var after = Positions(worm);
        Check(before.Length == after.Length, label + ": part count");
        for (int i = 0; i < before.Length; i++)
            Check(Vector2.Distance(before[i], after[i]) < .00001f, label + ": part " + i);
    }

    static GameObject Block(Vector2 center, Vector2 size, int layer = -1)
    {
        if (layer < 0) layer = LayerMask.NameToLayer("Blocks");
        Check(layer >= 0, "Blocks layer copied to isolated project");
        var obstacle = Own(new GameObject("Smoke obstacle"));
        obstacle.layer = layer;
        obstacle.transform.position = center;
        obstacle.AddComponent<BoxCollider2D>().size = size;
        Physics2D.SyncTransforms();
        return obstacle;
    }

    static void TestStructure()
    {
        var worm = Worm();
        var parts = Parts(worm);
        Check(parts.Length == 8, "head + six independently editable body sections + tail");
        var distinct = new HashSet<Transform>();
        float spacing = Get<float>(worm, "segmentSpacing");
        for (int i = 0; i < parts.Length; i++)
        {
            Check(parts[i] != null && distinct.Add(parts[i]), "part reference distinct " + i);
            Check(parts[i].IsChildOf(worm.transform), "part belongs to prefab " + i);
            Check(parts[i].GetComponentsInChildren<Renderer>(true).Length > 0, "part has independent visual " + i);
            for (int j = 0; j < i; j++)
                Check(!parts[i].IsChildOf(parts[j]) && !parts[j].IsChildOf(parts[i]), "parts are not transform-parented into a rigid chain");
            if (i > 0)
            {
                Check(Mathf.Abs(Vector2.Distance(parts[i - 1].position, parts[i].position) - spacing) < .01f, "initial segment spacing " + i);
                Check(parts[i].position.x < parts[i - 1].position.x, "reset spine trails along negative x " + i);
            }
        }
        Check(Vector2.Dot(worm.Head.right, Vector2.right) > .999f, "head's authored forward axis is +x");
        Check(worm.GetComponentsInChildren<Collider2D>(true).Length == 8, "eight authored 2D part colliders");
        Check(worm.GetComponentsInChildren<Collider>(true).Length == 0, "entire hierarchy has no 3D collider");
        Check(worm.GetComponentsInChildren<Rigidbody2D>(true).Length == 1, "one shared kinematic 2D rigidbody");
        Check(worm.GetComponentsInChildren<Rigidbody>(true).Length == 0, "entire hierarchy has no 3D rigidbody");
        foreach (var component in worm.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Check(component != null, "no missing scripts");
            string name = component.GetType().Name;
            Check(!name.Contains("Attack") && !name.Contains("Hitbox"), "no automatic attack component: " + name);
        }
        Check(worm.GetComponents<ZeldaCharacterData>().Length == 1, "one shared character-data component on the root");
        var before = Positions(worm);
        worm.enabled = false;
        Unchanged(worm, before, "disabling controller retains authored pose");
        worm.enabled = true;
        var reenabled = Parts(worm);
        for (int i = 0; i < parts.Length; i++) Check(parts[i] == reenabled[i], "reenabling retains independent part instance " + i);
        worm.gameObject.SetActive(false);
        Check(Parts(worm).Length == 8, "deactivation does not delete body references");
        worm.gameObject.SetActive(true);
        Check(Parts(worm).Length == 8, "reactivation does not duplicate parts");
        Object.DestroyImmediate(worm.gameObject);
    }

    static void TestDestinations()
    {
        var worm = Worm();
        Check(!worm.IsDestinationAllowed(new Vector2(13, 0)), "outside x bound rejected");
        Check(!worm.IsDestinationAllowed(new Vector2(0, 10)), "outside y bound rejected");
        Check(!worm.TrySetDestination(new Vector2(-13, 0)), "explicit outside destination rejected");
        Check(worm.TrySetDestination(new Vector2(8, 0)), "unobstructed destination accepted");
        var wall = Block(new Vector2(4, 0), Vector2.one);
        Check(!worm.IsDestinationAllowed(new Vector2(4, 0)), "point inside Blocks rejected");
        Check(!worm.IsDestinationAllowed(new Vector2(4, 1.1f)), "clearance rejects a nearby point outside the collider");
        Check(worm.IsDestinationAllowed(new Vector2(4, 1.6f)), "point beyond configured clearance accepted");
        Check(!worm.TrySetDestination(new Vector2(8, 0)), "clear endpoint beyond intervening Blocks rejected");
        Check(!worm.TrySetDestination(new Vector2(8, 1.8f)), "swept clearance catches near-miss path past obstacle corner");
        wall.GetComponent<Collider2D>().enabled = false;
        Physics2D.SyncTransforms();
        Check(worm.TrySetDestination(new Vector2(8, 0)), "disabled Blocks collider is not navigation geometry");
        wall.GetComponent<Collider2D>().enabled = true;
        wall.GetComponent<Collider2D>().isTrigger = true;
        Physics2D.SyncTransforms();
        Check(worm.TrySetDestination(new Vector2(8, 0)), "Blocks trigger does not stop navigation");
        wall.GetComponent<Collider2D>().isTrigger = false;
        wall.SetActive(false);
        Physics2D.SyncTransforms();
        Check(worm.TrySetDestination(new Vector2(8, 0)), "deliberately inactive Blocks object does not stop navigation");
        wall.SetActive(true);
        Physics2D.SyncTransforms();
        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(9361);
        try
        {
            for (int i = 0; i < 200; i++)
            {
                // Curves now also need turning room and a feasible next leg. A
                // rejected sample batch may retry, without relaxing wall clearance.
                bool found = false;
                for (int retry = 0; retry < 4 && !found; retry++) found = worm.TryPickDestination();
                Check(found, "bounded random sampling/retry finds a traversable curve " + i);
                Vector2 point = worm.CurrentDestination;
                Check(Mathf.Abs(point.x) <= 12.0001f && Mathf.Abs(point.y) <= 9.0001f, "sample inside configured rectangle");
                Check(worm.HasDestination && worm.IsDestinationAllowed(point), "sample has endpoint clearance");
                Check(Vector2.Distance(worm.HeadPosition, point) >= Get<float>(worm, "minDestinationDistance") - .001f, "sample respects strict minimum travel distance with no short-distance fallback");
                Check(Vector2.Distance(wall.GetComponent<Collider2D>().ClosestPoint(point), point) >= Get<float>(worm, "obstacleClearance") - .001f, "sample outside expanded obstacle");
            }
        }
        finally { UnityEngine.Random.state = randomState; }
        Object.DestroyImmediate(wall);
        var nonBlock = Block(new Vector2(4, 0), Vector2.one, 0);
        Check(worm.TrySetDestination(new Vector2(8, 0)), "non-Blocks geometry does not accidentally become navigation wall");
        Object.DestroyImmediate(nonBlock);
        if (Application.isPlaying)
        {
            var foreignScene = SceneManager.CreateScene("Sandworm foreign-scene obstacle");
            var foreign = Block(new Vector2(4, 0), Vector2.one);
            SceneManager.MoveGameObjectToScene(foreign, foreignScene);
            Physics2D.SyncTransforms();
            Check(worm.TrySetDestination(new Vector2(8, 0)), "other loaded scene's Blocks do not obstruct this worm");
            Object.DestroyImmediate(foreign);
            SceneManager.UnloadSceneAsync(foreignScene);
        }
        Set(worm, "wanderCenterOffset", new Vector2(3, -2));
        Validate(worm);
        Check(!worm.IsDestinationAllowed(new Vector2(-10, 0)), "offset rectangle changes destination domain");
        Check(worm.IsDestinationAllowed(new Vector2(14, -2)), "offset rectangle supports shifted positive boundary");
        Set(worm, "wanderCenterOffset", Vector2.zero);
        Validate(worm);
        var enclosure = Block(Vector2.zero, Vector2.one * 100);
        var elapsed = Stopwatch.StartNew();
        Check(!worm.TryPickDestination(), "fully blocked area returns failure after bounded attempts");
        Check(!worm.HasDestination, "blocked area does not retain a stale destination");
        var before = Positions(worm);
        for (int i = 0; i < 120; i++) worm.Advance(.02f);
        Unchanged(worm, before, "fully blocked area remains stationary");
        Check(elapsed.Elapsed.TotalSeconds < 5, "blocked retries finish promptly, not an unbounded search");
        Object.DestroyImmediate(enclosure);
        Object.DestroyImmediate(worm.gameObject);
    }

    static void StepChecked(SandwormBossPrototype worm, float dt)
    {
        Vector2[] before = Positions(worm);
        float angle = worm.Head.eulerAngles.z;
        worm.Advance(dt);
        Vector2[] after = Positions(worm);
        float spacing = Get<float>(worm, "segmentSpacing");
        float speed = Get<float>(worm, "moveSpeed");
        Check(Mathf.Abs(Mathf.DeltaAngle(angle, worm.Head.eulerAngles.z)) <= Get<float>(worm, "turnSpeed") * dt + .1f, "bounded heading turn per frame");
        for (int i = 0; i < after.Length; i++)
        {
            Check(Finite(after[i].x) && Finite(after[i].y), "finite part coordinates");
            Check(Vector2.Distance(before[i], after[i]) <= speed * dt + .035f, "no transform teleport or follower snap: part " + i);
            if (i == 0) continue;
            float distance = Vector2.Distance(after[i - 1], after[i]);
            Check(distance >= spacing * .62f && distance <= spacing * 1.07f, "arc-length followers neither pile up nor separate: part " + i + " gap=" + distance);
        }
    }

    static void TestFollowing()
    {
        var worm = Worm();
        var initial = Positions(worm);
        Check(worm.TrySetDestination(new Vector2(10, 0)), "straight travel target");
        worm.Advance(0);
        worm.Advance(-1);
        Unchanged(worm, initial, "nonpositive delta time");
        worm.SetMovementEnabled(false);
        worm.Advance(.2f);
        Unchanged(worm, initial, "movement toggle pause");
        worm.SetMovementEnabled(true);
        for (int i = 0; i < 100; i++) StepChecked(worm, .02f);
        Check(worm.HeadPosition.x > 5.8f && Mathf.Abs(worm.HeadPosition.y) < .01f, "head advances along authored +x");
        var straight = Positions(worm);
        Check(straight[7].x > initial[7].x + 5.8f, "tail advances using trail history");
        Check(straight[0].x - straight[7].x > 5.7f, "tail remains behind head rather than collapsing onto target");
        Check(worm.TrySetDestination(new Vector2(7, 7)), "curved travel target");
        Unchanged(worm, straight, "changing target never snaps pose");
        for (int i = 0; i < 30; i++) StepChecked(worm, .02f);
        var turning = Positions(worm);
        Check(turning[0].y > .25f, "head initiates bend");
        Check(Mathf.Abs(turning[7].y) < .025f, "tail remains on earlier straight path during initial turn");
        Check(turning[0].y > turning[4].y + .2f, "body turns later than the head");
        for (int i = 0; i < 65; i++) StepChecked(worm, .02f);
        Check(Positions(worm)[4].y > .4f, "middle body eventually follows the same curve");
        Check(worm.TrailPointCount > 2 && worm.TrailPointCount < 4096, "trail has useful bounded history");
        Object.DestroyImmediate(worm.gameObject);

        worm = Worm();
        Vector2 target = new Vector2(5, 0);
        Check(worm.TrySetDestination(target), "arrival target accepted");
        bool reached = false, repicked = false;
        for (int i = 0; i < 350; i++)
        {
            worm.Advance(.02f);
            if (Vector2.Distance(worm.HeadPosition, target) < .3f) reached = true;
            if (reached && worm.HasDestination && Vector2.Distance(worm.CurrentDestination, target) > .5f) { repicked = true; break; }
        }
        Check(reached && repicked, "arrival selects another destination and continues wandering");
        for (int i = 0; i < 1800; i++) worm.Advance(.02f);
        Check(worm.TrailPointCount < 4096, "long wandering prunes unused history");
        Object.DestroyImmediate(worm.gameObject);

        worm = Worm();
        Check(worm.TrySetDestination(new Vector2(10, 0)), "runtime spacing-edit setup");
        for (int i = 0; i < 30; i++) worm.Advance(.02f);
        Set(worm, "segmentSpacing", 2f);
        Validate(worm);
        worm.Advance(.02f);
        var resized = Positions(worm);
        for (int i = 1; i < resized.Length; i++)
            Check(Vector2.Distance(resized[i - 1], resized[i]) > 1.5f, "runtime spacing growth preserves enough trail for follower " + i);
        Object.DestroyImmediate(worm.gameObject);
    }

    static void TestSanitization()
    {
        var worm = Worm();
        Set(worm, "wanderSize", new Vector2(-24, -18));
        foreach (string name in new[] { "segmentSpacing", "moveSpeed", "turnSpeed", "obstacleClearance", "minDestinationDistance" }) Set(worm, name, -1f);
        Set(worm, "destinationAttempts", -20);
        Validate(worm);
        Vector2 size = Get<Vector2>(worm, "wanderSize");
        Check(size.x > 0 && size.y > 0, "negative area size sanitized to a positive rectangle");
        Check(Get<float>(worm, "segmentSpacing") > 0, "negative segment spacing sanitized");
        foreach (string name in new[] { "moveSpeed", "turnSpeed", "obstacleClearance", "minDestinationDistance" })
            Check(Finite(Get<float>(worm, name)) && Get<float>(worm, name) >= 0, "negative parameter sanitized: " + name);
        Check(Get<int>(worm, "destinationAttempts") > 0, "negative sample count sanitized");
        worm.ResetPose();
        worm.Advance(.02f);
        foreach (Vector2 point in Positions(worm)) Check(Finite(point.x) && Finite(point.y), "sanitized configuration has finite pose");
        Object.DestroyImmediate(worm.gameObject);
    }

    static void TestCore()
    {
        TestStructure();
        TestDestinations();
        TestFollowing();
        TestTransformsAndStreaming();
        TestSanitization();
    }

    static void TestTransformsAndStreaming()
    {
        var worm = Worm();
        worm.transform.position = new Vector3(30, 4, 0);
        worm.transform.rotation = Quaternion.Euler(0, 0, 30);
        worm.transform.localScale = new Vector3(-1.5f, .8f, 1);
        worm.ResetPose();
        Vector2 goal = worm.transform.TransformPoint(new Vector3(7, 0, 0));
        Vector2 start = worm.HeadPosition;
        Check(worm.TrySetDestination(goal), "rotated mirrored nonuniform root accepts local goal");
        worm.Advance(.2f);
        Vector2 direction = (goal - start).normalized;
        Check(Vector2.Dot(worm.HeadPosition - start, direction) > .6f, "mirrored root moves along visual +x");
        Check(Vector2.Dot(worm.Head.TransformVector(Vector3.right).normalized, direction) > .99f, "mirrored visual faces actual travel");
        var before = Positions(worm);
        worm.transform.position += new Vector3(3, 7, 0);
        worm.Advance(.02f);
        var after = Positions(worm);
        for (int i = 0; i < before.Length; i++)
            Check(Vector2.Distance(after[i], before[i] + new Vector2(3, 7)) < .12f, "root teleport keeps trail and part " + i);
        Object.DestroyImmediate(worm.gameObject);

        var cameraRoot = Own(new GameObject("Streaming query fixture"));
        var streamer = cameraRoot.AddComponent<CameraVisionObjectStreaming>();
        var wall = Block(new Vector2(4, 0), Vector2.one);
        var managedType = typeof(CameraVisionObjectStreaming).GetNestedType("ManagedObject", BindingFlags.NonPublic);
        var record = Activator.CreateInstance(managedType);
        Set(record, "gameObject", wall);
        Set(record, "suspendedByManager", true);
        ((IDictionary)Get<object>(streamer, "managedLookup")).Add(wall, record);
        wall.SetActive(false);
        var query = new SandwormObstacleQuery();
        query.Refresh(wall.scene);
        Check(!query.IsPointClear(new Vector2(4, 0), .8f), "streamed-out wall still excludes destinations");
        Check(!query.IsClear(Vector2.zero, new Vector2(8, 0), .8f), "streamed-out wall still excludes routes");
        Check(!wall.activeSelf, "navigation never wakes streamed-out wall");
        wall.GetComponent<Collider2D>().enabled = false;
        Check(query.IsClear(Vector2.zero, new Vector2(8, 0), .8f), "disabled collider stays nonblocking while streamed out");
        Object.DestroyImmediate(cameraRoot);
        Object.DestroyImmediate(wall);
    }

    static IEnumerator TestPlayMode()
    {
        Time.timeScale = 1;
        TestCore();
        var worm = Worm();
        Check(worm.TrySetDestination(new Vector2(10, 0)), "runtime automatic movement target");
        Time.timeScale = 0;
        yield return null;
        var paused = Positions(worm);
        for (int i = 0; i < 5; i++) yield return null;
        Unchanged(worm, paused, "actual Time.timeScale zero pauses runtime Update");
        Time.timeScale = 1;
        for (int i = 0; i < 60 && Vector2.Distance(worm.HeadPosition, paused[0]) < .001f; i++) yield return null;
        Check(Vector2.Distance(worm.HeadPosition, paused[0]) > .001f, "resuming actual scaled time resumes automatic movement");
        worm.SetMovementEnabled(false);
        var toggled = Positions(worm);
        for (int i = 0; i < 4; i++) yield return null;
        Unchanged(worm, toggled, "public movement toggle pauses actual Update");
        worm.enabled = false;
        var parts = Parts(worm);
        for (int i = 0; i < 3; i++) yield return null;
        for (int i = 0; i < parts.Length; i++) Check(parts[i] != null, "disabled runtime retains part " + i);
        Check(worm.GetComponentsInChildren<Collider2D>(true).Length == 8 && worm.GetComponentsInChildren<Rigidbody2D>(true).Length == 1, "runtime preserves the authored compound collider without adding physics components");
    }

    public static void RunGpu()
    {
        try
        {
            checks = 0;
            TestCore();
            CapturePreviews();
            Cleanup();
            Debug.Log("SANDWORM_PROTOTYPE_PASS checks=" + checks + " preview=Sandworm-preview.png frames=sandworm-frames");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void Label(string text, Vector2 position, float size, Color color)
    {
        var label = Own(new GameObject("Preview label: " + text));
        label.transform.position = new Vector3(position.x, position.y, -1);
        var mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
#if UNITY_2022_2_OR_NEWER
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        mesh.fontSize = 60;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.UpperLeft;
        mesh.color = color;
        var renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mesh.font.material;
        renderer.sortingOrder = 30000;
    }

    static void Line(string name, Vector2 from, Vector2 to, Color color, float width = .015f)
    {
        var root = Own(new GameObject(name));
        var line = root.AddComponent<LineRenderer>();
        line.sharedMaterial = Own(new Material(Shader.Find("Sprites/Default")));
        line.startColor = color; line.endColor = color;
        line.startWidth = width; line.endWidth = width;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(from.x, from.y, 1));
        line.SetPosition(1, new Vector3(to.x, to.y, 1));
        line.sortingOrder = -100;
    }

    static void Soldier(Vector2 position)
    {
        // Production ArabSoldierZeldaCharacterData.Front palette/16 PPU, without importing
        // unrelated character, combat and inventory dependencies into the smoke project.
        string[] rows = {
            "......HHHH......", ".....WHHWW......", "....HWWWWWWH....", ".....WFEFEW.....",
            ".....DFFFFD.....", "....RCCCCCCR....", "....ARCCCLLA....", "....LLRCASLL....",
            "....FFRCAAFF....", "....FFBCBBFF....", ".....TSTTST.....", ".....TSTTST.....",
            ".....KK..KK.....", ".....KK..KK.....", "................", "................"
        };
        var palette = new Dictionary<char, Color> {
            ['A'] = new Color(.40f,.45f,.47f), ['S'] = new Color(.24f,.29f,.31f), ['L'] = new Color(.57f,.60f,.59f),
            ['W'] = new Color(.68f,.65f,.56f), ['H'] = new Color(.85f,.81f,.69f), ['D'] = new Color(.43f,.41f,.35f),
            ['F'] = new Color(.69f,.46f,.29f), ['E'] = new Color(.06f,.05f,.04f), ['T'] = new Color(.52f,.47f,.34f),
            ['B'] = new Color(.28f,.19f,.10f), ['K'] = new Color(.22f,.14f,.08f), ['C'] = new Color(.57f,.59f,.60f),
            ['R'] = new Color(.34f,.37f,.39f), ['.'] = Color.clear
        };
        var texture = Own(new Texture2D(16, 16, TextureFormat.RGBA32, false));
        texture.filterMode = FilterMode.Point;
        var pixels = new Color[256];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) pixels[(15 - y) * 16 + x] = palette[rows[y][x]];
        texture.SetPixels(pixels); texture.Apply();
        var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .25f), 16));
        var root = Own(new GameObject("1x production soldier sprite reference"));
        root.transform.position = position;
        root.AddComponent<SpriteRenderer>().sprite = sprite;
    }

    static void NativePose(SandwormBossPrototype worm, int variant)
    {
        worm.ResetPose();
        Check(worm.TrySetDestination(new Vector2(10, 0)), "preview straight approach");
        for (int i = 0; i < 65; i++) worm.Advance(.02f);
        Check(worm.TrySetDestination(variant == 0 ? new Vector2(5, 7) : new Vector2(6, -7)), "preview curved destination");
        for (int i = 0; i < (variant == 0 ? 63 : 48); i++) worm.Advance(.02f);
        if (variant == 1)
        {
            Check(worm.TrySetDestination(new Vector2(11, -1)), "preview return curve");
            for (int i = 0; i < 32; i++) worm.Advance(.02f);
        }
        worm.SetMovementEnabled(false);
    }

    static void Capture(Camera camera, RenderTexture target, Texture2D texture)
    {
        camera.Render();
        RenderTexture.active = target;
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        RenderTexture.active = null;
    }

    static void CapturePreviews()
    {
        Color ink = new Color(.84f, .76f, .54f);
        Color secondary = new Color(.44f, .51f, .51f);
        for (int x = -12; x <= 12; x++) Line("1 unit grid", new Vector2(x, -5), new Vector2(x, 5), new Color(.10f, .14f, .25f));
        for (int y = -5; y <= 5; y++) Line("1 unit grid", new Vector2(-12, y), new Vector2(12, y), new Color(.10f, .14f, .25f));
        Line("Panel division", new Vector2(0, -4.8f), new Vector2(0, 4.5f), secondary);
        Label("SANDWORM / MOTION PROTOTYPE", new Vector2(-11.5f, 5.55f), .13f, ink);
        Label("01  /  BROAD TURN", new Vector2(-11.5f, 4.55f), .105f, secondary);
        Label("02  /  REVERSE CURVE", new Vector2(.6f, 4.55f), .105f, secondary);
        var first = Worm(); NativePose(first, 0); first.transform.position = new Vector3(-7.5f, -.6f, 0);
        var second = Worm(); NativePose(second, 1); second.transform.position = new Vector3(4.5f, 1.8f, 0);
        // Center each sampled pose geometrically, while retaining its actual simulated articulation.
        CenterPose(first, new Vector2(-6, .3f));
        CenterPose(second, new Vector2(6, .3f));
        Soldier(new Vector2(-10.7f, -3.65f)); Soldier(new Vector2(1.3f, -3.65f));
        Label("LEVEL 2 SOLDIER\n16 PPU / ORIGINAL SCALE", new Vector2(-9.9f, -3.35f), .085f, secondary);
        Label("LEVEL 2 SOLDIER\n16 PPU / ORIGINAL SCALE", new Vector2(2.1f, -3.35f), .085f, secondary);
        Line("One world unit", new Vector2(-2.7f, -4.2f), new Vector2(-1.7f, -4.2f), ink, .035f);
        Label("1 UNIT", new Vector2(-2.65f, -4.4f), .08f, ink);
        Label("HEAD + 6 BODY SECTIONS + TAIL   /   8 COLLIDERS   /   SHARED HEALTH", new Vector2(-11.5f, -5.2f), .1f, secondary);
        var camera = Own(new GameObject("Sandworm preview camera")).AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -30);
        camera.orthographic = true; camera.orthographicSize = 6.0f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.06f, .075f, .17f);
        var target = Own(new RenderTexture(1600, 800, 24));
        var texture = Own(new Texture2D(1600, 800, TextureFormat.RGBA32, false));
        camera.targetTexture = target; camera.aspect = 2;
        Capture(camera, target, texture);
        int visible = 0;
        foreach (Color pixel in texture.GetPixels()) if (pixel.r > .2f && pixel.g > .15f) visible++;
        Check(visible > 4000, "GPU preview contains visible body geometry");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        File.WriteAllBytes(Path.Combine(output, "Sandworm-preview.png"), texture.EncodeToPNG());
        // A separate genuine movement sequence, not interpolation between hand-posed segments.
        first.gameObject.SetActive(false); second.gameObject.SetActive(false);
        var moving = Worm();
        Set(moving, "wanderSize", new Vector2(40, 30)); Validate(moving);
        Check(moving.TrySetDestination(new Vector2(10, 0)), "animation first target");
        string frames = Path.Combine(output, "sandworm-frames"); Directory.CreateDirectory(frames);
        for (int frame = 0; frame < 32; frame++)
        {
            if (frame == 9) Check(moving.TrySetDestination(new Vector2(7, 7)), "animation upper bend");
            if (frame == 20) Check(moving.TrySetDestination(new Vector2(-2, 3)), "animation returning bend");
            for (int step = 0; step < 7; step++) moving.Advance(1f / 60f);
            Capture(camera, target, texture);
            File.WriteAllBytes(Path.Combine(frames, "sandworm-" + frame.ToString("000") + ".png"), texture.EncodeToPNG());
        }
        camera.targetTexture = null;
    }

    static void CenterPose(SandwormBossPrototype worm, Vector2 center)
    {
        var renderers = worm.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        worm.transform.position += (Vector3)(center - (Vector2)bounds.center);
    }
}
