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
public static class HeroDirectionalShieldSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Vector2[] Directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static HeroDirectionalShieldSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) return field; }
        throw new Exception("Missing field " + name);
    }
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static HeroZeldaCharacterData Spawn(string name = "Hero", float x = 0)
    {
        var hero = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/VitalNPCS/" + name + ".prefab"),
            new Vector3(x, 0, 0), Quaternion.identity).GetComponent<HeroZeldaCharacterData>();
        hero.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        return hero;
    }
    static void Face(HeroZeldaCharacterData hero, Vector2 facing, bool player = true)
    {
        var mover = hero.GetComponent<ZeldaFourWayMover>();
        mover.enabled = player;
        Set(mover, "facingDirection", player ? facing : -facing);
        Set(hero.GetComponent<ZeldaCharacterAiBase>(), "facingDirection", player ? -facing : facing);
        hero.ApplyCharacterVisual(hero.GetComponent<SpriteRenderer>(), facing, false, false);
    }
    static CharacterDamageResult Hit(HeroZeldaCharacterData hero, Vector2 from, bool blast = false)
        => hero.TakeDamage(1, null, blast ? DirectionalDamageContext.Explosion((Vector2)hero.transform.position + from) :
            DirectionalDamageContext.Melee((Vector2)hero.transform.position + from));
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "hero-directional-shield-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(HeroDirectionalShieldSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(HeroDirectionalShieldSmoke), false))
            { SessionState.SetBool(nameof(HeroDirectionalShieldSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Shield suite timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("HERO_DIRECTIONAL_SHIELD_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0; yield return null;
        new GameObject("Test Listener").AddComponent<AudioListener>();
        var clip = Resources.Load<AudioClip>("Audio/HeroShieldBlock");
        Check(clip != null && Mathf.Abs(clip.length - .24f) < .001f && clip.channels == 1, "imported short mono clang");
        foreach (string variant in new[] { "Hero", "Hero-ShieldOnly" })
        foreach (bool player in new[] { false, true })
        {
            var hero = Spawn(variant);
            Check(hero.ShieldBlockAngle == 90, "existing prefab inherits enabled 90-degree default without migration");
            int hurtEvents = 0; hero.HealthDamaged += (body, amount, owner) => hurtEvents++;
            foreach (Vector2 facing in Directions)
            {
                Face(hero, facing, player);
                var originalPosition = hero.transform.position;
                var originalBounds = hero.GetComponent<BoxCollider2D>().size;
                int hp = hero.CurrentHealth;
                Check(Hit(hero, facing * 2) == CharacterDamageResult.DirectionalShieldBlocked, "front blocked " + variant + " player=" + player + " facing=" + facing);
                Check(hero.CurrentHealth == hp && hurtEvents == 0 && !hero.IsShowingDamageFeedback, "no HP loss/red flash/health-damage event");
                Check(hero.transform.position == originalPosition && hero.GetComponent<BoxCollider2D>().size == originalBounds, "no physical recoil or new collision dimensions");
                foreach (float angle in new[] { -45f, 45f })
                    Check(Hit(hero, Quaternion.Euler(0,0,angle) * (facing * 2)) == CharacterDamageResult.DirectionalShieldBlocked, "inclusive 45-degree protection edge");
                int before = hero.ShieldBlockVersion;
                Check(Hit(hero, Quaternion.Euler(0,0,46) * (facing * 2)) == CharacterDamageResult.Damaged, "outside protection arc takes damage");
                Check(Hit(hero, -facing * 2) == CharacterDamageResult.Damaged, "rear hit takes damage");
                Check(hero.ShieldBlockVersion == before, "unprotected hits do not produce block events");
                hero.RestoreHealth(100); hurtEvents = 0; Set(hero, "damageFeedbackTimer", 0f);
            }
            Face(hero, new Vector2(.3f, .9f), player);
            Check(Hit(hero, Vector2.up * 2) == CharacterDamageResult.DirectionalShieldBlocked, "diagonal AI facing quantizes to visible sprite");
            Set(hero,"shieldBlockAngle",120f);
            Check(Hit(hero, Quaternion.Euler(0,0,55) * (Vector2.up * 2)) == CharacterDamageResult.DirectionalShieldBlocked, "inspector-configurable protection angle");
            Set(hero,"shieldBlockAngle",90f);
            Object.Destroy(hero.gameObject); yield return null;
        }
        var target = Spawn(); Face(target, Vector2.right);
        var attacker = Spawn("Hero-Unarmed", 4); Face(attacker, Vector2.left);
        int evidence = 0, healthEvents = 0;
        target.AttackedBy += who => { if (who == attacker) evidence++; };
        target.HealthDamaged += (body, amount, owner) => healthEvents++;
        typeof(ZeldaCharacterData).GetMethod("RefreshHolyShield",F).Invoke(target, new object[] {3,false});
        Check(CharacterPotionBuffs.TryApply(target, AlchemyPotionKind.Echo), "echo setup");
        var linked = Spawn("Hero-Unarmed", 12);
        var domino = new GameObject("Test links").AddComponent<DominoSkillRuntime>();
        var addMember = typeof(DominoSkillRuntime).GetMethod("AddMember",F);
        addMember.Invoke(domino,new object[] {target}); addMember.Invoke(domino,new object[] {linked});
        int linkedHp = linked.CurrentHealth, targetHp = target.CurrentHealth;
        Check(target.TakeDamage(99, attacker, DirectionalDamageContext.Melee(attacker.transform.position)) == CharacterDamageResult.DirectionalShieldBlocked,
            "lethal front attack blocked");
        Check(target.HolyShieldCharges == 3 && target.HolyShieldBlockVersion == 0 && target.CurrentHealth == targetHp &&
            CharacterPotionBuffs.Has(target,AlchemyPotionKind.Echo), "physical guard precedes Holy Shield and Echo");
        Check(linked.CurrentHealth == linkedHp && healthEvents == 0 && evidence == 1, "no domino propagation, but attack evidence delivered");
        Check(target.TakeDamage(1, attacker, DirectionalDamageContext.Melee(Vector2.left * 2)) == CharacterDamageResult.HolyShieldBlocked &&
            target.HolyShieldCharges == 2, "rear attack still consumes Holy Shield normally");
        // Clear unrelated buffs to test plain damage and foot-origin blasts independently.
        var state = target.CaptureRuntimeState(); state.holyShield = default; state.potionBuffs = default;
        target.ApplyRuntimeState(state); Face(target,Vector2.right);
        DominoSkillRuntime.ClearForSceneTransition();
        Check(Hit(target, Vector2.right * 2, true) == CharacterDamageResult.DirectionalShieldBlocked, "front explosion uses blast origin, not attacker");
        Check(target.TakeDamage(1, attacker, DirectionalDamageContext.Projectile(Vector2.left)) == CharacterDamageResult.DirectionalShieldBlocked,
            "projectile direction determines blocking");
        attacker.transform.position = Vector3.left * 8;
        Check(target.TakeDamage(1, attacker, DirectionalDamageContext.Projectile(Vector2.left)) == CharacterDamageResult.DirectionalShieldBlocked,
            "projectile remains blocked after shooter moves behind target");
        Check(target.TakeDamage(1, attacker, DirectionalDamageContext.Explosion(Vector2.right * 2)) == CharacterDamageResult.DirectionalShieldBlocked,
            "blast remains blocked after bomb owner moves behind target");
        Physics2D.SyncTransforms();
        Vector2 foot = target.GetComponent<Collider2D>().bounds.center;
        Check(target.TakeDamage(1, attacker, DirectionalDamageContext.Explosion(foot)) == CharacterDamageResult.Damaged, "inside-footprint explosion bypasses guard");
        Check(target.TakeDamage(1, null, default) == CharacterDamageResult.Damaged, "non-directional/environmental damage bypasses guard");
        int beforeShared = target.CurrentHealth;
        typeof(ZeldaCharacterData).GetMethod("TakeDominoDamage",F).Invoke(target,new object[] {1,attacker});
        Check(target.CurrentHealth == beforeShared - 1, "incoming domino damage bypasses physical shield");
        target.RestoreHealth(100);
        // One attack stays consumed even if the target turns after blocking it; no Might knockback.
        attacker.transform.position = Vector3.right * 2; Face(target,Vector2.right);
        Check(CharacterPotionBuffs.TryApply(attacker,AlchemyPotionKind.Might), "Might setup");
        var hit = new GameObject("Melee test").AddComponent<ZeldaAttackHitbox>();
        hit.Configure(.25f,Vector2.one,Color.white,2,attacker,ZeldaAttackVisualShape.Rock,false,true);
        var contact = typeof(ZeldaAttackHitbox).GetMethod("TryDamage",F);
        Vector3 oldPosition = target.transform.position; int oldHp = target.CurrentHealth, version = target.ShieldBlockVersion;
        contact.Invoke(hit,new object[] {target.GetComponent<Collider2D>()});
        Face(target,Vector2.left); contact.Invoke(hit,new object[] {target.GetComponent<Collider2D>()});
        Check(target.CurrentHealth == oldHp && target.ShieldBlockVersion == version+1 && target.transform.position == oldPosition,
            "hit-set consumes blocked swing and suppresses Might knockback");
        Object.Destroy(hit.gameObject);
        // Directed energy wave uses its forward axis, not caster position.
        Face(target,Vector2.right); attacker.transform.position = Vector3.left * 8;
        hit = new GameObject("Wave test").AddComponent<ZeldaAttackHitbox>();
        hit.transform.position = Vector3.right * 2; hit.transform.rotation = Quaternion.FromToRotation(Vector3.up,Vector3.left);
        hit.Configure(.3f,new Vector2(1,3),Color.white,1,attacker,ZeldaAttackVisualShape.DirectionalWave,false,true);
        contact.Invoke(hit,new object[] {target.GetComponent<Collider2D>()});
        Check(target.CurrentHealth == oldHp && target.ShieldBlockVersion == version+2, "wave comes from its travel direction");
        Object.Destroy(hit.gameObject);
        yield return null;
        // Same circular damage resolver, different explicit rules for blast vs persistent flame.
        ZeldaAttackHitbox.ApplyCircularPulse(Vector2.right * 2, 3, 1, attacker);
        Check(target.CurrentHealth == oldHp, "circular blast blocked from front");
        ZeldaAttackHitbox.ApplyCircularPulse(Vector2.right * 2, 3, 1, attacker, false);
        Check(target.CurrentHealth == oldHp-1, "persistent fire pulse is not blocked");
        Object.Destroy(attacker.gameObject); Object.Destroy(linked.gameObject); Object.Destroy(target.gameObject); yield return null;

        // Real first-contact bullet resolves its block, disappears, and preserves anonymous investigation.
        target = Spawn(); var ai = target.GetComponent<ZeldaCharacterAiBase>(); ai.enabled = true;
        Face(target,Vector2.right,false);
        attacker = Spawn("Hero-Unarmed",20); Face(attacker,Vector2.left);
        int hpBeforeShot = target.CurrentHealth;
        Check(DisposableHandgunBullet.Fire(attacker,Vector2.left,20), "gun fires");
        var bullet = Object.FindObjectOfType<DisposableHandgunBullet>();
        attacker.transform.position = Vector3.left * 20;
        typeof(DisposableHandgunBullet).GetMethod("MoveSwept",F).Invoke(bullet,new object[] {Vector2.left * 25});
        Check(target.CurrentHealth == hpBeforeShot && target.ShieldBlockVersion == 1 && Get<bool>(bullet,"finished"), "swept bullet is consumed by block");
        Check(ai.CurrentState == ZeldaAiState.Search && Get<bool>(ai,"anonymousHandgunInvestigation") &&
            Get<ZeldaFourWayMover>(ai,"targetMover") == null, "blocked gunshot investigates without identifying shooter");
        Check((Get<Vector2>(ai,"investigationIncidentPosition")-Vector2.right*20).sqrMagnitude < .001f,
            "investigation uses original firing position even after shooter moves");
        yield return null;
        Check(bullet == null,"blocked bullet actually destroyed");
        Face(target,Vector2.left,false);
        int hostileHp = target.CurrentHealth;
        var directResult = target.TakeDamage(1,attacker,DirectionalDamageContext.Melee(attacker.transform.position));
        Check(directResult == CharacterDamageResult.DirectionalShieldBlocked && target.CurrentHealth == hostileHp && ai.CurrentState == ZeldaAiState.Hostile,
            "blocked identified player attack still provokes hostility");
        Object.Destroy(target.gameObject); Object.Destroy(attacker.gameObject); yield return null;
        // No-shield variant and ordinary paladin receive front damage unchanged.
        foreach (string path in new[] { "Assets/Prefabs/VitalNPCS/Hero-Unarmed.prefab", "Assets/Prefabs/Level2NPCS/Idle/Paladin.prefab" })
        {
            var other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)).GetComponent<ZeldaCharacterData>();
            other.GetComponent<ZeldaCharacterAiBase>().enabled = false;
            int hp = other.CurrentHealth;
            Check(other.TakeDamage(1,null,DirectionalDamageContext.Melee(Vector2.down*2)) == CharacterDamageResult.Damaged && other.CurrentHealth == hp-1,
                "no guard inherited by unarmed or unrelated paladin");
            Object.Destroy(other.gameObject); yield return null;
        }
        target = Spawn(); Face(target,Vector2.down);
        Preview(target);
        var cached = target.GetComponent<SpriteRenderer>().sprite;
        var saved = JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(JsonUtility.ToJson(target.CaptureRuntimeState()));
        Object.Destroy(target.gameObject); yield return null;
        Check(cached == null,"destroy releases feedback sprites");
        target = Spawn(); target.ApplyRuntimeState(saved); Face(target,Vector2.down);
        Check(!target.IsShowingShieldBlockFeedback && Hit(target,Vector2.down*2) == CharacterDamageResult.DirectionalShieldBlocked,
            "save reconstruction keeps guard without saving cosmetic transient state");
        target.gameObject.SetActive(false); target.gameObject.SetActive(true); Face(target,Vector2.left,false);
        Check(!target.IsShowingShieldBlockFeedback && Hit(target,Vector2.left*2) == CharacterDamageResult.DirectionalShieldBlocked,
            "streamed body reactivation retains guard and clears old flash");
        Object.Destroy(target.gameObject); yield return null;
    }
    static void Preview(HeroZeldaCharacterData hero)
    {
        const int cell = 160, scale = 8, width = 640, height = 480;
        var image = new Texture2D(width,height,TextureFormat.RGBA32,false);
        var pixels = Enumerable.Repeat(new Color32(24,31,43,255),width*height).ToArray();
        var renderer = hero.GetComponent<SpriteRenderer>();
        for (int row = 0; row < 3; row++) for (int d = 0; d < 4; d++)
        {
            Set(hero,"shieldFeedbackUntil", row == 0 ? 0 : Time.time + (row == 1 ? .18f : .04f));
            hero.ApplyCharacterVisual(renderer,Directions[d],false,false);
            var original = renderer.sprite;
            hero.ApplyCharacterVisual(renderer,Directions[d],false,false);
            Check(original == renderer.sprite,"feedback sprite cached rather than rebuilt each frame");
            var sprite = renderer.sprite.texture.GetPixels32();
            string field = new[] {"Front","Back","Left","Right"}[d];
            var bodyRows = (string[])typeof(HeroZeldaCharacterData).GetField(field,F).GetValue(null);
            var palette = typeof(HeroZeldaCharacterData).GetMethod("PixelColor",F);
            for (int y = 0; y < 6; y++) for (int x = 0; x < 16; x++)
                if (bodyRows[y][x] != '.') Check(sprite[(15-y)*16+x].Equals((Color32)(Color)palette.Invoke(hero,new object[]{bodyRows[y][x]})),
                    "shield flash/recoil does not recolor head: phase=" + row + " direction=" + d + " x=" + x + " row=" + y);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                var color = sprite[y*16+x]; if (color.a == 0) continue;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                    pixels[((2-row)*cell+16+y*scale+dy)*width+d*cell+16+x*scale+dx] = color;
            }
        }
        image.SetPixels32(pixels); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../Hero-DirectionalShield-preview.png"),image.EncodeToPNG());
        Object.Destroy(image);
    }
}
