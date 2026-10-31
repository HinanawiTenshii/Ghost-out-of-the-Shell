using UnityEngine;

public sealed partial class TitleScreenController
{
    [SerializeField, Range(0f, 1f)] private float terminalSoundVolume = .28f;
    private AudioSource terminalAudio;
    private AudioClip terminalBootSound, terminalMoveSound, terminalConfirmSound;
    private float nextNavigationSound;
    private bool suppressSelectionSound;

    private void BuildTerminalAudio()
    {
        var sound = new GameObject("Terminal UI Audio");
        sound.transform.SetParent(transform, false);
        terminalAudio = sound.AddComponent<AudioSource>();
        terminalAudio.playOnAwake = false;
        terminalAudio.loop = false;
        terminalAudio.spatialBlend = 0f;
        terminalAudio.ignoreListenerPause = true;
        terminalAudio.volume = terminalSoundVolume;
        terminalBootSound = Resources.Load<AudioClip>("Audio/TitleTerminalBoot");
        terminalMoveSound = Resources.Load<AudioClip>("Audio/TitleTerminalMove");
        terminalConfirmSound = Resources.Load<AudioClip>("Audio/TitleTerminalConfirm");
    }

    public void PlayTerminalNavigation()
    {
        if (!terminalReady || suppressSelectionSound || RetroSceneLoadReveal.IsBlockingInput || Time.unscaledTime < nextNavigationSound) return;
        nextNavigationSound = Time.unscaledTime + .09f;
        PlayTerminalCue(terminalMoveSound);
    }

    private void PlayTerminalConfirm()
    {
        nextNavigationSound = Time.unscaledTime + .20f;
        PlayTerminalCue(terminalConfirmSound);
    }

    private void PlayTerminalCue(AudioClip clip)
    {
        if (clip == null || terminalAudio == null) return;
        // One UI voice: fast mouse sweeps never stack a wall of beeps.
        terminalAudio.Stop();
        terminalAudio.clip = clip;
        terminalAudio.Play();
    }
}
