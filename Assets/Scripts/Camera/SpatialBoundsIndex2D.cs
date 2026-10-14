using System.Collections.Generic;
using UnityEngine;

// Broad phase only: callers retain their exact scene, frustum and circle tests.
// Oversized entries/queries take a bounded fallback instead of filling huge grids.
public sealed class SpatialBoundsIndex2D<T> where T : class
{
    private struct Entry { public Bounds bounds; public Vector2Int min, max; public bool large; }
    private readonly float cellSize;
    private readonly Dictionary<T, Entry> entries = new Dictionary<T, Entry>();
    private readonly Dictionary<Vector2Int, HashSet<T>> cells = new Dictionary<Vector2Int, HashSet<T>>();
    private readonly HashSet<T> large = new HashSet<T>();
    private readonly HashSet<T> seen = new HashSet<T>();
    private readonly Stack<HashSet<T>> pool = new Stack<HashSet<T>>();
    public SpatialBoundsIndex2D(float size = 12f) { cellSize = Mathf.Max(.1f, size); }
    private Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.y / cellSize));
    private static long Area(Vector2Int min, Vector2Int max) => ((long)max.x-min.x+1) * ((long)max.y-min.y+1);
    public void Set(T item, Bounds bounds)
    {
        if (entries.TryGetValue(item, out Entry old) && old.bounds == bounds) return;
        var entry = new Entry { bounds = bounds, min = Cell(bounds.min), max = Cell(bounds.max) };
        entry.large = Area(entry.min, entry.max) > 256;
        if (entries.ContainsKey(item) && entry.min == old.min && entry.max == old.max)
        { entries[item] = entry; return; }
        Remove(item);
        entries[item] = entry;
        if (entry.large) { large.Add(item); return; }
        for (int x = entry.min.x; x <= entry.max.x; x++)
            for (int y = entry.min.y; y <= entry.max.y; y++)
            {
                var key = new Vector2Int(x,y);
                if (!cells.TryGetValue(key, out var bucket))
                { bucket = pool.Count > 0 ? pool.Pop() : new HashSet<T>(); cells.Add(key,bucket); }
                bucket.Add(item);
            }
    }
    public void Remove(T item)
    {
        if (!entries.TryGetValue(item, out Entry entry)) return;
        entries.Remove(item);
        if (entry.large) { large.Remove(item); return; }
        for (int x = entry.min.x; x <= entry.max.x; x++)
            for (int y = entry.min.y; y <= entry.max.y; y++)
            {
                var key = new Vector2Int(x,y);
                if (!cells.TryGetValue(key, out var bucket)) continue;
                bucket.Remove(item);
                if (bucket.Count == 0) { cells.Remove(key); pool.Push(bucket); }
            }
    }
    public void Query(Bounds area, List<T> result)
    {
        result.Clear(); seen.Clear();
        var min = Cell(area.min); var max = Cell(area.max);
        if (Area(min,max) > 4096)
        { foreach (var pair in entries) if (Intersects(pair.Value.bounds,area)) result.Add(pair.Key); return; }
        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                if (cells.TryGetValue(new Vector2Int(x,y), out var bucket))
                    foreach (var item in bucket)
                        if (seen.Add(item) && Intersects(entries[item].bounds, area)) result.Add(item);
        foreach (var item in large) if (Intersects(entries[item].bounds,area)) result.Add(item);
    }
    private static bool Intersects(Bounds a, Bounds b) => a.min.x <= b.max.x && a.max.x >= b.min.x &&
        a.min.y <= b.max.y && a.max.y >= b.min.y;
    public void Clear()
    {
        entries.Clear(); large.Clear(); seen.Clear();
        foreach (var bucket in cells.Values) { bucket.Clear(); pool.Push(bucket); }
        cells.Clear();
    }
}
