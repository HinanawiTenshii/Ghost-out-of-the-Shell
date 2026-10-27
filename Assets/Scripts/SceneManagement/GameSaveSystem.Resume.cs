using System;
using System.IO;
using UnityEngine;

public sealed partial class GameSaveSystem
{
    private int pendingResumeSlot = -1;
    private string pendingResumeCheckpointId;

    private void BeginRestoreResumeReference(int sourceSlot, string sourceCheckpointId)
    {
        pendingResumeSlot = sourceSlot;
        pendingResumeCheckpointId = sourceCheckpointId;
    }

    // Called only after successful world restoration, never for an arena's
    // internal layout reload. This updates metadata, not auto/quick/manual data.
    private void CommitRestoredResumeReference(SaveData data)
    {
        int slot = pendingResumeSlot;
        string checkpoint = pendingResumeCheckpointId;
        pendingResumeSlot = -1;
        pendingResumeCheckpointId = null;
        if (!SetResumeReference(activeProfile, data, slot, checkpoint, DateTime.UtcNow.Ticks)) return;
        try { WriteData(Path.Combine(ProfileFolder(activeProfile.id), "profile.json"), activeProfile); }
        catch (Exception ex)
        {
            // The world load itself succeeded. A metadata error must not disable
            // future saving or turn this into a failed restore.
            Debug.LogWarning("无法记录最近读取位置：" + ex.Message);
        }
    }

    private static bool SetResumeReference(ProfileData profile, SaveData data, int slot, string checkpointId, long now)
    {
        if (!BelongsTo(data, profile) || data.savedUtcTicks <= 0 || now <= 0 ||
            (string.IsNullOrEmpty(checkpointId) ? Array.IndexOf(KnownSaveSlots, slot) < 0 : !ValidId(checkpointId))) return false;
        profile.resumeUtcTicks = now;
        profile.resumeSavedUtcTicks = data.savedUtcTicks;
        profile.resumeSlot = string.IsNullOrEmpty(checkpointId) ? slot : -1;
        profile.resumeCheckpointId = checkpointId;
        return true;
    }

    private static void ClearResumeReference(ProfileData profile)
    {
        if (profile == null) return;
        profile.resumeUtcTicks = profile.resumeSavedUtcTicks = 0;
        profile.resumeSlot = -1;
        profile.resumeCheckpointId = null;
    }

    private static SaveData ResolveContinueTarget(ProfileData profile, out int slot, out string checkpointId, out long orderTicks)
    {
        var snapshots = new SaveData[SlotCount];
        foreach (int candidate in KnownSaveSlots)
            snapshots[candidate] = ReadData(ProfileSlotPath(profile.id, candidate));
        var checkpoint = profile.resumeUtcTicks > 0 && ValidId(profile.resumeCheckpointId)
            ? ReadData(CheckpointPath(profile.resumeCheckpointId)) : null;
        return SelectContinueTarget(profile, snapshots, checkpoint, out slot, out checkpointId, out orderTicks);
    }

    // Pure selection keeps disk I/O at the title/explicit-continue boundary and
    // allows offline tests without accessing the user's persistentDataPath.
    private static SaveData SelectContinueTarget(ProfileData profile, SaveData[] snapshots, SaveData checkpoint,
        out int slot, out string checkpointId, out long orderTicks)
    {
        slot = -1; checkpointId = null; orderTicks = 0;
        SaveData latest = null;
        if (profile == null || snapshots == null) return null;
        foreach (int candidate in KnownSaveSlots)
        {
            if (candidate >= snapshots.Length) continue;
            var data = snapshots[candidate];
            if (!BelongsTo(data, profile) || (latest != null && data.savedUtcTicks <= latest.savedUtcTicks)) continue;
            latest = data; slot = candidate; orderTicks = data.savedUtcTicks;
        }
        // A new snapshot may have committed before a profile-index write failed.
        // Such real progress supersedes an older successful-load selection.
        if (profile.resumeUtcTicks <= 0 || (latest != null && latest.savedUtcTicks > profile.resumeUtcTicks)) return latest;
        if (!string.IsNullOrEmpty(profile.resumeCheckpointId))
        {
            if (ValidId(profile.resumeCheckpointId) && MatchesResumeSnapshot(profile, checkpoint) &&
                checkpoint.visitedLevels != null && checkpoint.visitedLevels.Exists(v => v != null && v.checkpointId == profile.resumeCheckpointId))
            {
                slot = -1; checkpointId = profile.resumeCheckpointId; orderTicks = profile.resumeUtcTicks;
                return checkpoint;
            }
            return latest;
        }
        int preferred = profile.resumeSlot;
        if (Array.IndexOf(KnownSaveSlots, preferred) >= 0 && preferred < snapshots.Length &&
            MatchesResumeSnapshot(profile, snapshots[preferred]))
        {
            slot = preferred; orderTicks = profile.resumeUtcTicks;
            return snapshots[preferred];
        }
        // An interrupted auto rotation can move a referenced snapshot from 7 to
        // 8 before current commits. Match identity, never silently switch branch.
        foreach (int candidate in KnownSaveSlots)
            if (candidate < snapshots.Length && MatchesResumeSnapshot(profile, snapshots[candidate]))
            {
                slot = candidate; orderTicks = profile.resumeUtcTicks;
                return snapshots[candidate];
            }
        return latest;
    }

    private static bool MatchesResumeSnapshot(ProfileData profile, SaveData data)
        => BelongsTo(data, profile) && profile.resumeSavedUtcTicks > 0 && data.savedUtcTicks == profile.resumeSavedUtcTicks;
}
