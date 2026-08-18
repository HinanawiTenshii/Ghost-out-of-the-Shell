using UnityEngine;

/// <summary>
/// Scene-local 2D background music player with configurable looping playback.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoopingBackgroundMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnSceneStart = true;

    private AudioSource audioSource;

    public AudioClip MusicClip => musicClip;
    public AudioSource AudioSource => audioSource;

    private void Awake()
    {
        EnsureAudioSource();
        ApplySettings();
        if (playOnSceneStart && musicClip != null)
        {
            audioSource.Play();
        }
    }

    public void Play()
    {
        EnsureAudioSource();
        ApplySettings();
        if (musicClip != null)
        {
            audioSource.Play();
        }
    }

    public void Stop()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void ApplySettings()
    {
        audioSource.clip = musicClip;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureAudioSource();
        ApplySettings();
    }
}
