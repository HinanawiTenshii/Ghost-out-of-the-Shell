using System.Collections.Generic;
using UnityEngine;

public sealed partial class KnightCaptainAmbush
{
    // The authored patrol already describes navigable streets. Densify long
    // segments once per incident, so local A* never builds a whole-city grid.
    internal static List<Vector2> BuildRoadRoute(Vector2 start, Vector2 finish, Vector2[] road)
    {
        var result = new List<Vector2>();
        if (road == null || road.Length < 2 || Vector2.Distance(start, finish) < 10)
        { result.Add(finish); return result; }
        var nodes = new List<Vector2>();
        for (int i = 0; i < road.Length; i++)
        {
            Vector2 a = road[i], b = road[(i + 1) % road.Length];
            int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 6f));
            for (int p = 0; p < pieces; p++) nodes.Add(Vector2.Lerp(a, b, (float)p / pieces));
        }
        int entry = Nearest(start, nodes), exit = Nearest(finish, nodes);
        var clockwise = Walk(nodes, entry, exit, 1);
        var counterclockwise = Walk(nodes, entry, exit, -1);
        result = RouteLength(start, clockwise) <= RouteLength(start, counterclockwise) ? clockwise : counterclockwise;
        result.Add(finish);
        return result;
    }
    static int Nearest(Vector2 point, List<Vector2> nodes)
    {
        int best = 0; float distance = float.PositiveInfinity;
        for (int i = 0; i < nodes.Count; i++)
        { float d = (point - nodes[i]).sqrMagnitude; if (d < distance) { distance = d; best = i; } }
        return best;
    }
    static List<Vector2> Walk(List<Vector2> nodes, int from, int to, int step)
    {
        var result = new List<Vector2>();
        for (int n = 0; n <= nodes.Count; n++)
        { result.Add(nodes[from]); if (from == to) break; from = (from + nodes.Count + step) % nodes.Count; }
        return result;
    }
    internal static float RouteLength(Vector2 from, List<Vector2> points)
    {
        float sum = 0;
        foreach (var p in points) { sum += Vector2.Distance(from, p); from = p; }
        return sum;
    }
}
