using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SharedHostilitySmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static ZeldaFourWayMover player;
    static int checks;
    static SharedHostilitySmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("SharedHostilitySmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("SharedHostilitySmoke", false)) return;
        if (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) return;
        SessionState.SetBool("SharedHostilitySmoke", false);
        try { Verify(); Clear(); Debug.Log("SHARED_HOSTILITY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static void Set(ZeldaCharacterAiBase ai, string field, object value) => typeof(ZeldaCharacterAiBase).GetField(field, Flags).SetValue(ai, value);
    static void Invoke(ZeldaCharacterAiBase ai, string method, params object[] args) => typeof(ZeldaCharacterAiBase).GetMethod(method, Flags).Invoke(ai, args);
    static void Clear()
    {
        foreach (var timer in Object.FindObjectsOfType<SharedPlayerHostilityTimer>()) Object.DestroyImmediate(timer.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]); owned.Clear();
    }
    static GameObject Actor(string name, Vector2 position)
    {
        var go = new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover));
        owned.Add(go); go.transform.position = position; go.GetComponent<Rigidbody2D>().position = position; return go;
    }
    static ZeldaCharacterAiBase Npc(float x, float y = 0, Type type = null)
    {
        var go = Actor("Guard", new Vector2(x, y)); go.GetComponent<ZeldaFourWayMover>().enabled = false;
        var ai = (ZeldaCharacterAiBase)go.AddComponent(type ?? typeof(ZeldaCharacterAiBase));
        Set(ai, "enableAwarenessSound", false); Set(ai, "visionObstacleLayers", (LayerMask)0);
        Set(ai, "solidCollisionLayers", (LayerMask)0); Set(ai, "visionRadius", 3f);
        Set(ai, "facingDirection", Vector2.down);
        return ai;
    }
    static SharedPlayerHostilityTimer Setup(out ZeldaCharacterAiBase first, out ZeldaCharacterAiBase second)
    {
        Clear(); Time.timeScale = 1;
        player = Actor("Player", new Vector2(80, 0)).GetComponent<ZeldaFourWayMover>();
        first = Npc(0); second = Npc(20);
        first.OnCharacterDamagedBy(player.CharacterData); second.OnCharacterDamagedBy(player.CharacterData);
        var timer = SharedPlayerHostilityTimer.ForScene(SceneManager.GetActiveScene());
        Check(timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile && timer.Remaining == 9, "default shared nine seconds");
        return timer;
    }
    static void Verify()
    {
        Check(typeof(ZeldaCharacterAiBase).GetField("regularHostileLoseSightDelay", Flags) == null &&
            typeof(ZeldaCharacterAiBase).GetField("regularHostileLostSightTimer", Flags) == null &&
            typeof(ZeldaCharacterAiBase).GetField("searchDuration", Flags) == null &&
            typeof(ZeldaCharacterAiBase).GetField("searchTimer", Flags) == null, "obsolete per-guard player timer fields removed");
        var timer = Setup(out var a, out var b);
        timer.Advance(8);
        Check(timer.Remaining == 1, "shared countdown once, independent of guard count; remaining=" + timer.Remaining + " phase=" + timer.Phase + " preparing=" + RetroSceneLoadReveal.IsPreparingGameplay);
        Invoke(a, "UpdateTargetAndState", 20f); Invoke(b, "UpdateTargetAndState", 20f);
        Check(a.CurrentState == ZeldaAiState.Hostile && b.CurrentState == ZeldaAiState.Hostile && timer.Remaining == 1,
            "old per-NPC timeout cannot end pursuit; retaliation is not infinite");
        timer.Advance(1);
        Check(a.CurrentState == ZeldaAiState.Search && b.CurrentState == ZeldaAiState.Search && timer.Remaining == 12,
            "simultaneous shared search for twelve seconds");
        Set(a, "isTravelingToInvestigation", true);
        timer.Advance(5); Invoke(a, "UpdateTargetAndState", 30f); Invoke(b, "UpdateTargetAndState", 30f);
        Check(a.CurrentState == ZeldaAiState.Search && b.CurrentState == ZeldaAiState.Search && timer.Remaining == 7,
            "search is global even while one guard is traveling");
        var observer = Npc(80, 1); // Neutral observer, not already part of the pursuit.
        Physics2D.SyncTransforms();
        Check((bool)typeof(ZeldaCharacterAiBase).GetMethod("CanObserveSharedPlayer", Flags).Invoke(observer, new object[] { player }),
            "observer sees player; rb=" + observer.GetComponent<Rigidbody2D>().position + " player=" + player.transform.position);
        timer.Advance(.1f);
        Check(timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile && timer.Remaining == 9 &&
            a.IsActivelyHostileTo(player) && b.IsActivelyHostileTo(player), "any eligible observer refreshes and re-engages all pursuers");
        player.transform.position = new Vector2(100, 0); timer.Advance(2);
        Check(timer.Remaining == 7, "countdown resumes after losing all observers");
        player.transform.position = new Vector2(80, 0); timer.Advance(.1f);
        Check(timer.Remaining == 9, "hostile countdown resets to full upon sighting");
        player.transform.position = new Vector2(100, 0); timer.Advance(9); timer.Advance(11.9f);
        Check(a.CurrentState == ZeldaAiState.Search && b.CurrentState == ZeldaAiState.Search, "no premature recovery");
        timer.Advance(.11f);
        Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery && timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle,
            "all guards return when shared search expires");

        timer = Setup(out a, out b); timer.Advance(3);
        Time.timeScale = 0; timer.Advance(5); Check(timer.Remaining == 6, "pause freezes clock"); Time.timeScale = 1;
        a.StunForDuration(30); timer.Advance(6);
        Check(a.IsStunned && b.CurrentState == ZeldaAiState.Search, "shared timeout respects stun");
        timer.Advance(12); Invoke(a, "FinishStun");
        Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery, "expired shared search queues return until stun ends");

        timer = Setup(out a, out b); timer.Advance(9); timer.Advance(4);
        var savedA = a.CaptureSaveState(); var savedB = b.CaptureSaveState();
        Check(savedA.sharedPlayerPhase == 2 && savedA.sharedPlayerRemaining == 8 && savedB.sharedPlayerRemaining == 8, "save captures same global search progress");
        Object.DestroyImmediate(timer.gameObject);
        a.ApplySaveState(savedA); b.ApplySaveState(savedB);
        timer = SharedPlayerHostilityTimer.ForScene(SceneManager.GetActiveScene());
        Check(timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Search && timer.Remaining == 8, "restore resumes remaining time, not a fresh timer per guard");
        timer.Advance(8); Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery, "restored search expires together");

        timer = Setup(out a, out b); timer.Advance(3); ZeldaCharacterAiBase.NotifyPlayerEchoRewind(player); timer.Advance(1);
        Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery && timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle,
            "echo rewind clears shared incident without re-aggro");

        timer = Setup(out a, out b); var unrelated = Npc(40);
        unrelated.InvestigatePosition(new Vector2(41, 0)); timer.Advance(9);
        Check(unrelated.CaptureSaveState().sharedPlayerPhase == 0, "ordinary sound investigation does not join player incident");
        var otherScene = SceneManager.CreateScene("Shared timer other level");
        var otherTimer = SharedPlayerHostilityTimer.ForScene(otherScene);
        Check(otherTimer != timer && otherTimer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle, "timers isolated per scene");
        Check(Object.FindObjectsOfType<SharedPlayerHostilityTimer>().Length == 2, "one singleton per scene");
        player.enabled = false; var newBody = Actor("New controlled body", new Vector2(100, 0)).GetComponent<ZeldaFourWayMover>();
        timer.Advance(1);
        Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery, "unwitnessed control switch does not transfer pursuit automatically");

        timer = Setup(out a, out b);
        for (int i = 0; i < 30; i++) Npc(-10 - i * 5).OnCharacterDamagedBy(player.CharacterData);
        timer.Advance(1);
        Check(timer.Remaining == 8, "32 participants still consume exactly one second");
        var config = new SerializedObject(timer);
        config.FindProperty("hostileDuration").floatValue = 2;
        config.FindProperty("searchDuration").floatValue = 3;
        config.ApplyModifiedPropertiesWithoutUndo();
        Npc(-200).OnCharacterDamagedBy(player.CharacterData);
        timer.Advance(2); Check(timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Search && timer.Remaining == 3,
            "scene-authored durations are effective");
        timer.Advance(3); Check(a.CurrentState == ZeldaAiState.Recovery, "configured search duration expires");
        timer = Setup(out a, out b); timer.Advance(9); timer.Advance(4);
        a.OnCharacterDamagedBy(player.CharacterData);
        Check(timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Hostile && timer.Remaining == 9 &&
            a.IsActivelyHostileTo(player) && b.IsActivelyHostileTo(player), "new attack during shared search restarts hostility for everyone");

        timer = Setup(out a, out b);
        Vector2 lastSeen = timer.LastSeenPosition;
        player.transform.position = new Vector2(0, 80); Physics2D.SyncTransforms();
        timer.Advance(4);
        Invoke(a, "TickHostile", .016f);
        Vector2 direction = (Vector2)typeof(ZeldaCharacterAiBase).GetField("moveDirection", Flags).GetValue(a);
        Check(a.FacingDirection.y > .99f && direction.y > .9f, "unseen player is pursued at live position, not old sighting");
        player.transform.position = new Vector2(-80, 0); Physics2D.SyncTransforms();
        timer.Advance(4); Invoke(a, "TickHostile", .016f);
        Check(a.FacingDirection.x < -.99f && timer.Remaining == 1, "pursuit keeps tracking changes until timeout without refreshing unseen timer");
        Check(timer.LastSeenPosition == lastSeen, "live pursuit does not overwrite actual sighting memory");
        Vector2 firstCenter = new Vector2(5, 7), secondCenter = new Vector2(25, -6);
        a.GetComponent<Rigidbody2D>().position = firstCenter;
        b.GetComponent<Rigidbody2D>().position = secondCenter;
        Set(a, "searchRadius", 3.2f);
        timer.Advance(1);
        Check(a.CurrentState == ZeldaAiState.Search && a.CaptureSaveState().searchCenter == firstCenter &&
            b.CaptureSaveState().searchCenter == secondCenter,
            "shared timeout anchors each search at that guard's current position, not player memory or spawn");
        for (int i = 0; i < 40; i++)
        {
            Invoke(a, "ChooseNextSearchWaypoint"); Invoke(b, "ChooseNextSearchWaypoint");
            Check(Vector2.Distance(a.CaptureSaveState().searchWaypoint, firstCenter) <= 3.2001f &&
                Vector2.Distance(b.CaptureSaveState().searchWaypoint, secondCenter) <= 1.8001f,
                "each guard samples within its own configured search radius");
        }
        a.GetComponent<Rigidbody2D>().position = firstCenter + Vector2.right * .5f;
        timer.Advance(.1f); Invoke(a, "UpdateTargetAndState", .1f);
        Check(a.CaptureSaveState().searchCenter == firstCenter, "search centre does not drift with later movement");
        var searchSave = a.CaptureSaveState();
        a.ApplySaveState(searchSave); timer.Advance(.1f);
        Check(a.CaptureSaveState().searchCenter == firstCenter, "save/load retains the search-entry centre");
        a.OnCharacterDamagedBy(player.CharacterData);
        Vector2 newCenter = new Vector2(-10, -15);
        a.GetComponent<Rigidbody2D>().position = newCenter;
        timer.Advance(9);
        Check(a.CaptureSaveState().searchCenter == newCenter, "new search captures a new entry centre");

        VerifySearchMovement();
    }

    static T Get<T>(ZeldaCharacterAiBase ai, string field) => (T)typeof(ZeldaCharacterAiBase).GetField(field, Flags).GetValue(ai);
    static void VerifySearchMovement()
    {
        var timer = Setup(out var a, out var b);
        timer.Advance(9);
        Check(Get<float>(a,"sharedSearchWaypointPauseDuration") == 2f && Get<float>(a,"sharedSearchMinimumMoveDistance") == 1.2f,
            "shared search defaults: two-second pauses, 1.2-unit minimum movement");
        Vector2 center = a.CaptureSaveState().searchCenter;
        int movingLegs = 0;
        for (int i = 0; i < 80; i++)
        {
            Vector2 origin = a.GetComponent<Rigidbody2D>().position;
            Invoke(a,"ChooseNextSearchWaypoint");
            Vector2 point = a.CaptureSaveState().searchWaypoint;
            float distance = Vector2.Distance(origin, point);
            if (distance >= 1.3499f) movingLegs++;
            Check((distance >= 1.3499f || distance < .0001f) && Vector2.Distance(center,point) <= 1.8001f,
                "every search leg includes minimum travel plus arrival tolerance within original search region");
            a.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
        }
        Check(movingLegs > 70, "open search area normally produces movement rather than waiting");
        Vector2 arrived = a.GetComponent<Rigidbody2D>().position;
        Set(a,"cornerEscapeTimer",1f); Set(a,"cornerEscapeDirection",Vector2.left);
        Invoke(a,"TickSearch",.02f);
        Check(a.CaptureSaveState().sharedSearchPauseRemaining == 2f && Get<Vector2>(a,"moveDirection") == Vector2.zero &&
            Get<float>(a,"cornerEscapeTimer") == 0f, "arrival pauses instead of resampling and cancels old escape movement");
        Invoke(a,"TickSearch",.5f);
        var saved = JsonUtility.FromJson<ZeldaCharacterAiBase.SaveState>(JsonUtility.ToJson(a.CaptureSaveState()));
        Check(Mathf.Abs(saved.sharedSearchPauseRemaining - 1.5f) < .001f, "remaining pause serializes");
        a.ApplySaveState(saved);
        Check(Mathf.Abs(a.CaptureSaveState().sharedSearchPauseRemaining - 1.5f) < .001f, "save/load preserves partial pause");
        var randomBeforePause = UnityEngine.Random.state;
        Invoke(a,"TickSearch",1.49f);
        Check(Get<Vector2>(a,"moveDirection") == Vector2.zero && a.CaptureSaveState().searchWaypoint == arrived &&
            UnityEngine.Random.state.Equals(randomBeforePause), "no navigation sampling or motion during pause");
        Invoke(a,"TickSearch",.02f);
        Check(a.CaptureSaveState().sharedSearchPauseRemaining == 0f && Vector2.Distance(arrived,a.CaptureSaveState().searchWaypoint) >= 1.3499f,
            "pause expiry chooses next sufficiently distant point");

        // An old save has no pause field. The next arrival still gets the new cadence.
        saved.sharedSearchPauseRemaining = 0f; a.ApplySaveState(saved); Invoke(a,"TickSearch",.02f);
        Check(a.CaptureSaveState().sharedSearchPauseRemaining == 2f, "old save starts using new cadence at next arrival");
        Set(a,"facingDirection",Vector2.up);
        player.transform.position = a.transform.position + Vector3.up; player.GetComponent<Rigidbody2D>().position = player.transform.position;
        Physics2D.SyncTransforms(); timer.Advance(.1f);
        Check(a.CurrentState == ZeldaAiState.Hostile && a.CaptureSaveState().sharedSearchPauseRemaining == 0f && timer.Remaining == 9f,
            "seeing player interrupts pause immediately and restores shared hostility");

        timer = Setup(out a,out b); Set(a,"searchRadius",.25f); timer.Advance(9);
        Invoke(a,"TickSearch",.02f);
        randomBeforePause = UnityEngine.Random.state;
        for (int i = 0; i < 30; i++) Invoke(a,"TickSearch",.05f);
        Check(Get<Vector2>(a,"moveDirection") == Vector2.zero && UnityEngine.Random.state.Equals(randomBeforePause),
            "insufficient space waits rather than tiny moves or per-frame resampling");
        Invoke(a,"TickSearch",.51f);
        Check(a.CaptureSaveState().sharedSearchPauseRemaining == 2f && a.CaptureSaveState().searchWaypoint == Vector2.zero,
            "bounded retry still cannot choose undersized leg");
        timer.Advance(12);
        Check(a.CurrentState == ZeldaAiState.Recovery && b.CurrentState == ZeldaAiState.Recovery,
            "pauses do not extend shared search duration");

        timer = Setup(out a,out b); timer.Advance(9);
        Invoke(a,"TickSearch",.02f); Invoke(a,"TickSearch",1.3f);
        Check(a.CaptureSaveState().sharedSearchPauseRemaining == 2f, "stalled route also waits before retrying");
        var independent = Npc(40,0,typeof(SkeletonCharacterAi));
        Set(independent,"searchRadius",.5f); Invoke(independent,"BeginSearch");
        independent.GetComponent<Rigidbody2D>().position = independent.CaptureSaveState().searchWaypoint;
        Invoke(independent,"TickSearch",.02f);
        Check(independent.CaptureSaveState().sharedSearchPauseRemaining == 0f &&
            independent.CaptureSaveState().sharedPlayerPhase == 0, "independent creature search is unchanged");
        Debug.Log("SHARED_SEARCH_MOVEMENT_PASS totalChecks=" + checks);
    }
}
