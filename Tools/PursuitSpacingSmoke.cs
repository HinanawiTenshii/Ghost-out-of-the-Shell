using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PursuitSpacingSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ZeldaFourWayMover target;
    static int checks;
    static PursuitSpacingSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("PursuitSpacingSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("PursuitSpacingSmoke", false)) return;
        SessionState.SetBool("PursuitSpacingSmoke", false);
        try { Verify(); Clear(); Debug.Log("PURSUIT_SPACING_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(ZeldaCharacterAiBase ai, string field, object value) => typeof(ZeldaCharacterAiBase).GetField(field, Flags).SetValue(ai, value);
    static object Get(ZeldaCharacterAiBase ai, string field) => typeof(ZeldaCharacterAiBase).GetField(field, Flags).GetValue(ai);
    static object Invoke(ZeldaCharacterAiBase ai, string method, params object[] args) => typeof(ZeldaCharacterAiBase).GetMethod(method, Flags).Invoke(ai, args);
    static void Clear()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
    static GameObject Actor(string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        owned.Add(go); go.transform.position = position; go.GetComponent<Rigidbody2D>().position = position;
        var body = go.GetComponent<BoxCollider2D>(); body.size = Vector2.one * .6f; body.offset = Vector2.zero;
        return go;
    }
    static void Setup()
    {
        Clear(); Time.timeScale = 1;
        target = Actor("Target", new Vector2(100, 0)).GetComponent<ZeldaFourWayMover>();
    }
    static ZeldaCharacterAiBase Npc(float x, float speed, float y = 0)
    {
        var go = Actor("Pursuer", new Vector2(x, y)); go.GetComponent<ZeldaFourWayMover>().enabled = false;
        var data = new SerializedObject(go.GetComponent<CivilianZeldaCharacterData>());
        data.FindProperty("moveSpeed").floatValue = speed; data.ApplyModifiedPropertiesWithoutUndo();
        var ai = go.AddComponent<ZeldaCharacterAiBase>();
        Set(ai, "enableAwarenessSound", false); Set(ai, "solidCollisionLayers", (LayerMask)(~0));
        Set(ai, "currentState", ZeldaAiState.Hostile); Set(ai, "targetMover", target);
        Set(ai, "moveDirection", Vector2.right); Set(ai, "lastSeparationDesiredDirection", Vector2.right);
        Physics2D.SyncTransforms(); return ai;
    }
    static Vector2 Steer(ZeldaCharacterAiBase ai, Vector2 desired)
    {
        Set(ai, "nextSeparationRefreshTime", -1f);
        Vector2 result = (Vector2)Invoke(ai, "ApplyCharacterSeparation", desired);
        Set(ai, "moveDirection", result); return result;
    }
    static GameObject Wall(Vector2 center, Vector2 size)
    {
        var go = new GameObject("Wall", typeof(BoxCollider2D)); owned.Add(go);
        go.transform.position = center; go.GetComponent<BoxCollider2D>().size = size; Physics2D.SyncTransforms(); return go;
    }
    static void Advance(ZeldaCharacterAiBase ai, Vector2 direction)
    {
        var rb = ai.GetComponent<Rigidbody2D>();
        Vector2 displacement = direction * ai.GetComponent<ZeldaCharacterData>().MoveSpeed * .02f;
        displacement = (Vector2)Invoke(ai, "GetBlockedDisplacement", displacement);
        rb.position += displacement; ai.transform.position = rb.position; Physics2D.SyncTransforms();
    }
    static void Verify()
    {
        Setup(); var slow = Npc(0, 2); var fast = Npc(-1.6f, 4);
        Vector2 d = Steer(fast, Vector2.right);
        Check(d.x > .1f && Mathf.Abs(d.y) > .1f, "faster same-heading pursuer starts passing: " + d);
        float side = Mathf.Sign(d.y);
        for (int i = 0; i < 10; i++) Check(Mathf.Sign(Steer(fast, Vector2.right).y) == side, "stable passing side");
        Check(Steer(slow, Vector2.right) == Vector2.right, "front actor is not slowed by faster follower");
        for (int i = 0; i < 240; i++)
        {
            var a = Steer(fast, Vector2.right); var b = Steer(slow, Vector2.right);
            Advance(slow, b); Advance(fast, a);
            Vector2 delta = fast.transform.position - slow.transform.position;
            Check(Mathf.Abs(delta.x) >= .595f || Mathf.Abs(delta.y) >= .595f, "moving bodies never overlap at step " + i);
        }
        Check(fast.transform.position.x > slow.transform.position.x + 1f, "faster actor completes overtake: " + fast.transform.position + " / " + slow.transform.position);
        Check(Get(fast, "pursuitPassingActor") == null, "passing target released once whole body clears");

        Setup(); fast = Npc(-1.6f, 4); slow = Npc(0, 2);
        d = Steer(fast, Vector2.right);
        Check(d.x > .1f && Mathf.Abs(d.y) > .1f, "overtaking does not depend on creation/priority order");
        Set(slow, "moveDirection", Vector2.left); Set(slow, "lastSeparationDesiredDirection", Vector2.left);
        Steer(fast, Vector2.right);
        Check(Get(fast, "pursuitPassingActor") == null, "leader reversing direction returns to normal head-on avoidance");

        Setup(); slow = Npc(0, 2); fast = Npc(-.8f, 2);
        d = Steer(fast, Vector2.right);
        Check(d.x > 0 && d.x < .95f && Mathf.Abs(d.y) < .01f, "equal speed follows with a gap: " + d);
        Check((float)Get(fast, "cachedCharacterYieldWeight") > 0, "intentional following suppresses stuck path rebuild");
        for (int i = 0; i < 120; i++) { var a = Steer(fast, Vector2.right); Advance(slow, Vector2.right); Advance(fast, a); }
        float expectedGap = (float)Invoke(fast, "PursuitBodyGap", Vector2.right, slow.GetComponent<ZeldaCharacterData>());
        Check(slow.transform.position.x - fast.transform.position.x > expectedGap - .05f,
            "following converges to body-sized gap: actual=" + (slow.transform.position.x - fast.transform.position.x) + " expected=" + expectedGap);

        Setup(); slow = Npc(0, 2); fast = Npc(-1.3f, 4);
        d = Steer(fast, Vector2.right); side = Mathf.Sign(d.y);
        Set(fast, "pursuitPassingActor", null);
        Wall(new Vector2(0, side * .62f), new Vector2(20, .2f));
        d = Steer(fast, Vector2.right);
        Check(Mathf.Sign(d.y) == -side && Mathf.Abs(d.y) > .1f, "blocked preferred lane selects open side: " + d);

        Setup(); slow = Npc(0, 2); fast = Npc(-.85f, 4);
        Wall(new Vector2(5, .58f), new Vector2(30, .2f)); Wall(new Vector2(5, -.58f), new Vector2(30, .2f));
        d = Steer(fast, Vector2.right);
        Check(Mathf.Abs(d.y) < .01f && d.x > 0 && d.x < .55f, "narrow passage follows instead of oscillating: " + d);
        for (int i = 0; i < 180; i++) { var a = Steer(fast, Vector2.right); Advance(slow, Vector2.right); Advance(fast, a); }
        Check(slow.transform.position.x > 7 && slow.transform.position.x - fast.transform.position.x > .85f &&
              Mathf.Abs(fast.transform.position.y) < .01f, "narrow passage makes progress with spacing");

        Setup(); slow = Npc(0, 2); fast = Npc(-.65f, 4); Set(slow, "moveDirection", Vector2.zero);
        d = Steer(fast, Vector2.right);
        Check(Mathf.Abs(d.y) > .9f && Mathf.Abs(d.x) < .01f, "close stopped leader is bypassed sideways without deadlock: " + d);

        Setup(); slow = Npc(0, 2); fast = Npc(-1.4f, 4);
        d = Steer(fast, Vector2.right); side = Mathf.Sign(d.y); Set(fast, "pursuitPassingActor", null);
        var third = Npc(0, 2, side * 1f);
        d = Steer(fast, Vector2.right);
        Check(Mathf.Sign(d.y) == -side, "third character occupying lane selects other side: " + d);

        Setup(); slow = Npc(0, 2); fast = Npc(-1.4f, 2);
        slow.transform.localScale = Vector3.one * 2f; fast.transform.localScale = Vector3.one * 2f; Physics2D.SyncTransforms();
        d = Steer(fast, Vector2.right);
        Check(d.x < .99f, "following gap scales with collider size: " + d);
        Steer(fast, Vector2.zero); Check(Get(fast, "pursuitPassingActor") == null, "stopping/attacking clears passing commitment");

        Setup();
        var crowd = new ZeldaCharacterAiBase[6];
        var directions = new Vector2[6];
        for (int i = 0; i < crowd.Length; i++) crowd[i] = Npc(-1.1f * i, 2f + i * .5f);
        for (int step = 0; step < 360; step++)
        {
            // Keep steering for six physics ticks, matching the normal 0.12 s
            // separation interval rather than granting the algorithm a per-tick scan.
            if (step % 6 == 0)
                for (int i = 0; i < crowd.Length; i++) directions[i] = Steer(crowd[i], Vector2.right);
            for (int i = 0; i < crowd.Length; i++) Advance(crowd[i], directions[i]);
            for (int i = 0; i < crowd.Length; i++) for (int j = i + 1; j < crowd.Length; j++)
            {
                Vector2 delta = crowd[i].transform.position - crowd[j].transform.position;
                Check(Mathf.Abs(delta.x) >= .595f || Mathf.Abs(delta.y) >= .595f, "crowd remains collision safe at step " + step);
            }
        }
        for (int i = 0; i < crowd.Length; i++)
            Check(crowd[i].transform.position.x > -1.1f * i + 8f, "every pursuer makes progress: " + i + " " + crowd[i].transform.position);
        Check(crowd[5].transform.position.x > crowd[0].transform.position.x, "faster rear actor can overtake a group");

        Setup(); slow = Npc(0, 2); fast = Npc(-1.6f, 4);
        var otherScene = SceneManager.CreateScene("PursuitOtherScene"); SceneManager.MoveGameObjectToScene(slow.gameObject, otherScene);
        Check(Steer(fast, Vector2.right) == Vector2.right, "unrelated loaded scene cannot steer pursuit");
    }
}
