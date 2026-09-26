using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded rectilinear A*. Coordinates come from ports and expanded obstacle edges,
/// so thin walls cannot disappear between grid samples and endpoint links never become diagonal.</summary>
public static class OrthogonalCableRouter
{
    private struct Entry
    {
        public int state;
        public float cost, priority;
        public Entry(int s, float c, float p) { state = s; cost = c; priority = p; }
    }

    public static bool TryRouteBottomPorts(Vector2 start, Vector2 end, Rect startBody, Rect endBody,
        float leadLength, IList<Rect> obstacles, float clearance, float margin, int maxNodes, float bendCost,
        List<Vector2> path, IList<Rect> reservations, float stagger, int variation)
        => TryRoutePorts(start, end, new Vector2(0f, -1f), new Vector2(0f, -1f), startBody, endBody,
            leadLength, obstacles, clearance, margin, maxNodes, bendCost, path, reservations, stagger, variation);

    public static bool TryRoutePorts(Vector2 start, Vector2 end, Vector2 startDirection, Vector2 endDirection,
        Rect startBody, Rect endBody, float leadLength, IList<Rect> obstacles,
        float clearance, float margin, int maxNodes, float bendCost, List<Vector2> path,
        IList<Rect> reservations, float stagger, int variation)
    {
        path.Clear();
        if (start.x == end.x && start.y == end.y) { path.Add(start); return true; }
        var startExit = GetPortExit(start, startDirection, startBody, leadLength, clearance);
        var endExit = GetPortExit(end, endDirection, endBody, leadLength, clearance);
        var expanded = new List<Rect>();
        foreach (Rect r in obstacles) expanded.Add(Rect.MinMaxRect(r.xMin - clearance, r.yMin - clearance,
            r.xMax + clearance, r.yMax + clearance));
        // The protected end leads obey wall avoidance too; never fall back to side/top ports.
        if (!Clear(start, startExit, expanded) || !Clear(endExit, end, expanded)) return false;
        var targetBlock = new List<Rect> { endBody };
        var sourceBlock = new List<Rect> { startBody };
        if (!Clear(start, startExit, targetBlock) || !Clear(endExit, end, sourceBlock)) return false;
        var routingBlocks = new List<Rect>(obstacles);
        // Preserve the transformed local-bottom leads; the middle route cannot turn
        // back across the device or silently switch to another face for a shorter path.
        routingBlocks.Add(PortKeepout(startBody, startExit, startDirection, clearance));
        routingBlocks.Add(PortKeepout(endBody, endExit, endDirection, clearance));
        if (!TryRoute(startExit, endExit, routingBlocks, clearance, margin, maxNodes, bendCost,
            path, reservations, stagger, variation)) return false;
        path.Insert(0, start); path.Add(end);
        return true;
    }

    private static Vector2 GetPortExit(Vector2 port, Vector2 direction, Rect body, float minLead, float clearance)
    {
        float toEdge = direction.x > 0f ? body.xMax - port.x : direction.x < 0f ? port.x - body.xMin
            : direction.y > 0f ? body.yMax - port.y : port.y - body.yMin;
        float lead = Mathf.Max(minLead, toEdge + clearance + .08f);
        return new Vector2(port.x + direction.x * lead, port.y + direction.y * lead);
    }
    private static Rect PortKeepout(Rect body, Vector2 exit, Vector2 direction, float clearance)
    {
        float inset = clearance + .001f;
        return Rect.MinMaxRect(direction.x < 0f ? exit.x + inset : body.xMin,
            direction.y < 0f ? exit.y + inset : body.yMin,
            direction.x > 0f ? exit.x - inset : body.xMax,
            direction.y > 0f ? exit.y - inset : body.yMax);
    }

    public static bool TryRoute(Vector2 start, Vector2 end, IList<Rect> obstacles,
        float clearance, float margin, int maxNodes, float bendCost, List<Vector2> path)
        => TryRoute(start, end, obstacles, clearance, margin, maxNodes, bendCost, path, null, 0f, 0);

    /// <param name="reservations">Soft cable corridors, already expanded for both cable widths and spacing.
    /// They can be crossed if necessary but prolonged overlap is expensive, never a reason to cross walls.</param>
    public static bool TryRoute(Vector2 start, Vector2 end, IList<Rect> obstacles,
        float clearance, float margin, int maxNodes, float bendCost, List<Vector2> path,
        IList<Rect> reservations, float stagger, int variation)
    {
        if (!TryRouteCore(start, end, obstacles, clearance, margin, maxNodes, bendCost, path, reservations))
        {
            // Crowded cable reservations may exhaust the bounded graph. Keep a valid wall-safe
            // connection rather than hide it just because perfect cable separation is impossible.
            if (reservations == null || reservations.Count == 0
                || !TryRouteCore(start, end, obstacles, clearance, margin, maxNodes, bendCost, path, null)) return false;
        }
        if (stagger > 0f) AddNaturalTurns(path, obstacles, clearance, reservations, Mathf.Min(stagger, margin * .5f), variation);
        return true;
    }

    private static bool TryRouteCore(Vector2 start, Vector2 end, IList<Rect> obstacles,
        float clearance, float margin, int maxNodes, float bendCost, List<Vector2> path, IList<Rect> reservations)
    {
        path.Clear();
        var area = Rect.MinMaxRect(Mathf.Min(start.x, end.x) - margin,
            Mathf.Min(start.y, end.y) - margin, Mathf.Max(start.x, end.x) + margin,
            Mathf.Max(start.y, end.y) + margin);
        var blocks = new List<Rect>();
        var xs = new List<float> { start.x, end.x, area.xMin, area.xMax };
        var ys = new List<float> { start.y, end.y, area.yMin, area.yMax };
        var cableBands = new List<Rect>();
        if (reservations != null)
            foreach (Rect band in reservations)
            {
                if (!band.Overlaps(area)) continue;
                cableBands.Add(band);
                AddInside(xs, band.xMin - .001f, area.xMin, area.xMax);
                AddInside(xs, band.xMax + .001f, area.xMin, area.xMax);
                AddInside(ys, band.yMin - .001f, area.yMin, area.yMax);
                AddInside(ys, band.yMax + .001f, area.yMin, area.yMax);
            }
        foreach (Rect obstacle in obstacles)
        {
            Rect r = Rect.MinMaxRect(obstacle.xMin - clearance, obstacle.yMin - clearance,
                obstacle.xMax + clearance, obstacle.yMax + clearance);
            if (!r.Overlaps(area)) continue;
            if (Inside(start, r) || Inside(end, r)) return false;
            blocks.Add(r);
            AddInside(xs, r.xMin - .001f, area.xMin, area.xMax);
            AddInside(xs, r.xMax + .001f, area.xMin, area.xMax);
            AddInside(ys, r.yMin - .001f, area.yMin, area.yMax);
            AddInside(ys, r.yMax + .001f, area.yMin, area.yMax);
        }
        if (start.x == end.x && start.y == end.y) { path.Add(start); return true; }
        // Prefer simple direct or one-corner runs when they already avoid every obstacle.
        var corner = new Vector2(end.x, start.y);
        if (Clear(start, corner, blocks) && Clear(corner, end, blocks)
            && Exposure(start, corner, cableBands) + Exposure(corner, end, cableBands) == 0f)
        { path.Add(start); path.Add(corner); path.Add(end); Simplify(path); return true; }
        corner = new Vector2(start.x, end.y);
        if (Clear(start, corner, blocks) && Clear(corner, end, blocks)
            && Exposure(start, corner, cableBands) + Exposure(corner, end, cableBands) == 0f)
        { path.Add(start); path.Add(corner); path.Add(end); Simplify(path); return true; }

        SortUnique(xs); SortUnique(ys);
        int nx = xs.Count, ny = ys.Count;
        if ((long)nx * ny > maxNodes) return false;
        int count = nx * ny * 3;
        var costs = new float[count]; var parents = new int[count];
        for (int i = 0; i < count; i++) { costs[i] = float.PositiveInfinity; parents[i] = -1; }
        int startNode = ys.BinarySearch(start.y) * nx + xs.BinarySearch(start.x);
        int endNode = ys.BinarySearch(end.y) * nx + xs.BinarySearch(end.x);
        var heap = new List<Entry>();
        var edges = new Dictionary<int, bool>();
        int first = startNode * 3;
        costs[first] = 0f; Push(heap, new Entry(first, 0f, Distance(start, end)));
        int remaining = count * 2;
        while (heap.Count > 0 && remaining-- > 0)
        {
            Entry entry = Pop(heap);
            if (entry.cost > costs[entry.state]) continue;
            int node = entry.state / 3, axis = entry.state % 3;
            int x = node % nx, y = node / nx;
            if (node == endNode)
            {
                for (int state = entry.state; state >= 0; state = parents[state])
                { int p = state / 3; path.Add(new Vector2(xs[p % nx], ys[p / nx])); }
                path.Reverse(); Simplify(path); return true;
            }
            Vector2 current = new Vector2(xs[x], ys[y]);
            for (int direction = 0; direction < 4; direction++)
            {
                int xx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                int yy = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                if (xx < 0 || xx >= nx || yy < 0 || yy >= ny) continue;
                int nextNode = yy * nx + xx, nextAxis = direction < 2 ? 1 : 2;
                Vector2 next = new Vector2(xs[xx], ys[yy]);
                int edge = Math.Min(node, nextNode) * 2 + nextAxis - 1;
                bool clear;
                if (!edges.TryGetValue(edge, out clear))
                { clear = Clear(current, next, blocks); edges.Add(edge, clear); }
                if (!clear) continue;
                float cost = entry.cost + Distance(current, next) + Exposure(current, next, cableBands) * 18f
                    + (axis != 0 && axis != nextAxis ? bendCost : 0f);
                int nextState = nextNode * 3 + nextAxis;
                if (cost >= costs[nextState]) continue;
                costs[nextState] = cost; parents[nextState] = entry.state;
                Push(heap, new Entry(nextState, cost, cost + Distance(next, end)));
            }
        }
        return false;
    }

    // Length inside occupied cable corridors, not a binary blocker: common ports and
    // unavoidable short perpendicular crossings still work in constrained passages.
    private static float Exposure(Vector2 a, Vector2 b, IList<Rect> bands)
    {
        if (bands == null) return 0f;
        float total = 0f;
        foreach (Rect r in bands)
        {
            if (a.y == b.y && a.y > r.yMin && a.y < r.yMax)
                total += Mathf.Max(0f, Mathf.Min(Mathf.Max(a.x, b.x), r.xMax) - Mathf.Max(Mathf.Min(a.x, b.x), r.xMin));
            else if (a.x == b.x && a.x > r.xMin && a.x < r.xMax)
                total += Mathf.Max(0f, Mathf.Min(Mathf.Max(a.y, b.y), r.yMax) - Mathf.Max(Mathf.Min(a.y, b.y), r.yMin));
        }
        return total;
    }

    private static float Exposure(List<Vector2> path, IList<Rect> bands)
    {
        float cost = 0f;
        for (int i = 1; i < path.Count; i++) cost += Exposure(path[i - 1], path[i], bands);
        return cost;
    }

    private static void AddNaturalTurns(List<Vector2> path, IList<Rect> obstacles, float clearance,
        IList<Rect> reservations, float stagger, int variation)
    {
        // Add at most two corners, once. Complex routes, short links and cramped spaces stay simple.
        if (path.Count < 2 || path.Count > 7) return;
        var blocks = new List<Rect>();
        foreach (Rect r in obstacles) blocks.Add(Rect.MinMaxRect(r.xMin - clearance, r.yMin - clearance,
            r.xMax + clearance, r.yMax + clearance));
        float originalExposure = Exposure(path, reservations);
        var candidate = new List<Vector2>();
        if (path.Count == 2)
        {
            Vector2 a = path[0], b = path[1];
            if (Distance(a, b) < Mathf.Max(3f, stagger * 6f)) return;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float offset = stagger * (attempt < 2 ? 1f : .5f) * (((attempt + variation) & 1) == 0 ? 1f : -1f);
                Vector2 p = a.y == b.y ? new Vector2(a.x, a.y + offset) : new Vector2(a.x + offset, a.y);
                Vector2 q = a.y == b.y ? new Vector2(b.x, b.y + offset) : new Vector2(b.x + offset, b.y);
                candidate.Clear(); candidate.Add(a); candidate.Add(p); candidate.Add(q); candidate.Add(b);
                if (Accept(candidate, blocks, reservations, originalExposure))
                { path.Clear(); path.AddRange(candidate); return; }
            }
            return;
        }
        for (int n = 0; n < path.Count - 2; n++)
        {
            int i = (n + (variation & 0x7fffffff) % (path.Count - 2)) % (path.Count - 2) + 1;
            Vector2 a = path[i - 1], corner = path[i], b = path[i + 1];
            if (Mathf.Abs(a.x - b.x) < stagger * 3f || Mathf.Abs(a.y - b.y) < stagger * 3f) continue;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float fx = attempt == 0 ? .37f : attempt == 1 ? .63f : .5f;
                float fy = attempt == 0 ? .61f : attempt == 1 ? .39f : .5f;
                float x = a.x + (b.x - a.x) * fx, y = a.y + (b.y - a.y) * fy;
                candidate.Clear();
                for (int j = 0; j < i; j++) candidate.Add(path[j]);
                if (a.y == corner.y)
                { candidate.Add(new Vector2(x, a.y)); candidate.Add(new Vector2(x, y)); candidate.Add(new Vector2(b.x, y)); }
                else
                { candidate.Add(new Vector2(a.x, y)); candidate.Add(new Vector2(x, y)); candidate.Add(new Vector2(x, b.y)); }
                for (int j = i + 1; j < path.Count; j++) candidate.Add(path[j]);
                Simplify(candidate);
                if (candidate.Count <= path.Count || candidate.Count > path.Count + 2) continue;
                if (Accept(candidate, blocks, reservations, originalExposure))
                { path.Clear(); path.AddRange(candidate); return; }
            }
        }
    }

    private static bool Accept(List<Vector2> path, List<Rect> blocks, IList<Rect> bands, float originalExposure)
    {
        if (Exposure(path, bands) > originalExposure + .0001f) return false;
        for (int i = 1; i < path.Count; i++)
        {
            if (!Clear(path[i - 1], path[i], blocks) || Distance(path[i - 1], path[i]) < .08f) return false;
            for (int j = 1; j < i - 1; j++)
            {
                Vector2 a = path[i - 1], b = path[i], c = path[j - 1], d = path[j];
                // Closed segment AABBs are exact for orthogonal lines; reject self-crossing or touching.
                if (Mathf.Max(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)) <= Mathf.Min(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x))
                    && Mathf.Max(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)) <= Mathf.Min(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y))) return false;
            }
        }
        return true;
    }

    private static bool Inside(Vector2 p, Rect r) => p.x > r.xMin && p.x < r.xMax && p.y > r.yMin && p.y < r.yMax;
    private static float Distance(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private static void AddInside(List<float> values, float value, float min, float max)
    { if (value > min && value < max) values.Add(value); }
    private static void SortUnique(List<float> values)
    {
        values.Sort();
        for (int i = values.Count - 1; i > 0; i--) if (values[i] == values[i - 1]) values.RemoveAt(i);
    }
    private static bool Clear(Vector2 a, Vector2 b, List<Rect> blocks)
    {
        if (a.x != b.x && a.y != b.y) return false;
        foreach (Rect r in blocks)
        {
            if (a.y == b.y && a.y > r.yMin && a.y < r.yMax
                && Mathf.Max(a.x, b.x) > r.xMin && Mathf.Min(a.x, b.x) < r.xMax) return false;
            if (a.x == b.x && a.x > r.xMin && a.x < r.xMax
                && Mathf.Max(a.y, b.y) > r.yMin && Mathf.Min(a.y, b.y) < r.yMax) return false;
            if (Inside(a, r) || Inside(b, r)) return false;
        }
        return true;
    }
    private static void Simplify(List<Vector2> path)
    {
        for (int i = path.Count - 1; i > 0; i--)
            if (path[i].x == path[i - 1].x && path[i].y == path[i - 1].y) path.RemoveAt(i);
        for (int i = path.Count - 2; i > 0; i--)
            if ((path[i - 1].x == path[i].x && path[i].x == path[i + 1].x)
                || (path[i - 1].y == path[i].y && path[i].y == path[i + 1].y)) path.RemoveAt(i);
    }
    private static void Push(List<Entry> heap, Entry value)
    {
        int at = heap.Count; heap.Add(value);
        while (at > 0)
        {
            int parent = (at - 1) / 2;
            if (heap[parent].priority <= value.priority) break;
            heap[at] = heap[parent]; at = parent;
        }
        heap[at] = value;
    }
    private static Entry Pop(List<Entry> heap)
    {
        Entry result = heap[0], value = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0) return result;
        int at = 0;
        while (at * 2 + 1 < heap.Count)
        {
            int child = at * 2 + 1;
            if (child + 1 < heap.Count && heap[child + 1].priority < heap[child].priority) child++;
            if (value.priority <= heap[child].priority) break;
            heap[at] = heap[child]; at = child;
        }
        heap[at] = value; return result;
    }
}
