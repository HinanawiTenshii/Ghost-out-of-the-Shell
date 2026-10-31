using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ElderSwordPedestalSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string ElderPath = "Assets/Prefabs/VitalNPCS/Elder-Guide.prefab";
    static readonly string[] Stands = { "SwordPedestal", "SwordPedestal-Empty" };
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static ElderSwordPedestalSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, F).GetValue(o);
    static GameObject LoadStand(int i) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/" + Stands[i] + ".prefab");
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "elder-sword-pedestal-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElderPath);
        Check(prefab != null && prefab.GetComponent<ElderZeldaCharacterData>() != null, "elder script imports");
        Check(prefab.GetComponent<CharacterDialogue>().SpeakerName == "老者", "editable guide dialogue attached");
        foreach (var i in new[] { 0, 1 })
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadStand(i));
            var stand = instance.GetComponent<SwordPedestalVisual>();
            Check(stand.HasSword == (i == 0) && instance.GetComponentsInChildren<MeshRenderer>().Length == (i == 0 ? 2 : 1), "both authored states visible in editor");
            var serialized = new SerializedObject(stand);
            serialized.FindProperty("hasSword").boolValue = i != 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(SwordPedestalVisual).GetMethod("Update", F).Invoke(stand, null);
            Check(instance.GetComponentsInChildren<MeshRenderer>().Length == (i == 0 ? 1 : 2), "Inspector checkbox refreshes editor geometry");
            Object.DestroyImmediate(instance);
        }
        SessionState.SetBool(nameof(ElderSwordPedestalSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(ElderSwordPedestalSmoke), false))
            { SessionState.SetBool(nameof(ElderSwordPedestalSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 100; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Elder/pedestal test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("ELDER_PEDESTAL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        new GameObject("Test Audio Listener").AddComponent<AudioListener>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElderPath);
        var elder = Object.Instantiate(prefab, new Vector3(-2.2f,0,0), Quaternion.identity).GetComponent<ElderZeldaCharacterData>();
        var mover = elder.GetComponent<ZeldaFourWayMover>();
        Check(!mover.enabled && elder.GetComponent<ZeldaCharacterAiBase>().enabled, "idle NPC by default");
        elder.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        Check(elder.IsImmuneToDamage && !elder.CanTakeAttackDamage, "attack hitboxes reject immune elder");
        Check(elder.Health == 5 && elder.CurrentHealth == 5 && elder.AttackPower == 1, "civilian base stats");
        int changes = 0;
        elder.ValuesChanged += () => changes++;
        elder.TakeDamage(1); elder.TakeDamage(999, null); elder.TakeHandgunDamage(999, Vector2.zero);
        typeof(ZeldaCharacterData).GetMethod("TakeDominoDamage", F).Invoke(elder, new object[] { 999, null });
        elder.KillFromPotion(); elder.DieFromSoulDetonation();
        Check(elder.CurrentHealth == 5 && !elder.IsDead && changes == 0, "melee, lethal, handgun, shared damage and instant-kill APIs preserve life");
        var renderer = elder.GetComponent<SpriteRenderer>();
        foreach (var direction in new[] { Vector2.down, Vector2.up, Vector2.left, Vector2.right })
        foreach (var pose in new[] { 0, 1, 2, 3 })
        {
            var sprite = (Sprite)typeof(PriestZeldaCharacterData).GetMethod("CreateSprite", F).Invoke(elder, new object[] { DirectionIndex(direction), pose });
            Check(sprite.pixelsPerUnit == 16 && sprite.texture.filterMode == FilterMode.Point && sprite.texture.GetPixels32().Count(c => c.a > 0) > 60,
                "four-direction elder idle/walk/attack sprites are valid");
            Object.Destroy(sprite.texture); Object.Destroy(sprite);
        }
        elder.ApplyCharacterVisual(renderer, Vector2.down, false, false);
        Sprite cached = renderer.sprite;
        elder.ApplyCharacterVisual(renderer, Vector2.down, false, false);
        Check(renderer.sprite == cached && renderer.color == Color.white, "cached art, no hurt flash on immunity");
        elder.name = "Renamed Tutorial Elder";
        var state = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(elder.CaptureRuntimeState()));
        var savedPrefab = Resources.Load<SaveGameCatalog>("SaveGameCatalog").FindCharacter(state.characterType, elder.name);
        Check(savedPrefab == prefab, "renamed elder resolves to unique saved character type");
        var restored = Object.Instantiate(savedPrefab, new Vector3(100,100,0), Quaternion.identity).GetComponent<ElderZeldaCharacterData>();
        restored.ApplyRuntimeState(state); restored.TakeDamage(999);
        Check(restored.IsImmuneToDamage && restored.CurrentHealth == 5 && !restored.IsDead, "immunity retained through runtime-state round trip");
        Object.Destroy(restored.gameObject);
        var civilian = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab"), new Vector3(100,100,0), Quaternion.identity).GetComponent<ZeldaCharacterData>();
        Check(!civilian.IsImmuneToDamage && civilian.CanTakeAttackDamage, "ordinary characters remain damageable");
        int oldHealth = civilian.CurrentHealth;
        civilian.TakeDamage(1);
        Check(civilian.CurrentHealth < oldHealth, "existing damage path still deducts HP");
        civilian.KillFromPotion();
        Check(civilian.IsDead, "ordinary lethal potion still kills");

        var stands = new SwordPedestalVisual[2];
        for (int i = 0; i < 2; i++)
        {
            var stand = stands[i] = Object.Instantiate(LoadStand(i), new Vector3(i * 2.2f,0,0), Quaternion.identity).GetComponent<SwordPedestalVisual>();
            Check(stand.HasSword == (i == 0), "prefab state survives play mode");
            var collider = stand.GetComponent<BoxCollider2D>();
            Check(stand.GetComponentsInChildren<Collider2D>(true).Length == 1 && collider.enabled && !collider.isTrigger &&
                collider.size == new Vector2(1.18f,.75f) && collider.offset == new Vector2(0,.3125f), "one solid collider follows overhead stone footprint only");
            Check(stand.GetComponents<MonoBehaviour>().Length == 1 && !stand.GetComponent<DoorData>(), "no unrequested pickup, breakage or quest behavior");
            var meshes = stand.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
            Check(meshes.Length == 2 && meshes.All(m => m.vertexCount < 45 && m.colors32.All(c => c.a == 255)), "small flat-colour meshes");
            Check(meshes[0].vertexCount == 20 && meshes[0].triangles.Length == 42, "only two cut-corner faces and an empty slot, no extra decoration");
            var stoneVertices = meshes[0].vertices;
            float baseFront = stoneVertices.Take(8).Min(v => v.y);
            float topFront = stoneVertices.Skip(8).Take(8).Min(v => v.y);
            float topRear = stoneVertices.Skip(8).Take(8).Max(v => v.y);
            Check(Mathf.Abs((topRear-topFront) / (topFront-baseFront)-6f) < .0001f, "visible top is six times deeper than the front lip");
            float slotCentre = stoneVertices.Skip(16).Average(v => v.y);
            Check(slotCentre == (topFront+topRear)*.5f && slotCentre == HeroSwordPixelArt.InsertionRow/16f,
                "sword and empty slot meet at the centre of the overhead top face");
            var swordVertices = meshes[1].vertices;
            Check(swordVertices.All(v => Mathf.Abs(v.x * 16 - Mathf.Round(v.x * 16)) < .0001f &&
                Mathf.Abs(v.y * 16 - Mathf.Round(v.y * 16)) < .0001f), "sword is aligned to the character 16-PPU pixel grid");
            Check(swordVertices.Take(8).Min(v => v.y) == 7f/16 && swordVertices.Take(8).Max(v => v.y) == 13f/16,
                "only upper six of twelve blade pixels visible; no floating exposed point");
            Check(meshes[1].bounds.max.y == 18f/16 && meshes[0].bounds.max.y == 13f/16, "sword unchanged above a wider overhead surface");
            foreach (var mesh in meshes)
            {
                var v = mesh.vertices; var t = mesh.triangles;
                for (int n = 0; n < t.Length; n += 3)
                    Check(Vector3.Cross(v[t[n+1]] - v[t[n]], v[t[n+2]] - v[t[n]]).z > .000001f, "valid nondegenerate mesh triangle");
            }
            Check(stand.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sortingOrder >= 5 && r.sharedMaterial.shader.name == "Sprites/Default"), "scene-style unlit material and sorting");
            for (int n = 0; n < 16; n++) stand.SetSwordPresent(n % 2 == 0);
            Check(!stand.HasSword && stand.GetComponentsInChildren<MeshRenderer>().Length == 1, "runtime removal hides only sword");
            Check(stand.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).SequenceEqual(meshes), "state toggle reuses all geometry");
            stand.gameObject.SetActive(false); stand.gameObject.SetActive(true);
            Check(!stand.HasSword && stand.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).SequenceEqual(meshes), "distance reload preserves state and resources");
            stand.SetSwordPresent(i == 0);
        }
        yield return null; yield return null;
        Preview();
        var releasedMeshes = stands.SelectMany(s => s.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh).ToArray();
        var releasedMaterial = stands[0].GetComponentInChildren<MeshRenderer>().sharedMaterial;
        foreach (var stand in stands) Object.Destroy(stand.gameObject);
        Object.Destroy(elder.gameObject);
        yield return null; yield return null;
        Check(releasedMeshes.All(m => m == null) && releasedMaterial == null && cached == null, "meshes, material and inherited elder sprites are released");
    }
    static int DirectionIndex(Vector2 d) => d == Vector2.down ? 0 : d == Vector2.up ? 1 : d == Vector2.left ? 2 : 3;
    static void Preview()
    {
        var go = new GameObject("Preview Camera");
        var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 1.25f;
        camera.transform.position = new Vector3(0,.62f,-10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.047f,.122f,.188f);
        var rt = RenderTexture.GetTemporary(1200,400,24,RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active; camera.targetTexture = rt; camera.aspect = 3f; camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1200,400,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1200,400),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../Elder-SwordPedestal-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt); Object.Destroy(go);
    }
}
