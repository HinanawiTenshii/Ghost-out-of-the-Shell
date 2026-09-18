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
            disk.objects.AddRange(scene.objects.Values);
            world.scenes.Add(disk);
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
        if (destination == null || destination.objects == null || destination.objects.Count == 0)
            throw new InvalidOperationException("存档缺少当前场景快照。");
        var prefab = catalog != null ? catalog.FindCharacter(player.character.characterType, player.name) : null;
        if (prefab == null) throw new InvalidOperationException("存档角色模板不存在，请重新生成 Save Game Catalog。");

        ResetAllSceneStates();
        foreach (var disk in world.scenes)
        {
            var state = new SceneState { sceneName = disk.name };
            foreach (var obj in disk.objects) state.objects[obj.path] = obj;
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
        SceneManager.LoadScene(sceneName);
    }

    private void FinalizeRestoredDevices(Scene scene, SceneState state)
    {
        var objects = new Dictionary<string, Transform>();
        foreach (var root in scene.GetRootGameObjects()) IndexHierarchy(root.transform, objects);
        foreach (var obj in state.objects.Values)
        {
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
                puppet.RestoreSavedControl(lever != null ? lever.GetComponent<LeverData>() : null, obj.deviceAttached, obj.deviceRemote);
            }
            if (GameSaveSystem.IsLoading)
            {
                var ai = target.GetComponent<ZeldaCharacterAiBase>();
                if (ai != null) ai.ApplySaveState(obj.savedAi);
            }
        }
    }
}
