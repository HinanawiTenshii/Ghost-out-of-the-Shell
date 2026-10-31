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
public static class HeroSwordAttackSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static HeroSwordAttackSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, F).GetValue(obj);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "hero-sword-attack-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(HeroSwordAttackSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HeroSwordAttackSmoke),false))
            { SessionState.SetBool(nameof(HeroSwordAttackSmoke),false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Hero sword test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERO_SWORD_ATTACK_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0;
        yield return null;
        var listener = new GameObject("Test Listener").AddComponent<AudioListener>();
        var sprite = HeroSwordPixelArt.GetAttackSprite();
        Check(sprite == HeroSwordPixelArt.GetAttackSprite() && sprite.pixelsPerUnit == 16 && sprite.texture.filterMode == FilterMode.Point,
            "single cached 16-PPU point-filtered hero sword");
        var stand = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/SwordPedestal.prefab"), new Vector3(100,0,0),Quaternion.identity);
        var mesh = stand.GetComponentsInChildren<MeshFilter>().Single(f => f.sharedMesh.name.Contains("Blade")).sharedMesh;
        var vertices = mesh.vertices; var colors = mesh.colors32;
        for (int start = 0; start < vertices.Length; start += 4)
        {
            int x0 = Mathf.RoundToInt(vertices[start].x * 16), y0 = Mathf.RoundToInt(vertices[start].y * 16);
            int x1 = Mathf.RoundToInt(vertices[start+2].x * 16), y1 = Mathf.RoundToInt(vertices[start+2].y * 16);
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                Check(((Color32)sprite.texture.GetPixel(2-x,17-y)).Equals(colors[start]), "every exposed pedestal pixel matches the rotated attack sword");
        }
        Object.Destroy(stand);
        var heroes = new HeroZeldaCharacterData[4];
        var attacks = new ZeldaAttackHitbox[4];
        for (int i = 0; i < 4; i++)
        {
            var hero = heroes[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/Hero.prefab"), new Vector3((i-1.5f)*2.4f,0,0),Quaternion.identity).GetComponent<HeroZeldaCharacterData>();
            hero.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            var mover = hero.GetComponent<ZeldaFourWayMover>();
            if (i == 3) mover.ReceiveControl(Directions[i]);
            Check(mover.TryPerformAttack(Directions[i]), "NPC/player sword attack fires in direction " + i);
            var attack = attacks[i] = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single(h => Get<ZeldaCharacterData>(h,"owner") == hero);
            var renderer = attack.GetComponent<SpriteRenderer>();
            Check(renderer.sprite == sprite && renderer.color == Color.white, "solid bright weapon pixels match the opaque hero rather than darkening through alpha");
            Check((attack.transform.up - (Vector3)Directions[i]).sqrMagnitude < .00001f, "sword points along actual attack direction");
            Check(attack.GetComponent<BoxCollider2D>().size == Vector2.one && attack.transform.localScale == new Vector3(.55f,.9f,1), "original damage box unchanged");
            Check(Get<int>(attack,"attackPower") == 3 && Get<float>(attack,"lifetime") == .25f && hero.AttackSpawnOffset == new Vector2(0,.6f), "original damage, lifetime and attack offset unchanged");
            var basicRenderer = Get<SpriteRenderer>(attack,"potionSweepRenderer");
            Check(Get<bool>(attack,"heroSwordSweep") && !Get<bool>(attack,"potionMeleeSweep") && renderer.forceRenderingOff &&
                basicRenderer != null && basicRenderer.sprite == sprite && basicRenderer.color == Color.white, "basic sword enables one visual-only sweep without enabling Berserk");
            var position = attack.transform.position; var rotation = attack.transform.rotation; var scale = attack.transform.localScale;
            Vector3 start = Vector3.zero, end = Vector3.zero;
            foreach (float progress in new[] { 0f,.25f,.5f,.75f,1f })
            {
                SetNormalSweepProgress(attack,progress);
                // FromToRotation(up, down) may use a 180-degree X rotation.
                // Measure in the attack's frame rather than assuming every facing has world +Z as its normal.
                float angle = Vector3.SignedAngle(Vector3.up,
                    attack.transform.InverseTransformDirection(basicRenderer.transform.up),Vector3.forward);
                float expected = Mathf.SmoothStep(-45f,45f,progress);
                Check(Mathf.Abs(Mathf.DeltaAngle(expected,angle)) < .001f, "Update drives the 90-degree arc around facing direction " + i +
                    ": progress=" + progress + ", expected=" + expected + ", actual=" + angle + ", dt=" + Time.deltaTime);
                Check(attack.transform.position == position && attack.transform.rotation == rotation && attack.transform.localScale == scale &&
                    attack.GetComponent<BoxCollider2D>().size == Vector2.one, "visual animation does not move, rotate or enlarge hitbox");
                Check(Mathf.Abs(Vector3.Dot(basicRenderer.transform.TransformVector(Vector3.up),basicRenderer.transform.TransformVector(Vector3.right))) < .00001f,
                    "rotating blade is not sheared by nonuniform damage-box scale");
                if (progress == 0) start = basicRenderer.transform.up;
                if (progress == 1) end = basicRenderer.transform.up;
            }
            Check(Mathf.Abs(Vector3.Angle(start,end)-90) < .001f && attack.GetComponentsInChildren<Collider2D>().Length == 1, "total sweep is exactly 90 degrees, with no extra attack collider");
            Check(basicRenderer.sortingOrder == hero.GetComponent<SpriteRenderer>().sortingOrder + ((i == 1 || i == 2) ? -1 : 1),
                "up/left sword grip is behind head/near shield; down/right sword is in front");
            hero.ApplyCharacterVisual(hero.GetComponent<SpriteRenderer>(), Directions[i], false, true);
        }
        Preview(attacks);
        var target = new GameObject("Sword Damage Target").AddComponent<ZeldaCharacterData>();
        target.transform.position = attacks[0].transform.position;
        var body = target.gameObject.AddComponent<BoxCollider2D>();
        int hp = target.CurrentHealth;
        var damage = typeof(ZeldaAttackHitbox).GetMethod("TryDamage",F);
        damage.Invoke(attacks[0], new object[] { body }); damage.Invoke(attacks[0], new object[] { body });
        Check(target.CurrentHealth == hp-3, "real contact resolver still applies AP3 exactly once per target");
        Object.Destroy(target.gameObject);
        foreach (var attack in attacks) Object.Destroy(attack.gameObject);
        yield return null;
        var berserker = heroes[0];
        var berserkMover = berserker.GetComponent<ZeldaFourWayMover>();
        berserkMover.AdvanceExternalAttackTimer(1);
        Check(CharacterPotionBuffs.TryApply(berserker,AlchemyPotionKind.Berserk), "berserk buff applied");
        Check(berserkMover.TryPerformAttack(Vector2.up), "berserk attack fires");
        var sweep = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
        var sweepRenderer = Get<SpriteRenderer>(sweep,"potionSweepRenderer");
        Check(sweepRenderer != null && sweepRenderer.sprite == sprite && sweepRenderer.color == berserker.AttackVisualTint,
            "berserk sweep inherits matching sword artwork and palette");
        var shape = sweep.GetComponent<BoxCollider2D>().size; var pose = sweep.transform.rotation;
        foreach (float progress in new[] { 0f,.5f,1f })
        {
            typeof(ZeldaAttackHitbox).GetMethod("UpdatePotionSweepVisual",F).Invoke(sweep,new object[] {progress});
            Check(sweep.GetComponent<BoxCollider2D>().size == shape && sweep.transform.rotation == pose, "sweep animation leaves combat geometry untouched");
            Check(Mathf.Abs(Mathf.DeltaAngle(Mathf.SmoothStep(-55,55,progress),
                Vector3.SignedAngle(Vector3.up,sweepRenderer.transform.up,Vector3.forward))) < .001f, "Berserk keeps its original 110-degree arc");
        }
        Object.Destroy(sweep.gameObject);
        foreach (var hero in heroes) Object.Destroy(hero.gameObject);
        yield return null;
        foreach (var path in new[] { "Assets/Prefabs/VitalNPCS/Hero-Unarmed.prefab", "Assets/Prefabs/VitalNPCS/Hero-ShieldOnly.prefab", "Assets/Prefabs/Level2NPCS/Idle/Paladin.prefab" })
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)).GetComponent<ZeldaCharacterData>();
            actor.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            Check(actor.GetComponent<ZeldaFourWayMover>().TryPerformAttack(Vector2.right), "comparison actor attacks");
            var hit = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
            Check(hit.GetComponent<SpriteRenderer>().sprite != sprite, "swordless heroes and paladins do not use the hero sword");
            Check(hit.GetComponent<SpriteRenderer>().color == actor.AttackVisualTint, "other attack tints unchanged");
            Check(!Get<bool>(hit,"potionSweepVisualActive") && !hit.GetComponent<SpriteRenderer>().forceRenderingOff, "normal attacks of other characters stay static");
            if (actor is HeroZeldaCharacterData) Check(actor.AttackPower == 1 && actor.AttackVisualShape == ZeldaAttackVisualShape.Rock, "swordless civilian attack preserved");
            if (actor is HeroZeldaCharacterData shielded && shielded.HasShield)
            {
                hit.transform.rotation = Quaternion.FromToRotation(Vector3.up,Vector3.left);
                hit.Configure(actor.AttackDuration,actor.AttackSize,actor.AttackVisualTint,actor.AttackPower,actor,actor.AttackVisualShape,false,true);
                Check(hit.GetComponent<SpriteRenderer>().sortingOrder < actor.GetComponent<SpriteRenderer>().sortingOrder, "shield-only attack also goes behind near-side raised shield");
                hit.Configure(.1f,Vector2.one,Color.white,1,null,ZeldaAttackVisualShape.Box);
                Check(hit.GetComponent<SpriteRenderer>().sortingOrder == 2, "reconfiguration restores the original renderer order");
            }
            Object.Destroy(hit.gameObject); Object.Destroy(actor.gameObject); yield return null;
        }
        Object.Destroy(listener.gameObject); yield return null;
        var oldTests = (IEnumerator)typeof(ElderSwordPedestalSmoke).GetMethod("Verify",F).Invoke(null,null);
        while (oldTests.MoveNext()) yield return oldTests.Current;
        Debug.Log("ELDER_PEDESTAL_REGRESSION_PASS");
        var equipmentTests = (IEnumerator)typeof(HeroEquipmentVariantsSmoke).GetMethod("Verify",F).Invoke(null,null);
        while (equipmentTests.MoveNext()) yield return equipmentTests.Current;
        Debug.Log("HERO_EQUIPMENT_REGRESSION_PASS");
    }
    static void SetNormalSweepProgress(ZeldaAttackHitbox hit, float progress)
    {
        typeof(ZeldaAttackHitbox).GetField("elapsedLifetime",F).SetValue(hit,progress * Get<float>(hit,"lifetime"));
        typeof(ZeldaAttackHitbox).GetMethod("Update",F).Invoke(hit,null);
    }
    static void Preview(ZeldaAttackHitbox[] attacks)
    {
        var go = new GameObject("Hero Attack Preview Camera"); var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 1.5f; camera.transform.position = new Vector3(0,0,-10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.047f,.122f,.188f);
        var rt = RenderTexture.GetTemporary(1440,432,24,RenderTextureFormat.ARGB32); var old = RenderTexture.active;
        camera.targetTexture = rt; camera.aspect = 1440f/432;
        var image = new Texture2D(1440,432*3,TextureFormat.RGB24,false);
        for (int phase = 0; phase < 3; phase++)
        {
            foreach (var attack in attacks) SetNormalSweepProgress(attack,phase * .5f);
            camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,1440,432),0,(2-phase)*432);
        }
        image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../Hero-SwordSweep-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.Destroy(go);
    }
}
