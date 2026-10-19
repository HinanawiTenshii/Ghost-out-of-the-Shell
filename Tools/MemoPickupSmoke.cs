using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MemoPickupSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static MemoPickupSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "memo-pickup-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(MemoPickupSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(MemoPickupSmoke), false))
            { SessionState.SetBool(nameof(MemoPickupSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("MEMO_PICKUP_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool v, string m) { checks++; if (!v) throw new Exception(m); }
    static FieldInfo Field(object o, string n)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, F); if (f != null) return f; }
        throw new Exception("Missing field " + n);
    }
    static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
    static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static ZeldaCharacterData Actor<T>(string name) where T : ZeldaCharacterData
    {
        var go = new GameObject(name); var data = go.AddComponent<T>(); go.AddComponent<ZeldaFourWayMover>();
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 20; state.possessionEnergy = 30;
        data.ApplyRuntimeState(state); return data;
    }
    static void Control(ZeldaCharacterData actor)
    {
        var old = ZeldaRuntimeRegistry.GetControlledMover(); if (old != null) old.enabled = false;
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.enabled = true;
        mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        ActiveZeldaCharacterDataSource.Instance?.Refresh();
    }
    static void Close(ZeldaCharacterData actor)
    {
        ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 1;
        actor.GetComponent<ZeldaFourWayMover>().EndItemAttack();
    }
    static PersistentInventory Inventory()
    {
        var go = new GameObject("Memo Inventory"); go.SetActive(false);
        var inv = go.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        go.SetActive(true); inv.enabled = false; return inv;
    }
    static void HudCheck(ZeldaHealthHeartsUI hud, int count)
    {
        Canvas.ForceUpdateCanvases();
        var slots = Get<Image[]>(hud, "inventorySlots");
        var container = Get<RectTransform>(hud, "inventoryContainer");
        Check(slots.Length == count && Get<Image[]>(hud, "inventoryItemIcons").Length == count && Get<Text[]>(hud, "inventoryQuantityTexts").Length == count, "all HUD arrays grow/shrink together");
        Check(container.anchorMin.x == .5f && container.anchorMax.x == .5f && container.pivot.x == .5f && container.anchoredPosition.x == 0, "inventory centered, odd or even count");
        float first = slots[0].rectTransform.anchoredPosition.x, last = slots[count-1].rectTransform.anchoredPosition.x;
        Check(Mathf.Abs(first + last - container.sizeDelta.x) < .01f, "symmetric slot padding");
        for (int i = 0; i < count; i++)
        {
            Check(slots[i] != null && slots[i].GetComponentInChildren<InventoryItemCooldownOverlay>(true) != null, "slot supports cooldown " + i);
            if (i > 0) Check(slots[i].rectTransform.anchoredPosition.x > slots[i-1].rectTransform.anchoredPosition.x, "slot order " + i);
        }
        var root = (RectTransform)hud.transform;
        Check(container.sizeDelta.x * container.localScale.x <= root.rect.width - 47f, "expanded inventory fits screen");
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var player = Actor<CivilianZeldaCharacterData>("Memo User"); Control(player);
        var inv = Inventory();
        var baseline = inv.CaptureRuntimeState();
        var prefab = Resources.Load<GameObject>("PickupItems/MemoPickupItem");
        Check(prefab != null, "prefab imported");
        var template = prefab.GetComponent<MemoPickupItem>();
        Check(template.ItemId == "memo" && template.ItemName == "M.E.M.O." && template.MaxStackSize == 1 && template.RequiredUseSkill == 0, "identity, stack and skill settings");
        Check(template.CanUseFromInventory && !template.CanBeStoredInCardboardBox(player), "R consumable, not a box payload");
        Check(SaveGameCatalog.LoadPickupTemplates().Count(p => p.ItemId == "memo") == 1, "catalog template unique");
        var memo = Object.Instantiate(prefab).GetComponent<MemoPickupItem>();
        var visual = memo.GetComponent<MemoPickupItemVisual>();
        Check(visual.InventoryIcon != null && visual.InventoryIcon.texture.filterMode == FilterMode.Bilinear && memo.GetComponent<Collider2D>().isTrigger, "smooth inventory icon and nonblocking pickup");
        Check(visual.InventoryIcon.texture.width == 128 && memo.GetComponent<SpriteRenderer>().sprite.texture.width == 128, "smooth sphere in world and inventory");
        var getColor = typeof(MemoPickupItemVisual).GetMethod("GetPixelColor", F);
        Check((Color)getColor.Invoke(visual, new object[] { '#' }) == Color.white && (Color)getColor.Invoke(visual, new object[] { 'r' }) == Color.white, "all sphere lines and cross pure white");
        Check(memo.transform.Find("Memory Glow").GetComponent<SpriteRenderer>().color == new Color(1,1,1,.42f), "neutral white halo");
        var shape = typeof(MemoPickupItemVisual).GetMethod("SpherePixel", BindingFlags.NonPublic | BindingFlags.Static);
        Check((char)shape.Invoke(null, new object[] { Vector2.zero, .027f }) == '#' &&
            (char)shape.Invoke(null, new object[] { new Vector2(.15f,0), .027f }) == '#' &&
            (char)shape.Invoke(null, new object[] { new Vector2(-.15f,0), .027f }) == '#', "cross centered in sphere");
        for (int i = 0; i < 36; i++)
        {
            float a = i * Mathf.PI / 18f;
            Check((char)shape.Invoke(null, new object[] { new Vector2(Mathf.Cos(a),Mathf.Sin(a)) * .55f, .027f }) == '#', "round silhouette at every angle");
        }
        Check((char)shape.Invoke(null, new object[] { new Vector2(.5f,.5f), .027f }) == '.', "no old cube corners");
        int points = Get<int>(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GrowthCollectible.prefab").GetComponent<GrowthCollectible>(), "orbitPointCount");
        Check(points == 24 && memo.transform.Find("Memory Orbit").childCount == points, "world orbit matches shipped DATA dot count");
        Check(CountOrbitPoints(visual.InventoryIcon) == points, "inventory orbit matches DATA and world count");
        Check(CountOrbitPoints(MemoPickupItemVisual.SphereIcon) == 0, "UI body contains no duplicate orbit");
        Check(!memo.ExecuteUse(player) && inv.SlotCount == 5, "direct use cannot grant free capacity");
        Check(memo.TryPickUp() && inv.SlotCount == 5 && inv.GetSlot(0).ItemId == "memo", "E collects without expanding");
        Close(player); yield return null; yield return null;
        Check(inv.TryDropSelectedItem() && inv.SlotCount == 5 && inv.GetSlot(0).IsEmpty, "Q drops without consuming or expanding");
        var dropped = Object.FindObjectOfType<MemoPickupItem>();
        Check(dropped != null, "dropped physical item exists");
        dropped.transform.position = player.transform.position; Set(dropped, "pickupLockedUntil", 0f);
        Check(dropped.TryPickUp(), "dropped item can be picked up again");
        Close(player); yield return null; yield return null;
        var source = ActiveZeldaCharacterDataSource.Instance;
        if (!source) source = new GameObject("HUD Data").AddComponent<ActiveZeldaCharacterDataSource>(); source.Refresh();
        var hud = new GameObject("Memo HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<ZeldaHealthHeartsUI>();
        yield return null;
        HudCheck(hud, 5);
        for (int i = 1; i < 5; i++) inv.SetSlot(i, GoldCoinPickupItem.DefaultItemId, 20);
        var oldSlots = inv.Slots.ToArray(); int events = 0;
        Action onChanged = () =>
        {
            events++;
            Check(inv.SlotCount == 6 && inv.GetSlot(0).IsEmpty && inv.CaptureRuntimeState().slots.Length == 6, "atomic state observable by save callbacks");
            Check(!inv.TryUseSelectedItem() && !inv.TryDropSelectedItem(), "reentrant use/drop blocked");
        };
        inv.InventoryChanged += onChanged;
        Close(player); Check(inv.TryUseSelectedItem(), "R succeeds with completely full inventory");
        inv.InventoryChanged -= onChanged;
        Check(events == 1 && inv.SlotCount == 6 && inv.SelectedSlotIndex == 0, "one consumption, one slot, selection stable");
        for (int i = 0; i < 5; i++) Check(ReferenceEquals(oldSlots[i], inv.GetSlot(i)) && (i == 0 || inv.GetSlot(i).Quantity == 20), "old slot identity/data preserved " + i);
        HudCheck(hud, 6);
        inv.SetSlot(5, "memo", 1); inv.SelectSlot(5); Close(player);
        Time.timeScale = 0; Check(!inv.TryUseSelectedItem() && inv.SlotCount == 6 && !inv.GetSlot(5).IsEmpty, "paused use rejected"); Time.timeScale = 1;
        var ghost = Actor<GhostZeldaCharacterData>("Ghost"); Control(ghost);
        Check(!inv.TryUseSelectedItem() && inv.SlotCount == 6, "ghost use rejected");
        Control(player); Close(player);
        Check(inv.TryUseSelectedItem() && inv.SlotCount == 7 && inv.GetSlot(5).IsEmpty, "new slot usable and repeated upgrades stack");
        HudCheck(hud, 7);
        inv.SetSlot(6, GoldCoinPickupItem.DefaultItemId, 17); inv.SelectSlot(6);
        var state = inv.CaptureRuntimeState(); string json = JsonUtility.ToJson(state);
        var other = Actor<CivilianZeldaCharacterData>("Another Body"); Control(other);
        Check(JsonUtility.ToJson(inv.CaptureRuntimeState()) == json, "possession preserves consciousness inventory");
        Check(SceneTravelStateManager.GetOrCreate().CaptureDiskPlayer().inventory.slots.Length == 7, "disk player snapshot captures full capacity");
        inv.ClearAll(); Check(inv.SlotCount == 7 && inv.Slots.All(s => s.IsEmpty), "clearing contents does not revoke permanent expansion");
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        Check(inv.SlotCount == 7 && inv.SelectedSlotIndex == 6 && inv.GetSlot(6).Quantity == 17, "JSON roundtrip includes last slot and selection");
        inv.PreserveForNextSceneLoad();
        Call(inv, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Additive);
        Call(inv, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        Check(JsonUtility.ToJson(inv.CaptureRuntimeState()) == json, "travel retains capacity and items through additive + single events");
        var duplicate = new GameObject("Duplicate Inventory").AddComponent<PersistentInventory>();
        yield return null;
        Check(duplicate == null && PersistentInventory.Instance == inv && inv.SlotCount == 7, "scene duplicate cannot reset surviving inventory");
        inv.ApplyRuntimeState(baseline); HudCheck(hud, 5);
        Check(inv.SlotCount == 5 && inv.Slots.All(s => s.IsEmpty) && inv.SelectedSlotIndex == 0, "old five-slot save shrinks safely");
        inv.ApplyRuntimeState(state); HudCheck(hud, 7);
        inv.PreserveCapacityForNextSceneLoad();
        Call(inv, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        Check(inv.SlotCount == 7 && inv.Slots.All(s => s.IsEmpty), "next level clears carried items but preserves permanent capacity");
        inv.ApplyRuntimeState(state);
        Call(inv, "OnSceneLoaded", SceneManager.GetActiveScene(), LoadSceneMode.Single);
        Check(inv.SlotCount == 5 && inv.Slots.All(s => s.IsEmpty), "fresh scene session resets capacity"); HudCheck(hud, 5);
        inv.ApplyRuntimeState(state);
        const string key = "MemoPickupSmoke.OnlyTestCache";
        Set(inv, "saveKey", key); Set(inv, "saveToPlayerPrefs", true); inv.Save(); Set(inv, "saveToPlayerPrefs", false);
        inv.ApplyRuntimeState(baseline); Set(inv, "saveToPlayerPrefs", true); inv.Load(); Set(inv, "saveToPlayerPrefs", false);
        Check(inv.SlotCount == 7 && inv.GetSlot(6).Quantity == 17, "legacy prefs cache preserves expansion"); PlayerPrefs.DeleteKey(key);
        Object.DestroyImmediate(inv.gameObject); inv = Inventory(); Check(inv.SlotCount == 5, "fresh singleton baseline five");
        inv.ApplyRuntimeState(JsonUtility.FromJson<PersistentInventory.RuntimeState>(json));
        Call(hud, "TryConnectInventory"); Call(hud, "RefreshInventoryDisplay"); HudCheck(hud, 7);
        inv.ApplyRuntimeState(default); Check(inv.SlotCount == 5 && inv.Slots.All(s => s.IsEmpty), "empty old state restores baseline");
        // Resize beyond a normal screen and then restore; no implicit gameplay capacity cap.
        var many = new PersistentInventory.RuntimeState { slots = new PersistentInventory.RuntimeSlotState[40] };
        inv.ApplyRuntimeState(many); HudCheck(hud, 40);
        inv.ApplyRuntimeState(state); HudCheck(hud, 7);
        Close(other); yield return null;
        PreviewWorld(prefab, visual != null ? visual.InventoryIcon : template.ItemVisual.InventoryIcon);
        PreviewHud(hud);
    }
    static void SaveRender(Camera camera, string file, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24); var old = RenderTexture.active;
        camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
        var png = new Texture2D(width, height, TextureFormat.RGBA32, false); png.ReadPixels(new Rect(0, 0, width, height), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + file), png.EncodeToPNG());
        Object.Destroy(png); camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
    }
    static int CountOrbitPoints(Sprite sprite)
    {
        var old = RenderTexture.active;var rt = RenderTexture.GetTemporary(128,128,0);
        Graphics.Blit(sprite.texture,rt);RenderTexture.active=rt;
        var image=new Texture2D(128,128,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
        var pixels=image.GetPixels32();var seen=new bool[pixels.Length];int count=0;
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            int start=y*128+x;var p=new Vector2((x+.5f)/64f-1,(y+.5f)/64f-1);
            if(seen[start]||pixels[start].a<128||p.sqrMagnitude<.72f*.72f)continue;
            count++;var pending=new System.Collections.Generic.Stack<int>();pending.Push(start);seen[start]=true;
            while(pending.Count>0)
            {
                int index=pending.Pop(),px=index%128,py=index/128;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=px+dx,ny=py+dy;if(nx<0||ny<0||nx>=128||ny>=128)continue;
                    int next=ny*128+nx;if(seen[next]||pixels[next].a<128)continue;
                    seen[next]=true;pending.Push(next);
                }
            }
        }
        Object.Destroy(image);RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);return count;
    }
    static void PreviewWorld(GameObject prefab, Sprite icon)
    {
        var cam = new GameObject("Preview Camera").AddComponent<Camera>(); cam.transform.position = new Vector3(100, 100, -10);
        cam.orthographic = true; cam.orthographicSize = .8f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f,.06f,.10f);
        var data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GrowthCollectible.prefab"), new Vector3(99.1f,100,0), Quaternion.identity);
        var memo = Object.Instantiate(prefab, new Vector3(100.7f,100,0), Quaternion.identity);
        SaveRender(cam, "Memo-world-preview.png", 900, 400);
        Object.Destroy(data); Object.Destroy(memo); Object.Destroy(cam.gameObject);
    }
    static void PreviewHud(ZeldaHealthHeartsUI hud)
    {
        var cam = new GameObject("HUD Preview").AddComponent<Camera>(); cam.transform.position = new Vector3(200,200,-10);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.035f,.06f,.10f);
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); SaveRender(cam, "Memo-hud-preview.png", 1280, 720);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; Object.Destroy(cam.gameObject);
    }
}
