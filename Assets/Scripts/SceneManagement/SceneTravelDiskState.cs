using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class SceneTravelStateManager
{
    [Serializable] private sealed class DiskScene
    {
        public string name;
        public List<ObjectState> objects = new List<ObjectState>();
        public List<DiskTransform> transforms = new List<DiskTransform>();
    }
    // Renderer/collider-only objects do not need hundreds of default character
    // and device fields. Keep their exact state with a compact, lossless record.
    [Serializable] private sealed class DiskTransform
    {
        public string path;
        public bool active;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    [Serializable] private sealed class DiskWorld
    {
        public List<DiskScene> scenes = new List<DiskScene>();
    }
    [Serializable] public sealed class DiskPlayer
    {
        public string path, name;
        public Vector3 position;
        public Vector2 facing;
        public float stamina;
        public ZeldaCharacterData.RuntimeState character;
        public List<SavedScalarFields.Value> configuration;
        public PersistentInventory.RuntimeState inventory;
        public PlayerGrowthAttributes.RuntimeState growth;
        public string wornBoxId;
        public List<SavedScalarFields.Value> wornBoxFields;
    }

    public string CaptureDiskWorld()
    {
        CaptureScene(SceneManager.GetActiveScene());
        var world = new DiskWorld();
        foreach (var scene in sceneStates.Values)
        {
            var disk = new DiskScene { name = scene.sceneName };
            foreach (var state in scene.objects.Values)
            {
                if (IsTransformOnly(state)) disk.transforms.Add(new DiskTransform { path = state.path,
                    active = state.activeSelf, position = state.localPosition, rotation = state.localRotation, scale = state.localScale });
                else disk.objects.Add(state);
            }
            world.scenes.Add(disk);
        }
        return JsonUtility.ToJson(world);
    }

    internal static string CompactLegacyDiskWorld(string json)
    {
        // Newly captured worlds already contain compact records; migrate an old
        // entry checkpoint once, not by parsing every checkpoint at each save.
        if (string.IsNullOrEmpty(json) || json.Contains("\"transforms\":")) return json;
        var world = JsonUtility.FromJson<DiskWorld>(json);
        if (world == null || world.scenes == null) throw new InvalidOperationException("初始场景快照不完整。");
        foreach (var scene in world.scenes)
        {
            if (scene.transforms == null) scene.transforms = new List<DiskTransform>();
            for (int i = scene.objects.Count - 1; i >= 0; i--)
            {
                var state = scene.objects[i];
                if (!IsTransformOnly(state)) continue;
                scene.transforms.Add(new DiskTransform { path = state.path, active = state.activeSelf,
                    position = state.localPosition, rotation = state.localRotation, scale = state.localScale });
                scene.objects.RemoveAt(i);
            }
        }
        return JsonUtility.ToJson(world);
    }

    public DiskPlayer CaptureDiskPlayer()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null) throw new InvalidOperationException("当前没有可保存的受控角色。");
        var data = mover.GetComponent<ZeldaCharacterData>();
        return new DiskPlayer {
            path = mover.gameObject.scene == SceneManager.GetActiveScene() ? BuildPath(mover.transform) : "",
            name = mover.name, position = mover.transform.position, facing = mover.FacingDirection,
            stamina = mover.CurrentStamina, character = data.CaptureRuntimeState(),
            configuration = SavedScalarFields.Capture(data),
            wornBoxId = mover.ActiveCardboardBox != null ? mover.ActiveCardboardBox.SaveSourceItemId : "",
            wornBoxFields = mover.ActiveCardboardBox != null ? SavedScalarFields.Capture(mover.ActiveCardboardBox, true) : null,
            inventory = PersistentInventory.Instance != null ? PersistentInventory.Instance.CaptureRuntimeState() : default,
            growth = PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.CaptureRuntimeState() : default
        };
    }

    public void RestoreDiskWorld(string json, string sceneName, DiskPlayer player, SaveGameCatalog catalog)
    {
        // Validate and resolve the template before discarding the live session.
        var world = JsonUtility.FromJson<DiskWorld>(json);
        if (world == null || world.scenes == null || player == null)
            throw new InvalidOperationException("存档数据不完整。");
        DiskScene destination = world.scenes.Find(s => s.name == sceneName);
        if (destination == null || destination.objects == null ||
            (destination.objects.Count == 0 && (destination.transforms == null || destination.transforms.Count == 0)))
            throw new InvalidOperationException("存档缺少当前场景快照。");
        var prefab = catalog != null ? catalog.FindCharacter(player.character.characterType, player.name) : null;
        if (prefab == null) throw new InvalidOperationException("存档角色模板不存在，请重新生成 Save Game Catalog。");

        ResetAllSceneStates();
        foreach (var disk in world.scenes)
        {
            var state = new SceneState { sceneName = disk.name };
            foreach (var obj in disk.objects)
            {
                state.objects[obj.path] = obj;
                BuilderTurretBond.TrackOwner(obj.builderOwnerId);
            }
            if (disk.transforms != null)
                foreach (var value in disk.transforms)
                    state.objects[value.path] = new ObjectState { path = value.path, activeSelf = value.active,
                        localPosition = value.position, localRotation = value.rotation, localScale = value.scale };
            sceneStates[state.sceneName] = state;
        }
        quickRestartSceneName = sceneName;
        quickRestartSceneState = sceneStates[sceneName];
        quickRestartCharacterPath = player.path;
        quickRestartCharacterName = player.name;
        quickRestartCharacterState = player.character;
        quickRestartCharacterPosition = player.position;
        hasQuickRestartCharacter = true;
        quickRestartInventoryState = player.inventory;
        hasQuickRestartInventory = true;
        quickRestartGrowthState = player.growth;
        hasQuickRestartGrowth = true;

        if (string.IsNullOrEmpty(player.path))
        {
            var holder = new GameObject("Saved Character Template");
            holder.SetActive(false);
            var template = Instantiate(prefab, holder.transform);
            template.SetActive(false);
            SavedScalarFields.Apply(template.GetComponent<ZeldaCharacterData>(), player.configuration);
            template.transform.SetParent(null, false);
            DontDestroyOnLoad(template);
            quickRestartPersistentCharacterTemplate = template;
            Destroy(holder);
        }
        PersistentInventory.Instance?.PreserveForNextSceneLoad();
        quickRestartPending = true;
        RetroSceneLoadReveal.LoadSceneAsync(sceneName);
    }

    private void FinalizeRestoredDevices(Scene scene, SceneState state)
    {
        var objects = new Dictionary<string, Transform>();
        foreach (var root in scene.GetRootGameObjects()) IndexHierarchy(root.transform, objects);
        foreach (var obj in state.objects.Values)
        {
            if (IsTransformOnly(obj)) continue;
            if (!objects.TryGetValue(obj.path, out var target) || target == null) continue;
            var box = target.GetComponent<CardboardBoxPickupItem>();
            var bomb = target.GetComponent<PlacedBomb>();
            if (bomb != null)
            {
                Transform owner = null;
                if (obj.deviceOwnedByPlayer) owner = ZeldaRuntimeRegistry.GetControlledMover()?.transform;
                else if (!string.IsNullOrEmpty(obj.deviceOwnerPath)) objects.TryGetValue(obj.deviceOwnerPath, out owner);
                bomb.RestoreSavedOwner(owner != null ? owner.GetComponent<ZeldaCharacterData>() : null);
            }
            var toggle = target.GetComponent<TimedLeverObjectToggle>();
            if (toggle != null) toggle.ApplySaveState(obj.timedToggle);
            if (box != null) box.RestoreSavedContents(obj.deviceRemote);
            var puppet = target.GetComponent<ClockworkPuppetRuntime>();
            if (puppet != null)
            {
                SavedScalarFields.Apply(puppet, obj.deviceFields);
                Transform lever = null;
                if (!string.IsNullOrEmpty(obj.leverPath)) objects.TryGetValue(obj.leverPath, out lever);
                Transform ballista = null;
                if (!string.IsNullOrEmpty(obj.ballistaPath)) objects.TryGetValue(obj.ballistaPath, out ballista);
                puppet.RestoreSavedControl(lever != null ? lever.GetComponent<LeverData>() : null, obj.deviceAttached, obj.deviceRemote,
                    ballista != null ? ballista.GetComponent<FortressBallista>() : null);
            }
            if (GameSaveSystem.IsLoading)
            {
                var ai = target.GetComponent<ZeldaCharacterAiBase>();
                if (ai != null) ai.ApplySaveState(obj.savedAi);
            }
            // Authored bindings must follow the saved body (including a persistent
            // traveller), not the fresh scene copy created during Awake. All body
            // identities and control ownership are now restored, so reconnect before play resumes.
            target.GetComponent<TurretCharacterAi>()?.RefreshProtectionBinding();
            if (obj.hasTrialProofAltar) target.GetComponent<TrialProofAltar>()?.RestoreLinkedDoor();
        }
    }

    private static bool IsTransformOnly(ObjectState s)
    {
        return !s.hasTrapdoor && !s.hasKnightAmbush && !s.hasArenaChallenge && !s.hasCharacter && !s.hasMover && !s.hasAi && !s.hasDoor && !s.hasLever && !s.hasBatterySocket &&
            !s.hasEnergyCrystal && !s.hasSummoningCircle && !s.hasElectricGate && !s.hasTemporaryEffectArea && !s.hasLiquidInjector &&
            !s.hasAlchemyCauldron && !s.hasItemSubmissionStation && !s.hasTrialProofAltar && !s.hasMortarInstallation && string.IsNullOrEmpty(s.pickupId) &&
            string.IsNullOrEmpty(s.deviceKind) && string.IsNullOrEmpty(s.builderOwnerId) &&
            s.documentFields == null && s.doorDataFields == null && s.timedToggle == null;
    }

    // Called only for known builders. Remove turrets left in unloaded scenes too,
    // so returning or saving after their summoner's death cannot resurrect them.
    public void RemoveBuilderTurretsForOwner(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId)) return;
        foreach (var scene in sceneStates.Values)
        {
            var roots = new List<string>();
            foreach (var state in scene.objects.Values)
                if (state.builderOwnerId == ownerId) roots.Add(state.path);
            if (roots.Count == 0) continue;
            // Loading a save initially shares this snapshot with quick restart.
            // Death changes the live world, never the saved restart checkpoint.
            if (ReferenceEquals(scene, quickRestartSceneState))
            {
                var checkpoint = new SceneState { sceneName = scene.sceneName };
                foreach (var entry in scene.objects) checkpoint.objects.Add(entry.Key, entry.Value);
                quickRestartSceneState = checkpoint;
            }
            var removed = new List<string>();
            foreach (string path in scene.objects.Keys)
                foreach (string root in roots)
                    if (path == root || path.StartsWith(root + "/", StringComparison.Ordinal))
                    { removed.Add(path); break; }
            foreach (string path in removed) scene.objects.Remove(path);
        }
    }
}
