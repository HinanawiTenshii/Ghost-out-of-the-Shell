using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Run in the isolated Unity project, never in the user's open scene.
[InitializeOnLoad]
public static class SandwormCharacterDataSmoke
{
    const string Pending = "SandwormCharacterDataSmoke.Pending";
    const string PrefabPath = "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> Owned = new List<GameObject>();
    static IEnumerator test;
    static int checks, lastFrame = -1;
    static SandwormCharacterDataSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception("Sandworm data: " + label);
    }
    static GameObject Own(GameObject value) { Owned.Add(value); return value; }
    static void Cleanup() { foreach (var item in Owned) if (item != null) Object.DestroyImmediate(item); Owned.Clear(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                test = Verify();
            }
            if (test == null || test.MoveNext()) return;
            test = null; Cleanup();
            Debug.Log("SANDWORM_CHARACTER_DATA_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { test = null; Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Check(prefab != null, "prefab imports");
        var root = Own(Object.Instantiate(prefab));
        var data = root.GetComponent<SandwormZeldaCharacterData>();
        var worm = root.GetComponent<SandwormBossPrototype>();
        var states = root.GetComponent<SandwormStateController>();
        Check(states != null && states.IsUnderground && states.State == SandwormState.Underground, "defaults to underground");
        var sorting = root.GetComponent<SortingGroup>();
        Check(sorting != null && sorting.enabled && sorting.sortingOrder == -9 && sorting.sortingLayerID == 0, "default underground uses the below-world sorting band");
        Check(!data.ParticipatesInCharacterCollision, "underground is excluded from character collision");
        foreach (var part in root.GetComponentsInChildren<Collider2D>()) Check(!part.enabled, "all underground colliders disabled");
        foreach (var visual in root.GetComponentsInChildren<SandwormSegmentVisual>()) Check(visual.HasUndergroundOverlay, "all underground parts are masked");
        states.EnterAboveGround();
        Check(!sorting.enabled, "surface restores individual authored sorting by disabling the group");
        Check(data != null && root.GetComponentsInChildren<ZeldaCharacterData>(true).Length == 1, "one shared data component");
        Check(data.Health == 60 && data.CurrentHealth == 60, "initial and maximum HP 60");
        Check(data.AttackPower == 10 && data.FinalAttackPower == 10, "attack power 10");
        Check(data.SkillValue == 0 && data.FinalSkillValue == 0, "skill value 0");
        Check(data.PossessionEnergy == 5 && data.FinalPossessionEnergy == 5 && data.PossessionCost == 6, "possession 5 / cost 6");
        Check(data.ParticipatesInCharacterCollision, "living sandworm is solid to ordinary characters");
        Check(root.GetComponent<ZeldaFourWayMover>() == null && root.GetComponent<ZeldaCharacterAiBase>() == null, "no humanoid controller replaces roaming");
        worm.SetMovementEnabled(false);
        var physics = root.GetComponent<Rigidbody2D>();
        var colliders = root.GetComponentsInChildren<CapsuleCollider2D>();
        Check(colliders.Length == 8 && root.GetComponentsInChildren<Collider2D>().Length == 8, "eight capsule colliders");
        Check(physics != null && physics.bodyType == RigidbodyType2D.Kinematic && physics.gravityScale == 0, "shared gravity-free kinematic body");
        foreach (var collider in colliders)
        {
            Check(collider.enabled && !collider.isTrigger && collider.attachedRigidbody == physics, "solid compound part");
            Check(collider.GetComponentInParent<ZeldaCharacterData>() == data, "part routes hits to shared HP");
            Check(collider.GetComponent<SandwormSegmentVisual>() != null && collider.gameObject.layer == 0, "collider follows visible part, not Blocks layer");
        }
        Physics2D.SyncTransforms();
        var probe = Own(new GameObject("Ordinary character collision probe"));
        probe.SetActive(false);

        var mover = probe.AddComponent<ZeldaFourWayMover>();
        mover.enabled = false;
        probe.SetActive(true);
        probe.GetComponent<BoxCollider2D>().size = Vector2.one * .4f;
        foreach (var collider in colliders)
        {
            probe.transform.position = collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 1f);
            Physics2D.SyncTransforms();
            object[] args = { Vector2.down * 2f, Vector2.zero };
            var allowed = (Vector2)typeof(ZeldaFourWayMover).GetMethod("GetLinearBlockedDisplacement", Private).Invoke(mover, args);
            Check(allowed.magnitude < 1.1f, "actual character movement is blocked by " + collider.name);
        }
        probe.SetActive(false);

        // The exact same crossing becomes unobstructed underground, and movement continues.
        probe.SetActive(true);
        states.EnterUnderground();
        Physics2D.SyncTransforms();
        Check(sorting.enabled && sorting.sortingOrder == -9, "re-entering underground restores low sorting");
        object[] undergroundArgs = { Vector2.down * 2f, Vector2.zero };
        var unblocked = (Vector2)typeof(ZeldaFourWayMover).GetMethod("GetLinearBlockedDisplacement", Private).Invoke(mover, undergroundArgs);
        Check(Mathf.Abs(unblocked.magnitude - 2f) < .001f, "player can pass through underground body");
        var undergroundStart = worm.HeadPosition;
        worm.SetMovementEnabled(true);
        Check(worm.TrySetDestination(new Vector2(8, 0)), "underground navigation remains enabled");
        worm.Advance(.1f);
        Check(Vector2.Distance(worm.HeadPosition, undergroundStart) > .1f, "underground keeps roaming");
        worm.SetMovementEnabled(false);
        states.EnterAboveGround();
        probe.SetActive(false);

        var hitObject = Own(new GameObject("Multi-segment damage probe"));
        hitObject.transform.position = new Vector3(1000, 1000, 0);
        var hit = hitObject.AddComponent<ZeldaAttackHitbox>();
        hit.enabled = false;
        typeof(ZeldaAttackHitbox).GetField("attackPower", Private).SetValue(hit, 7);
        var tryDamage = typeof(ZeldaAttackHitbox).GetMethod("TryDamage", Private);
        foreach (var collider in colliders) tryDamage.Invoke(hit, new object[] { collider });
        Check(data.CurrentHealth == 53, "one attack touching eight parts only deals damage once");
        var savedHeadSprite = worm.Head.GetComponent<SpriteRenderer>().sprite;
        worm.SetMovementEnabled(true);
        Check(worm.TrySetDestination(new Vector2(8, 0)), "colliders do not block own roaming");
        for (int i = 0; i < 20; i++)
        {
            worm.Advance(.02f);
            var position = worm.Head.localPosition;
            typeof(SandwormZeldaCharacterData).GetMethod("Update", Private).Invoke(data, null);
            Check(Vector3.Distance(worm.Head.localPosition, position) < .0001f, "damage feedback never rewinds head");
        }
        Check(worm.HeadPosition.x > 1, "roaming continues after a hit");
        Check(worm.Head.GetComponent<SpriteRenderer>().sprite == savedHeadSprite, "character data preserves the pixel sprite");
        worm.SetMovementEnabled(false);
        Physics2D.SyncTransforms();
        Check(colliders[0].OverlapPoint(worm.Head.position), "moving collider follows head");

        Check(data.TrySpendPossessionEnergy(2), "energy spending uses shared character system");
        var state = data.CaptureRuntimeState();
        var restored = Own(Object.Instantiate(prefab));
        restored.transform.position = new Vector3(100, 100, 0);
        restored.GetComponent<SandwormBossPrototype>().SetMovementEnabled(false);
        var restoredData = restored.GetComponent<SandwormZeldaCharacterData>();
        restoredData.ApplyRuntimeState(state);
        Check(restored.GetComponent<SandwormStateController>().State == SandwormState.AboveGround, "surface state round-trips through character save data");
        Check(restoredData.CurrentHealth == 53 && restoredData.Health == 60, "shared health round-trips through runtime state");
        Check(restoredData.PossessionEnergy == 3 && restoredData.MaxPossessionEnergy == 5 && restoredData.PossessionCost == 6, "spent energy and cost round-trip");
        Check(restoredData.AttackPower == 10 && restoredData.SkillValue == 0, "restored prefab keeps combat attributes");
        restoredData.RestorePossessionEnergy(2);
        Check(restoredData.PossessionEnergy == 5, "restored energy cap remains five");
        states.EnterUnderground();
        restoredData.ApplyRuntimeState(data.CaptureRuntimeState());
        Check(restored.GetComponent<SandwormStateController>().IsUnderground && !restoredData.ParticipatesInCharacterCollision, "underground state round-trips");
        foreach (var part in restored.GetComponentsInChildren<Collider2D>()) Check(!part.enabled, "restored underground collision remains off");
        states.enabled = false;
        Check(!states.IsUnderground && data.ParticipatesInCharacterCollision, "disabling state controller restores normal character");
        Check(!sorting.enabled, "disabling controller also restores ordinary sorting");
        states.enabled = true;
        Check(states.IsUnderground && !data.ParticipatesInCharacterCollision, "reenabling reapplies configured state");
        Check(sorting.enabled && sorting.sortingOrder == -9, "reenabling restores underground sorting");
        data.TakeDamage(60);
        Check(data.IsDead && data.CurrentHealth == 0, "lethal damage enters shared death lifecycle");
        yield return null;
        Check(root == null, "death destroys root");
        foreach (var collider in colliders) Check(collider == null, "death removes all part colliders");
    }
}
