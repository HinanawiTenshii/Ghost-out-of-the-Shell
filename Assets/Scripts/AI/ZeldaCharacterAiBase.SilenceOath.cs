using UnityEngine;

public partial class ZeldaCharacterAiBase
{
    private SilenceOathRuntime.State silenceOath;
    private SpriteRenderer silenceOathVisual;

    public bool HasSilenceOath => silenceOath.rank > 0;
    // Keep the OLD incident private after expiration. A new witnessed crime can
    // start a normal incident, but merely ticking Hostile must never replay it.
    public bool IsSilenceOathIncidentPrivate => HasSilenceOath || silenceOath.privateIncident;
    public float SilenceOathRemaining => HasSilenceOath
        ? Mathf.Max(0f, SilenceOathDuration - silenceOath.elapsed) : 0f;
    public float SilenceOathDuration => silenceOath.rank >= 2 && silenceOath.alertCycle
        ? SilenceOathRuntime.MaximumDuration : SilenceOathRuntime.BaseDuration;
    public int SilenceOathRank => silenceOath.rank;

    public bool CanReceiveSilenceOath => isActiveAndEnabled && characterData != null &&
        !characterData.IsDead && !characterData.IsGhostLike && mover != null && !mover.enabled &&
        !possessionLocked && !IsStunned && (HasSilenceOath ||
            currentState != ZeldaAiState.Alert && currentState != ZeldaAiState.Hostile &&
            currentState != ZeldaAiState.Search);

    internal void ApplySilenceOath(int rank)
    {
        silenceOath = new SilenceOathRuntime.State {
            rank = Mathf.Clamp(rank, 1, 3),
            alertCycle = IsSilenceAlertCycle(currentState),
            privateIncident = IsSilenceAlertCycle(currentState)
        };
        // Never reset perception, target, movement, or the local countdown.
        if (sharedPlayerTimer != null) sharedPlayerTimer.Leave(this);
        SilenceOathRuntime.Register(this);
        RefreshSilenceOathVisual();
    }

    private static bool IsSilenceAlertCycle(ZeldaAiState state) =>
        state == ZeldaAiState.Alert || state == ZeldaAiState.Hostile || state == ZeldaAiState.Search;

    private void OnSilenceOathStateChanged(ZeldaAiState state)
    {
        if (HasSilenceOath && IsSilenceAlertCycle(state))
        {
            silenceOath.alertCycle = true;
            silenceOath.privateIncident = true;
        }
        else if (state == ZeldaAiState.Recovery || state == ZeldaAiState.Idle)
        {
            silenceOath.privateIncident = false;
            silenceOath.alertCycle = false;
            if (silenceOath.elapsed >= SilenceOathRuntime.BaseDuration) EndSilenceOath();
        }
    }

    private void PrepareFreshSilenceOathIncident()
    {
        if (!HasSilenceOath && silenceOath.privateIncident)
        {
            silenceOath.privateIncident = false;
            // This is a NEW crime/reacquisition, not expiration or a load callback.
            lastSharedHostilityTarget = null;
        }
    }

    private void ReportFreshSilenceOathEvidence()
    {
        if (HasSilenceOath || !silenceOath.privateIncident) return;
        PrepareFreshSilenceOathIncident();
        SyncSharedPlayerIncident();
        ShareNearbyPlayerHostility();
    }

    private void AdvanceSilenceOath(float delta)
    {
        if (!HasSilenceOath || GameSaveSystem.IsLoading) return;
        if (characterData == null || characterData.IsDead || mover == null || mover.enabled)
        { ClearSilenceOath(); return; }
        silenceOath.elapsed += Mathf.Max(0f, delta);
        if (silenceOath.elapsed >= SilenceOathDuration) EndSilenceOath();
        RefreshSilenceOathVisual();
    }

    private void EndSilenceOath()
    {
        silenceOath.rank = 0;
        SilenceOathRuntime.Unregister(this);
        if (silenceOathVisual != null) silenceOathVisual.enabled = false;
        // No ChangeState / Join / broadcast here: suppress queued alarms permanently.
    }

    private void ClearSilenceOath()
    {
        EndSilenceOath();
        silenceOath = default;
    }

    private void RestoreSilenceOath(SilenceOathRuntime.State state)
    {
        ClearSilenceOath();
        silenceOath = state;
        silenceOath.rank = Mathf.Clamp(state.rank, 0, 3);
        silenceOath.elapsed = Mathf.Clamp(state.elapsed, 0f, SilenceOathRuntime.MaximumDuration);
        if (characterData == null || characterData.IsDead || mover == null || mover.enabled)
        { ClearSilenceOath(); return; }
        if (HasSilenceOath && silenceOath.elapsed < SilenceOathDuration) SilenceOathRuntime.Register(this);
        else EndSilenceOath();
        RefreshSilenceOathVisual();
    }

    public void RestoreSilenceOathAfterSceneReturn(SaveState saved)
    {
        var effect = saved != null ? saved.silenceOath : default;
        // Scene travel normally ends pursuit, unlike an exact disk-save reload.
        // Keep any unused base duration without resurrecting the previous alert cycle.
        effect.alertCycle = false;
        effect.privateIncident = false;
        RestoreSilenceOath(effect);
    }

    private void RefreshSilenceOathVisual()
    {
        if (!HasSilenceOath) return;
        if (silenceOathVisual == null)
        {
            var go = new GameObject("Silence Oath Mark");
            go.layer = 2;
            go.transform.SetParent(transform, false);
            silenceOathVisual = go.AddComponent<SpriteRenderer>();
            silenceOathVisual.sprite = SilenceOathRuntime.GetIcon(0);
        }
        silenceOathVisual.enabled = isActiveAndEnabled;
        silenceOathVisual.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, 1.35f));
        silenceOathVisual.transform.rotation = Quaternion.identity;
        var scale = transform.lossyScale;
        silenceOathVisual.transform.localScale = new Vector3(.42f / Mathf.Max(.01f, Mathf.Abs(scale.x)),
            .42f / Mathf.Max(.01f, Mathf.Abs(scale.y)), 1f);
        silenceOathVisual.color = Color.Lerp(ZeldaUiPalette.Primary, Color.white,
            .15f + .1f * Mathf.Sin(Time.time * 3f));
        if (characterRenderer != null)
        {
            silenceOathVisual.sortingLayerID = characterRenderer.sortingLayerID;
            silenceOathVisual.sortingOrder = characterRenderer.sortingOrder + 8;
        }
    }
}
