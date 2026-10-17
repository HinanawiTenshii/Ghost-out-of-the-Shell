using UnityEngine;

/// <summary>Only the scene's designated corrupted crystal grants evidence. Streaming is not destruction.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(EnergyCrystal))]
public sealed class GraveyardCrystalQuestTarget : MonoBehaviour
{
    private EnergyCrystal crystal;
    private void OnEnable()
    {
        crystal = GetComponent<EnergyCrystal>();
        crystal.StateChanged += OnCrystalStateChanged;
        SyncEvidence();
    }
    private void OnDisable()
    {
        if (crystal != null) crystal.StateChanged -= OnCrystalStateChanged;
    }
    private void OnCrystalStateChanged(EnergyCrystal source) => SyncEvidence();
    public void SyncEvidence()
    {
        if (!Application.isPlaying) return;
        if (crystal == null) crystal = GetComponent<EnergyCrystal>();
        if (crystal != null && crystal.IsBroken)
            QuestJournalManager.GetOrCreate().ReportGraveyardCrystalDestroyed();
    }
}
