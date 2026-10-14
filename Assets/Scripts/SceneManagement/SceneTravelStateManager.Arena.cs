using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class SceneTravelStateManager
{
    private void OnArenaWinnerDied(ZeldaCharacterData character)
    {
        if (Instance != this || character == null) return;
        string identity = character.IndividualAccessId;
        ArenaChallengeController.NotifyWinnerDeath(identity);
        // A winning body can die after leaving the arena. Update only live-world
        // snapshots, never a manual-save/quick-restart checkpoint. No per-frame scan.
        foreach (var scene in sceneStates.Values)
        {
            var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene.sceneName);
            if (loaded.IsValid() && loaded.isLoaded && ArenaChallengeController.HasControllerInScene(loaded.handle)) continue;
            List<ObjectState> changed = null;
            foreach (var record in scene.objects.Values)
            {
                var arena = record.arenaChallengeState;
                if (!record.hasArenaChallenge || arena == null || arena.claimed || arena.phase != ArenaChallengeController.Phase.Completed || arena.winnerId != identity) continue;
                var copy = JsonUtility.FromJson<ObjectState>(JsonUtility.ToJson(record));
                if (!ArenaChallengeController.ReopenForWinnerDeath(copy.arenaChallengeState, identity)) continue;
                if (changed == null) changed = new List<ObjectState>();
                changed.Add(copy);
            }
            if (changed == null) continue;
            if (ReferenceEquals(scene, quickRestartSceneState))
            {
                var checkpoint = new SceneState { sceneName = scene.sceneName };
                foreach (var entry in scene.objects) checkpoint.objects.Add(entry.Key, entry.Value);
                quickRestartSceneState = checkpoint;
            }
            foreach (var record in changed)
            {
                scene.objects[record.path] = record;
                QuestJournalManager.GetOrCreate().RecordArenaQuestProgress(record.arenaChallengeState);
            }
        }
    }

    [Serializable] private sealed class ArenaLayout
    {
        public string scene;
        public List<ObjectState> objects = new List<ObjectState>();
    }
    public string CaptureArenaLayout(ArenaChallengeController arena, ZeldaFourWayMover entrant)
    {
        var scene = arena.gameObject.scene;
        var snapshot = CreateSceneSnapshot(scene);
        var layout = new ArenaLayout { scene = scene.name };
        foreach (var root in scene.GetRootGameObjects())
        foreach (var target in root.GetComponentsInChildren<Transform>(true))
        {
            if (!arena.region.Contains(target.position) || target == arena.transform || target.IsChildOf(arena.transform) ||
                (entrant != null && (target == entrant.transform || target.IsChildOf(entrant.transform))) ||
                target.GetComponentInParent<PickupItemBase>() != null || target.GetComponentInParent<PlacedBomb>() != null ||
                target.GetComponentInParent<ClockworkPuppetRuntime>() != null || target.GetComponentInParent<BuilderTurretBond>() != null) continue;
            if (snapshot.objects.TryGetValue(BuildPath(target), out var state)) layout.objects.Add(state);
        }
        // Detached serialized records cannot subsequently be mutated by live captures.
        return JsonUtility.ToJson(layout);
    }

    public string MergeArenaLayout(string currentWorld, string layoutJson)
    {
        var world = JsonUtility.FromJson<DiskWorld>(currentWorld);
        var layout = JsonUtility.FromJson<ArenaLayout>(layoutJson);
        if (layout == null || layout.objects == null || layout.objects.Count == 0) throw new InvalidOperationException("竞技场布局快照缺失。");
        var scene = world.scenes.Find(s => s.name == layout.scene);
        if (scene == null) throw new InvalidOperationException("竞技场快照场景不匹配。");
        var objects = new Dictionary<string, ObjectState>();
        foreach (var state in scene.objects) objects[state.path] = state;
        var transforms = new Dictionary<string, DiskTransform>();
        foreach (var state in scene.transforms) transforms[state.path] = state;
        foreach (var original in layout.objects)
        {
            if (original.hasArenaChallenge) continue;
            if (IsTransformOnly(original))
            {
                transforms[original.path] = new DiskTransform { path = original.path, active = original.activeSelf,
                    position = original.localPosition, rotation = original.localRotation, scale = original.localScale };
                continue;
            }
            if (!objects.TryGetValue(original.path, out var live))
            {
                // Character/item deaths and consumption are never undone. Scenery can be reconstructed.
                if (original.hasCharacter || !string.IsNullOrEmpty(original.pickupId) || !string.IsNullOrEmpty(original.deviceKind)) continue;
                objects[original.path] = original;
                continue;
            }
            live.localPosition = original.localPosition; live.localRotation = original.localRotation;
            live.localScale = original.localScale; live.activeSelf = original.activeSelf;
            if (original.hasAi)
            {
                live.aiEnabled = original.aiEnabled; live.aiState = original.aiState; live.savedAi = original.savedAi;
                live.moverEnabled = original.moverEnabled;
            }
            if (original.hasDoor) { live.doorLocked = original.doorLocked; live.doorOpen = original.doorOpen; }
            // Everything else remains live: HP, energy, inventory, sockets, potion pool settings, quests, etc.
        }
        scene.objects = new List<ObjectState>(objects.Values);
        scene.transforms = new List<DiskTransform>(transforms.Values);
        return JsonUtility.ToJson(world);
    }
}
