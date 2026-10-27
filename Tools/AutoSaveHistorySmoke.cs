using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Can be called from an isolated editor harness, or compiled with the existing
// offline smoke stubs. Never instantiate GameSaveSystem, start play mode, change
// PlayerSettings, or consult Application.persistentDataPath for these checks.
public static class AutoSaveHistorySmoke
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static int checks;

    public static void RunChecks()
    {
        checks = 0;
        string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".codex-temp"));
        string directory = Path.Combine(root, "auto-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Check(Enumerable.Range(0, 9).Where(GameSaveSystem.IsManualSlot).SequenceEqual(new[] { 2, 3, 4, 5, 6 }), "only five original manual slots");
            Check(Enumerable.Range(0, 9).Where(GameSaveSystem.IsAutomaticSlot).SequenceEqual(new[] { 0, 7, 8 }), "three explicit automatic slots");
            Check(!GameSaveSystem.IsManualSlot(-1) && !GameSaveSystem.IsManualSlot(9) && !GameSaveSystem.IsAutomaticSlot(9), "invalid slots rejected");
            Check(Name(7) == "auto-previous-1.json" && Name(8) == "auto-previous-2.json", "history cannot alias manual files");
            string campaign = Guid.NewGuid().ToString("N");
            var untouched = new Dictionary<string, byte[]>();
            for (int slot = 1; slot <= 6; slot++)
            {
                string path = Path.Combine(directory, Name(slot));
                Invoke("WriteData", path, Snapshot(campaign, 100 + slot));
                untouched.Add(path, File.ReadAllBytes(path));
            }
            // Old installs have only auto.json; the first new save migrates its
            // original complete payload without altering its timestamp/name.
            var legacy = Snapshot(campaign, 0);
            Invoke("WriteData", Path.Combine(directory, Name(0)), legacy);
            Commit(directory, Snapshot(campaign, 1));
            Expect(directory, 1, 0, null);
            var previous = Read(directory, 7);
            Check(previous.savedAt == legacy.savedAt && previous.savedUtcTicks == legacy.savedUtcTicks && previous.world == legacy.world,
                "legacy snapshot is retained verbatim, including original save time");
            Commit(directory, Snapshot(campaign, 2));
            Expect(directory, 2, 1, 0);
            var third = Snapshot(campaign, 3);
            string before = JsonUtility.ToJson(third);
            Commit(directory, third);
            Expect(directory, 3, 2, 1);
            Check(JsonUtility.ToJson(third) == before, "commit does not mutate captured input");
            Commit(directory, third);
            Expect(directory, 3, 2, 1);
            Check(Directory.GetFiles(directory, "auto*.json").Length == 3, "exactly three automatic snapshots after repeated rotation");

            // Deterministic pre-current I/O failure on every platform. An output
            // temporary path occupied by a directory cannot be written as a file.
            string blockedTemporary = Path.Combine(directory, Name(8)) + ".tmp";
            Directory.CreateDirectory(blockedTemporary);
            bool failed = false;
            try { Commit(directory, Snapshot(campaign, 4)); }
            catch (TargetInvocationException) { failed = true; }
            finally { Directory.Delete(blockedTemporary); }
            Check(failed, "history write failure is reported");
            Expect(directory, 3, 2, 1);
            Commit(directory, Snapshot(campaign, 4));
            Expect(directory, 4, 3, 2);

            // Emulate a crash after history has advanced but before current did.
            Invoke("WriteData", Path.Combine(directory, Name(8)), Read(directory, 7));
            Invoke("WriteData", Path.Combine(directory, Name(7)), Read(directory, 0));
            Commit(directory, Snapshot(campaign, 5));
            Expect(directory, 5, 4, 3);

            File.WriteAllText(Path.Combine(directory, Name(7)), "corrupt fixture");
            Commit(directory, Snapshot(campaign, 6));
            Expect(directory, 6, 5, 3);
            Check(Read(directory, 8).campaignId == campaign, "corruption skipped while valid older history survives");

            string replacement = Guid.NewGuid().ToString("N");
            Commit(directory, Snapshot(replacement, 10));
            Expect(directory, 10, null, null);
            Check(!File.Exists(Path.Combine(directory, Name(7))) && !File.Exists(Path.Combine(directory, Name(8))),
                "overwriting a progress slot clears foreign campaign history");
            Commit(directory, Snapshot(replacement, 11));
            Expect(directory, 11, 10, null);
            Check(Read(directory, 7).campaignId == replacement, "new campaign rotates only its own snapshots");
            foreach (var pair in untouched)
                Check(pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)), "automatic rotation never modifies " + Path.GetFileName(pair.Key));
            Check(!File.Exists(Path.Combine(directory, "manual-6.json")) && !File.Exists(Path.Combine(directory, "manual-7.json")),
                "history never creates extra manual files");
            Debug.Log("AutoSaveHistory ALL PASS " + checks);
        }
        finally
        {
            // Explicit files only, with a validated fresh fixture directory.
            if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid smoke fixture path");
            foreach (int slot in Enumerable.Range(0, 9))
            {
                string path = Path.Combine(directory, Name(slot));
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
        }
    }

    private static object Invoke(string name, params object[] args) => typeof(GameSaveSystem).GetMethod(name, PrivateStatic).Invoke(null, args);
    private static string Name(int slot) => (string)Invoke("SlotName", slot);
    private static GameSaveSystem.SaveData Read(string directory, int slot) =>
        (GameSaveSystem.SaveData)Invoke("ReadData", Path.Combine(directory, Name(slot)));
    private static void Commit(string directory, GameSaveSystem.SaveData data) => Invoke("CommitAutomaticSaveInDirectory", directory, data);
    private static GameSaveSystem.SaveData Snapshot(string campaign, int moment) => new GameSaveSystem.SaveData
    {
        profileId = "slot-1", campaignId = campaign, savedUtcTicks = 1000 + moment,
        savedAt = "original-time-" + moment, name = "snapshot-" + moment, scene = "SyntheticFixture",
        levelName = "Synthetic Fixture", world = "{\"moment\":" + moment + "}", collectibleCount = moment,
        player = new SceneTravelStateManager.DiskPlayer()
    };
    private static void Expect(string directory, int current, int? previous, int? older)
    {
        foreach (var item in new[] { new { slot = 0, moment = (int?)current }, new { slot = 7, moment = previous }, new { slot = 8, moment = older } })
        {
            var data = Read(directory, item.slot);
            Check(item.moment.HasValue ? data != null && data.collectibleCount == item.moment.Value : data == null,
                "slot " + item.slot + " holds " + (item.moment.HasValue ? item.moment.Value.ToString() : "nothing"));
        }
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("AutoSaveHistory: " + message);
    }
}
