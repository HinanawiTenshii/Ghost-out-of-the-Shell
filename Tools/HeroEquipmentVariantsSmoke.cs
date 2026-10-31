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
public static class HeroEquipmentVariantsSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Names = { "Hero", "Hero-Unarmed", "Hero-ShieldOnly" };
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static HeroEquipmentVariantsSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, F).GetValue(o);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "hero-equipment-variants-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(HeroEquipmentVariantsSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HeroEquipmentVariantsSmoke), false))
            { SessionState.SetBool(nameof(HeroEquipmentVariantsSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hero variants timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERO_VARIANTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0;
        var civilian = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Civilian-level2-Idle.prefab").GetComponent<CivilianZeldaCharacterData>();
        var catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
        var heroes = new HeroZeldaCharacterData[3];
        var create = typeof(HeroZeldaCharacterData).GetMethod("CreateSprite", F);
        for (int i = 0; i < Names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/" + Names[i] + ".prefab");
            Check(prefab, "prefab imports " + Names[i]);
            var go = Object.Instantiate(prefab, new Vector3(i * 5, 0, 0), Quaternion.identity);
            var data = heroes[i] = go.GetComponent<HeroZeldaCharacterData>();
            var mover = go.GetComponent<ZeldaFourWayMover>();
            Check(go.GetComponents<ZeldaCharacterData>().Length == 1 && go.GetComponent<ZeldaCharacterAiBase>().enabled && !mover.enabled, "single character with original idle AI");
            Check(data.HasSword == (i == 0) && data.HasShield == (i != 1), "correct equipment flags");
            Check(data.Health == 10 && data.SkillValue == 4 && data.MoveSpeed == 5 && data.PossessionEnergy == 3 && data.PossessionCost == 1, "noncombat hero stats retained");
            Check(data.CameraOrthographicSize == 9 && data.PlayerVisionRadius == 13 && !go.GetComponent<BoxCollider2D>().isTrigger, "camera and body collision retained");
            Check(!go.GetComponent<NativeCharacterSkillData>() && !PixelCharacterWalkAnimator.Supports(data), "no extra skill or duplicate walk animation");
            if (i > 0)
            {
                Check(data.AttackVisualShape == civilian.AttackVisualShape && data.AttackPrefab == civilian.AttackPrefab, "civilian attack renderer and hitbox prefab");
                Check(data.AttackPower == civilian.AttackPower && data.AttackDuration == civilian.AttackDuration &&
                    data.AttackSpawnOffset == civilian.AttackSpawnOffset && data.AttackSize == civilian.AttackSize && data.AttackVisualTint == civilian.AttackVisualTint,
                    "all authored attack parameters match civilian");
            }
            else Check(data.AttackPower == 3 && data.AttackVisualShape == ZeldaAttackVisualShape.Sword && data.AttackDuration == .25f, "original sword combat unchanged");
            for (int direction = 0; direction < 4; direction++)
            for (int pose = 0; pose < 4; pose++)
            {
                var sprite = (Sprite)create.Invoke(data, new object[] { direction, pose });
                Check(sprite.pixelsPerUnit == 16 && sprite.texture.filterMode == FilterMode.Point && sprite.texture.isReadable, "readable 16 PPU pose");
                var pixels = sprite.texture.GetPixels32();
                Color32 metal = new SerializedObject(data).FindProperty("metalColor").colorValue;
                Check((pixels.Count(c => c.Equals(metal)) > 0) == (i != 1), "metal gear absent only on fully unarmed hero");
                // The scabbard tip sits outside the body in every direction, distinct from the shield.
                int tipX = direction == 0 ? 12 : direction == 1 ? 3 : direction == 2 ? 11 : 3;
                Check((pixels[2 * 16 + tipX].a > 0) == (i == 0), "no hidden scabbard in swordless pose");
                if (data.HasShield)
                {
                    var ink = typeof(HeroZeldaCharacterData).GetMethod("PixelColor",F);
                    Func<char,Color32> color = symbol => (Color)ink.Invoke(data,new object[] {symbol});
                    if (direction == 0)
                    {
                        Check(pixels[7*16+5].Equals(color('T')), "front shield is raised in front of torso");
                        if (pose == 3)
                            Check(pixels[4*16+8].Equals(color('F')), "front attacking hand remains visible beside shield");
                        else
                            Check(pixels[6*16+10].Equals(color('F')), "front resting weapon hand preserved while shield stays raised");
                    }
                    else if (direction == 1)
                    {
                        Check(pixels[10*16+12].Equals(color('B')), "up-facing shield shows the inner leather backing");
                        Check(!pixels[6*16+11].Equals(color('F')), "no dangling duplicate shield hand behind raised shield");
                        if (pose != 3)
                            Check(pixels[6*16+4].Equals(color('F')), "up-facing resting weapon hand preserved");
                    }
                    else if (direction == 2)
                    {
                        Check(pixels[8*16+2].Equals(color('C')), "near-side shield is ahead of left-facing body, not on its flank");
                        Check(pixels[7*16+4].Equals(color('F')), "near shield is connected to bent forearm");
                        Check(pixels[6*16+7].Equals(color('B')), "near shield arm has no duplicate hanging hand");
                    }
                    else
                    {
                        Check(pixels[9*16+14].Equals(color('Q')), "far shield peeks out in front of right-facing body");
                        Check(pixels[7*16+15].Equals(color('Q')), "far shield retains an outer rim beside the near attacking arm");
                        if (pose == 3)
                            Check(pixels[7*16+(data.HasSword ? 13 : 12)].Equals(color('F')), "near attacking hand overlaps far shield correctly");
                        else
                            Check(pixels[6*16+7].Equals(color('F')), "right-facing near hand rests while far arm holds shield ahead");
                    }
                    string field = new[] {"Front","Back","Left","Right"}[direction];
                    var bodyRows = (string[])typeof(HeroZeldaCharacterData).GetField(field,F).GetValue(null);
                    for (int row = 0; row < 6; row++) for (int x = 0; x < 16; x++)
                        if (bodyRows[row][x] != '.')
                            Check(pixels[(15-row)*16+x].Equals(color(bodyRows[row][x])), "raised limbs and far shield do not paint over cap, face or hair");
                    if (pose == 1 || pose == 2)
                    {
                        int steppingFootX = pose == 1 ? 5 : 9;
                        int groundedFootX = pose == 1 ? 9 : 5;
                        Check(pixels[3*16+steppingFootX].Equals(color('K')) && pixels[2*16+steppingFootX].a == 0 &&
                            pixels[2*16+groundedFootX].Equals(color('K')),
                            "walking feet continue stepping beneath continuously raised shield");
                    }
                }
                Object.Destroy(sprite.texture); Object.Destroy(sprite);
            }
            go.name = "Renamed story hero " + i;
            var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(data.CaptureRuntimeState()));
            Check(catalog.FindCharacter(saved.characterType, go.name) == prefab, "renamed traveling body resolves correct equipment prefab");
            var restored = Object.Instantiate(catalog.FindCharacter(saved.characterType, go.name)).GetComponent<HeroZeldaCharacterData>();
            restored.ApplyRuntimeState(saved);
            Check(restored.GetType() == data.GetType() && restored.HasSword == data.HasSword && restored.HasShield == data.HasShield && restored.AttackVisualShape == data.AttackVisualShape, "disk-format reconstruction preserves appearance and attack");
            Object.Destroy(restored.gameObject);
            go.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            Check(mover.TryPerformAttack(Vector2.right), "AI can invoke attack without player control");
            var attack = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
            Check(Get<ZeldaAttackVisualShape>(attack, "visualShape") == data.AttackVisualShape && Get<int>(attack, "attackPower") == data.AttackPower, "NPC hitbox inherits correct combat");
            Check(Vector2.Scale(attack.GetComponent<BoxCollider2D>().size, (Vector2)attack.transform.localScale) == data.AttackSize,
                "NPC hitbox uses civilian dimensions when swordless");
            Object.Destroy(attack.gameObject); mover.AdvanceExternalAttackTimer(1f); yield return null;
            mover.ReceiveControl(Vector2.left);
            Check(mover.TryPerformAttack(Vector2.left), "possessed variant attacks");
            attack = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
            Check(Get<ZeldaAttackVisualShape>(attack, "visualShape") == data.AttackVisualShape, "player and AI use the same attack shape");
            Object.Destroy(attack.gameObject); mover.enabled = false; mover.AdvanceExternalAttackTimer(1f);
            go.SetActive(false); go.SetActive(true); go.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            yield return null;
        }
        Preview(heroes, false); Preview(heroes, true);
        var cached = new Sprite[3];
        for (int i = 0; i < heroes.Length; i++)
        {
            var renderer = heroes[i].GetComponent<SpriteRenderer>();
            heroes[i].ApplyCharacterVisual(renderer, Vector2.down, false, false); cached[i] = renderer.sprite;
            heroes[i].ApplyCharacterVisual(renderer, Vector2.down, false, false);
            Check(renderer.sprite == cached[i], "animation sprites are cached");
            Object.Destroy(heroes[i].gameObject);
        }
        yield return null; yield return null;
        Check(cached.All(s => s == null), "derived character destruction releases animation sprites");
        // Existing full-gear hero's original regression suite must continue to pass.
        var originalSuite = (IEnumerator)typeof(HeroSmoke).GetMethod("Verify", F).Invoke(null, null);
        while (originalSuite.MoveNext()) yield return originalSuite.Current;
        Debug.Log("HERO_ORIGINAL_REGRESSION_PASS");
    }
    static void Preview(HeroZeldaCharacterData[] heroes, bool attack)
    {
        const int cell = 160, scale = 8, width = 640, height = 480;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = Enumerable.Repeat(new Color32(24, 31, 43, 255), width * height).ToArray();
        var renderer = new GameObject("Preview sprite").AddComponent<SpriteRenderer>();
        for (int row = 0; row < heroes.Length; row++)
        for (int d = 0; d < 4; d++)
        {
            heroes[row].ApplyCharacterVisual(renderer, Directions[d], false, attack);
            var sprite = renderer.sprite.texture.GetPixels32();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                var c = sprite[y * 16 + x]; if (c.a == 0) continue;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                    pixels[((2 - row) * cell + 16 + y * scale + dy) * width + d * cell + 16 + x * scale + dx] = c;
            }
        }
        image.SetPixels32(pixels); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../Hero-Equipment-" + (attack ? "Attack" : "Idle") + "-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); Object.Destroy(renderer.gameObject);
    }
}
