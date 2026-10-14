using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Uses an isolated scene containing unchanged copies of the authored room and wall blocks.
public static class OssuaryMazeSmoke
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OssuaryMazeSmoke.unity");
            var group = GameObject.Find("Ossuary Maze");
            Check(group && group.transform.parent.name == "Walls", "maze grouped under existing Walls");
            Vector2 origin = new Vector2(39.1670301f, 43.79673885f);
            Check(Vector2.Distance(group.transform.position, origin) < .0001f, "correct screenshot region");
            Check(group.transform.childCount == 36, "36 editable primitives");
            Check(group.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "no per-frame maze scripts");
            Physics2D.SyncTransforms();
            foreach (Transform child in group.transform)
            {
                var collider = child.GetComponent<BoxCollider2D>();
                var renderer = child.GetComponent<SpriteRenderer>();
                Check(child.gameObject.layer == 7 && collider && collider.enabled && !collider.isTrigger, "Blocks collider " + child.name);
                Check(renderer && renderer.sprite && renderer.enabled && renderer.color == Color.white, "existing white square sprite " + child.name);
                Check(renderer.sortingOrder == 0, "same wall rendering order");
                Check(Vector2.Distance(renderer.bounds.size, collider.bounds.size) < .0001f, "visual/collision bounds agree");
                Check(Vector2.Distance(renderer.bounds.center, collider.bounds.center) < .0001f, "no offset invisible collider");
                Check(Physics2D.OverlapPoint(child.position, 1 << 7), "native wall blocks passage");
            }
            // Check actual Unity colliders with a conservative 1.1-unit-wide actor footprint.
            const float step = .1f;
            const int nx = 277, ny = 238;
            var open = new bool[nx * ny]; var seen = new bool[nx * ny]; int total = 0;
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                Vector2 p = origin + new Vector2((x + .5f) * step, (y + .5f) * step);
                if (!Physics2D.OverlapBox(p, new Vector2(1.1f, 1.1f), 0, 1 << 7)) { open[y * nx + x] = true; total++; }
            }
            Func<Vector2, int> cell = p => Mathf.Clamp((int)(p.y / step), 0, ny - 1) * nx + Mathf.Clamp((int)(p.x / step), 0, nx - 1);
            int start = cell(new Vector2(27.63881f, 4.77244f));
            int exit = cell(new Vector2(2.11869f, 0));
            int reward = cell(new Vector2(14.6f, 21.7f));
            Check(open[start] && open[exit] && open[reward], "existing entrance, exit and reserved reward remain clear");
            var queue = new Queue<int>(); queue.Enqueue(start); seen[start] = true; int reached = 0;
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(), x = p % nx, y = p / nx; reached++;
                foreach (int n in new[] { x > 0 ? p - 1 : -1, x + 1 < nx ? p + 1 : -1, y > 0 ? p - nx : -1, y + 1 < ny ? p + nx : -1 })
                { if (n >= 0 && open[n] && !seen[n]) { seen[n] = true; queue.Enqueue(n); } }
            }
            Check(seen[exit] && seen[reward], "native physics route to both destinations");
            Check(reached == total, "no isolated traversable pocket or sealed empty chamber");
            Debug.Log("OSSUARY_MAZE_PASS checks=" + checks + " reachablePhysicsCells=" + reached);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
