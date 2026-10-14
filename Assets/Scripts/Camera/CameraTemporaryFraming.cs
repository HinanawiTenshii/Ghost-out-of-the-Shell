using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-scoped, owner-held framing requests; never modifies character/save data.</summary>
public static class CameraTemporaryFraming
{
    private struct Request
    {
        public Scene scene;
        public float multiplier;
    }

    private static readonly Dictionary<Behaviour, Request> requests = new Dictionary<Behaviour, Request>();
    private static readonly List<Behaviour> expired = new List<Behaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { requests.Clear(); expired.Clear(); }

    public static void Set(Behaviour owner, float multiplier)
    {
        if (owner == null) return;
        requests[owner] = new Request { scene = owner.gameObject.scene, multiplier = Mathf.Max(1f, multiplier) };
    }

    public static void Remove(Behaviour owner)
    {
        if (ReferenceEquals(owner, null)) return;
        requests.Remove(owner);
    }

    public static float GetMultiplier(Scene scene)
    {
        float multiplier = 1f;
        expired.Clear();
        foreach (var entry in requests)
        {
            var owner = entry.Key;
            var request = entry.Value;
            if (owner == null || !owner.isActiveAndEnabled || !request.scene.IsValid() || !request.scene.isLoaded ||
                owner.gameObject.scene != request.scene)
            { expired.Add(owner); continue; }
            // Two simultaneous worms should still widen by 30%, not compound to 69%.
            if (request.scene == scene) multiplier = Mathf.Max(multiplier, request.multiplier);
        }
        foreach (var owner in expired) requests.Remove(owner);
        return multiplier;
    }
}
