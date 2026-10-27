using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed partial class GameSaveSystem
{
    // Called only after the complete scene/player snapshot has been captured.
    // Profile metadata is committed by SaveCore after this method succeeds.
    private void CommitAutomaticSave(SaveData data)
    {
        if (!BelongsTo(data, activeProfile)) throw new InvalidDataException("自动存档不属于当前进度。");
        CommitAutomaticSaveInDirectory(ProfileFolder(activeProfile.id), data);
    }

    // An explicit directory also permits isolated tests without touching the
    // user's persistentDataPath or constructing the live save service.
    private static void CommitAutomaticSaveInDirectory(string directory, SaveData data)
    {
        if (data == null || data.version != 4 || !IsProfileId(data.profileId) || !ValidId(data.campaignId))
            throw new InvalidDataException("无效的自动存档标识。");

        var previous = new List<SaveData>(2);
        var seen = new HashSet<string>();
        // A retry of the same snapshot must not rotate it into its own history.
        seen.Add(Hash(JsonUtility.ToJson(data)));
        foreach (int slot in new[] { 0, 7, 8 })
        {
            var candidate = ReadData(Path.Combine(directory, SlotName(slot)));
            if (candidate == null || candidate.profileId != data.profileId || candidate.campaignId != data.campaignId)
                continue;
            if (!seen.Add(Hash(JsonUtility.ToJson(candidate)))) continue;
            previous.Add(candidate);
            if (previous.Count == 2) break;
        }

        // Every file is flushed and atomically replaced by WriteData. Commit
        // oldest first and current last: a failed history write cannot destroy
        // the still-readable current save. Duplicate history after an interrupted
        // rotation is collapsed on the next commit. Legacy auto.json naturally
        // becomes the first history item; no migration writes are needed at boot.
        if (previous.Count > 1) WriteData(Path.Combine(directory, SlotName(8)), previous[1]);
        if (previous.Count > 0) WriteData(Path.Combine(directory, SlotName(7)), previous[0]);
        WriteData(Path.Combine(directory, SlotName(0)), data);

        // Remove foreign/corrupt old history only AFTER the replacement current
        // snapshot exists. Read() also checks campaign identity if cleanup fails.
        for (int index = previous.Count; index < 2; index++)
        {
            try { DeleteSaveFile(Path.Combine(directory, SlotName(index == 0 ? 7 : 8))); }
            catch (Exception ex) { Debug.LogWarning("无法清理旧自动存档：" + ex.Message); }
        }
    }
}
