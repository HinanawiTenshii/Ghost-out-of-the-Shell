using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Preloaded, independent one-shot so the consumed item/body cannot cut off the shot.</summary>
public static class DisposableHandgunAudio
{
    private static AudioClip shot;
    public static void Preload()
    {
        if (shot == null) shot = Resources.Load<AudioClip>("Audio/HandgunFire");
    }

    public static void Play(Scene scene, Vector3 position)
    {
        Preload();
        if (shot == null || !scene.IsValid() || !scene.isLoaded) return;
        var root = new GameObject("Hand Cannon Shot Sound");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = position;
        root.AddComponent<CameraVisionStreamingExempt>();
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = shot;
        source.volume = .65f;
        source.pitch = 1f;
        // This item is fired by the controlled character. Avoid the overhead
        // camera listener's Z offset reducing a sound emitted at the player.
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.Play();
        Object.Destroy(root, shot.length + .08f);
    }
}
