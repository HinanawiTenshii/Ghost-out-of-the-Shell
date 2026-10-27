using System;
using System.Collections.Generic;
using System.Reflection;

// Pure production-selector checks. No Unity startup, files or live save paths.
public static class AutoSaveResumeSmoke
{
    private static int checks;
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;

    public static int RunChecks()
    {
        checks = 0;
        var profile = new GameSaveSystem.ProfileData { id = "slot-1", campaignId = Guid.NewGuid().ToString("N") };
        var slots = new GameSaveSystem.SaveData[9];
        var oldManual = slots[2] = Snapshot(profile, 100);
        var newerAuto = slots[0] = Snapshot(profile, 200);
        Expect(profile, slots, null, newerAuto, 0, null, 200, "legacy profile selects latest save");
        Check(Mark(profile, oldManual, 2, null, 300), "successful older manual load sets resume reference");
        Expect(profile, slots, null, oldManual, 2, null, 300, "continue preserves older loaded branch");
        Check(Mark(profile, oldManual, 2, null, 301), "repeated load only updates metadata");
        Check(oldManual.savedUtcTicks == 100 && newerAuto.savedUtcTicks == 200, "load never changes snapshot timestamps");
        Check(ReferenceEquals(slots[0], newerAuto) && slots[7] == null && slots[8] == null, "loading does not rotate automatic history");

        slots[8] = oldManual; slots[2] = newerAuto;
        Expect(profile, slots, null, oldManual, 8, null, 301, "interrupted rotation follows moved snapshot identity");
        slots[8] = null;
        Expect(profile, slots, null, newerAuto, 0, null, 200, "missing resume snapshot falls back without adopting another snapshot in that slot");
        slots[2] = Snapshot(new GameSaveSystem.ProfileData { id = "slot-1", campaignId = Guid.NewGuid().ToString("N") }, 100);
        Expect(profile, slots, null, newerAuto, 0, null, 200, "foreign campaign cannot satisfy resume timestamp");

        slots[2] = oldManual;
        var newBranch = slots[0] = Snapshot(profile, 400);
        Expect(profile, slots, null, newBranch, 0, null, 400, "new snapshot beats stale resume after profile write failure");
        slots[0] = newerAuto;
        string checkpointId = Guid.NewGuid().ToString("N");
        var entrance = Snapshot(profile, 50);
        entrance.visitedLevels = new List<GameSaveSystem.LevelVisit> { new GameSaveSystem.LevelVisit { checkpointId = checkpointId } };
        Check(Mark(profile, entrance, -1, checkpointId, 500), "restart entry creates checkpoint resume reference");
        Expect(profile, slots, entrance, entrance, -1, checkpointId, 500, "continue after restart returns to entry without auto rotation");
        Expect(profile, slots, null, newerAuto, 0, null, 200, "missing entry safely falls back");
        entrance.visitedLevels[0].checkpointId = Guid.NewGuid().ToString("N");
        Expect(profile, slots, entrance, newerAuto, 0, null, 200, "wrong checkpoint identity rejected");

        Check(!Mark(profile, oldManual, 9, null, 600), "unknown source slot rejected");
        Check(!Mark(profile, oldManual, -1, "../escape", 600), "invalid checkpoint identity rejected");
        Check(!Mark(profile, Snapshot(new GameSaveSystem.ProfileData { id = "slot-2", campaignId = profile.campaignId }, 100), 2, null, 600), "other profile cannot set reference");
        Call("ClearResumeReference", profile);
        Check(profile.resumeUtcTicks == 0 && profile.resumeSavedUtcTicks == 0 && profile.resumeCheckpointId == null, "actual save clears load selection");
        Expect(profile, slots, entrance, newerAuto, 0, null, 200, "cleared reference returns to normal latest-save behavior");
        Check(Mark(profile, oldManual, 2, null, 700), "reload chooses latest played profile");
        var other = new GameSaveSystem.ProfileData { id = "slot-2", campaignId = Guid.NewGuid().ToString("N") };
        var otherSlots = new GameSaveSystem.SaveData[9]; otherSlots[0] = Snapshot(other, 650);
        Select(profile, slots, null, out _, out _, out long activeOrder);
        Select(other, otherSlots, null, out _, out _, out long otherOrder);
        Check(activeOrder > otherOrder, "cross-profile continue orders by last successful load, not old snapshot time");
        Expect(profile, new GameSaveSystem.SaveData[9], null, null, -1, null, 0, "all missing snapshots cannot continue");
        return checks;
    }

    private static object Call(string name, params object[] args) => typeof(GameSaveSystem).GetMethod(name, Flags).Invoke(null, args);
    private static bool Mark(GameSaveSystem.ProfileData profile, GameSaveSystem.SaveData data, int slot, string checkpoint, long ticks)
        => (bool)Call("SetResumeReference", profile, data, slot, checkpoint, ticks);
    private static GameSaveSystem.SaveData Select(GameSaveSystem.ProfileData profile, GameSaveSystem.SaveData[] slots,
        GameSaveSystem.SaveData checkpoint, out int selected, out string checkpointId, out long order)
    {
        object[] args = { profile, slots, checkpoint, -1, null, 0L };
        var result = (GameSaveSystem.SaveData)Call("SelectContinueTarget", args);
        selected = (int)args[3]; checkpointId = (string)args[4]; order = (long)args[5];
        return result;
    }
    private static void Expect(GameSaveSystem.ProfileData profile, GameSaveSystem.SaveData[] slots, GameSaveSystem.SaveData checkpoint,
        GameSaveSystem.SaveData expected, int expectedSlot, string expectedCheckpoint, long expectedOrder, string label)
    {
        var result = Select(profile, slots, checkpoint, out int slot, out string id, out long order);
        Check(ReferenceEquals(result, expected) && slot == expectedSlot && id == expectedCheckpoint && order == expectedOrder, label);
    }
    private static GameSaveSystem.SaveData Snapshot(GameSaveSystem.ProfileData profile, long ticks) => new GameSaveSystem.SaveData
    { version = 4, profileId = profile.id, campaignId = profile.campaignId, savedUtcTicks = ticks, world = "unchanged-world" };
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception("AUTOSAVE_RESUME_FAIL: " + label); checks++; }
}
